// TvMenuMirror.cs — the game list on the phone too, while it's open on a TV (TvOutput.MenusOnTv), and a finger
// on the phone steering it: tap a game, swipe to scroll, flick to fling. A canvas on the TV's camera is drawn by
// that camera alone, so a second camera can't simply look at it: while the list's open the TV's camera draws into
// a texture instead, shown on both screens - fitted to the phone (the TV's shape, black either side), and whole on
// the TV by a camera of its own. The phone's touches can't reach a canvas on the TV by themselves either, so they're
// turned into the same spot on the TV's picture and handed to what's there, as the UI's input module would.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class TvMenuMirror : MonoBehaviour
    {
        const int PhoneOrder = 31000;   // (over the phone's own canvases, under the splash)

        static TvMenuMirror s_self;

        /// The list is on the phone too, so a finger there is really on the TV's picture.
        public static bool Active => s_self && s_self._texture;

        /// A point on the phone's screen, as the same spot on the TV's picture (while Active).
        public static Vector2 ToTv(Vector2 phone) => (phone - s_self._fit.position) / s_self._fit.size * TvOutput.Size;

        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        RenderTexture _texture;
        Camera _tvCamera, _showCamera;
        GameObject _phone, _show;
        RawImage _phonePicture, _showPicture;
        Rect _fit;

        // The finger down now, on the TV's picture.
        PointerEventData _data;
        GameObject _click;
        Vector2 _downAt;
        bool _dragging;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("TV Menu Mirror");
            DontDestroyOnLoad(go);
            s_self = go.AddComponent<TvMenuMirror>();
        }

        void LateUpdate()
        {
            bool want = TvOutput.MenusOnTv && !TvOutput.Handheld && GamePicker.IsOpen;
            if (want != (_texture != null))
            {
                if (want) Begin();
                else End();
            }
            if (!_texture) return;
            Fit();
            Touch();
        }

        void Begin()
        {
            if (!_phone) Build();
            var size = TvOutput.Size;
            _texture = new RenderTexture(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 24);
            _tvCamera = TvOutput.Camera;
            _tvCamera.targetTexture = _texture;
            _phonePicture.texture = _showPicture.texture = _texture;
            _showCamera.enabled = true;
            _show.SetActive(true);
            _phone.SetActive(true);
        }

        void End()
        {
            Release();
            if (_tvCamera) _tvCamera.targetTexture = null;
            _tvCamera = null;
            _showCamera.enabled = false;
            _show.SetActive(false);
            _phone.SetActive(false);
            _phonePicture.texture = _showPicture.texture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        void Build()
        {
            // On the phone: black, and the picture fitted in it.
            _phone = new GameObject("TV Mirror", typeof(Canvas));   // (TvOutput.Moves leaves it on the phone, by this name)
            _phone.transform.SetParent(transform, false);
            var canvas = _phone.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PhoneOrder;
            var black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            black.transform.SetParent(_phone.transform, false);
            black.color = Color.black;
            black.raycastTarget = false;
            Stretch(black.rectTransform);
            _phonePicture = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            _phonePicture.transform.SetParent(_phone.transform, false);
            _phonePicture.raycastTarget = false;

            // On the TV: a camera of its own, drawing just the picture.
            var at = new GameObject("TV Mirror Camera");
            at.transform.SetParent(transform, false);
            at.transform.position = new Vector3(0f, -20000f, 0f);
            _showCamera = at.AddComponent<Camera>();
            _showCamera.targetDisplay = TvOutput.Display;
            _showCamera.depth = 101;
            _showCamera.cullingMask = 1 << TvOutput.Layer;
            _showCamera.nearClipPlane = 0.1f;
            _showCamera.farClipPlane = 10f;
            _showCamera.clearFlags = CameraClearFlags.SolidColor;
            _showCamera.backgroundColor = Color.black;
            _show = new GameObject("TV Mirror Show", typeof(Canvas));
            _show.layer = TvOutput.Layer;
            _show.transform.SetParent(at.transform, false);
            var showCanvas = _show.GetComponent<Canvas>();
            showCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            showCanvas.worldCamera = _showCamera;
            showCanvas.planeDistance = 1f;
            _showPicture = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            _showPicture.gameObject.layer = TvOutput.Layer;
            _showPicture.transform.SetParent(_show.transform, false);
            _showPicture.raycastTarget = false;
            Stretch(_showPicture.rectTransform);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // The TV's shape, as big as it goes on the phone, whichever way up that is now.
        void Fit()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            var tv = TvOutput.Size;
            float scale = Mathf.Min(screen.x / tv.x, screen.y / tv.y);
            var size = tv * scale;
            _fit = new Rect((screen - size) * 0.5f, size);
            _phonePicture.rectTransform.sizeDelta = size;
        }

        // ---- a finger on the phone, on the TV's picture ----

        void Touch()
        {
            var pointer = Pointer.current;
            if (pointer == null || !EventSystem.current) return;
            var at = pointer.position.ReadValue();
            var tv = ToTv(at);
            if (_data == null)
            {
                if (pointer.press.wasPressedThisFrame && _fit.Contains(at)) Down(at, tv);
                return;
            }
            if (pointer.press.isPressed) Move(at, tv);
            else Release();
        }

        void Down(Vector2 at, Vector2 tv)
        {
            var data = new PointerEventData(EventSystem.current)
            {
                position = tv, pressPosition = tv, button = PointerEventData.InputButton.Left, displayIndex = TvOutput.Display,
            };
            _hits.Clear();
            EventSystem.current.RaycastAll(data, _hits);
            if (_hits.Count == 0) return;
            var hit = _hits[0];
            var go = hit.gameObject;
            data.pointerCurrentRaycast = data.pointerPressRaycast = hit;
            data.rawPointerPress = go;
            data.eligibleForClick = true;
            var pressed = ExecuteEvents.ExecuteHierarchy(go, data, ExecuteEvents.pointerDownHandler);
            _click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
            data.pointerPress = pressed ? pressed : _click;
            data.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(go);
            if (data.pointerDrag) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.initializePotentialDrag);
            _data = data;
            _downAt = at;
            _dragging = false;
        }

        void Move(Vector2 at, Vector2 tv)
        {
            _data.delta = tv - _data.position;
            _data.position = tv;
            if (!_dragging)
            {
                // (Measured on the phone: how far a finger has to go before it's a swipe, not a tap.)
                float threshold = EventSystem.current.pixelDragThreshold;
                if ((at - _downAt).sqrMagnitude < threshold * threshold) return;
                _dragging = _data.dragging = true;
                if (_data.pointerDrag) ExecuteEvents.Execute(_data.pointerDrag, _data, ExecuteEvents.beginDragHandler);
                // A swipe's no tap any more.
                if (_data.pointerPress) ExecuteEvents.Execute(_data.pointerPress, _data, ExecuteEvents.pointerUpHandler);
                _data.pointerPress = null;
                _data.eligibleForClick = false;
            }
            if (_data.pointerDrag && _data.delta != Vector2.zero) ExecuteEvents.Execute(_data.pointerDrag, _data, ExecuteEvents.dragHandler);
        }

        // The finger off (or the list gone from under it).
        void Release()
        {
            var data = _data;
            if (data == null) return;
            _data = null;   // (first: a tap can close the list, and that lands back here)
            if (data.pointerPress) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            if (data.eligibleForClick && _click && _click == data.pointerPress) ExecuteEvents.Execute(_click, data, ExecuteEvents.pointerClickHandler);
            if (_dragging && data.pointerDrag) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.endDragHandler);
            _dragging = false;
            _click = null;
        }
    }
}
