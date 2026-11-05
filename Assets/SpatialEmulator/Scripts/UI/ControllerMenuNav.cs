// ControllerMenuNav.cs — the menus with a controller (casting to a TV, nothing
// to tap): while a menu or window is open - the screen's three-bar menu
// (Options opens it: GamepadInput), its side section, Settings, SCORES, SAVES,
// PLAY AS, the AR side menus, a CAP video's ShareWindow - the d-pad or left stick moves a gold frame from
// key to key on screen (the nearest one that way), Cross presses the framed
// key as a tap would, Circle backs out (the window's close key, or the menu).
// Only keys that are showing and on top count: one under a window or faded
// away isn't reached. The game picker keeps its own (GamePicker). Made at run time.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class ControllerMenuNav : MonoBehaviour
    {
        const float Dead = 0.5f, RepeatFirst = 0.35f, RepeatNext = 0.12f;
        static readonly Color Gold = new Color(1f, 0.82f, 0.18f), Glint = new Color(1f, 0.97f, 0.8f);

        static ControllerMenuNav s_instance;

        /// A menu's open with a controller: it's steering the menu, not the game (GamepadInput).
        public static bool Active { get; private set; }

        Button _current;
        Vector2 _heldDir;
        float _nextRepeat;
        Canvas _canvas;
        RectTransform _frame;
        Image[] _edges;
        readonly List<Button> _keys = new List<Button>();
        readonly List<Rect> _rects = new List<Rect>();
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        readonly List<CanvasGroup> _groups = new List<CanvasGroup>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (s_instance) return;
            var go = new GameObject("Controller Menu Nav");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<ControllerMenuNav>();
        }

        // The screen the menus are on: the TV's, while they're there (TvOutput).
        static Vector2 Size => TvOutput.MenusOnTv ? TvOutput.Size : new Vector2(Screen.width, Screen.height);
        static int DisplayIndex => TvOutput.MenusOnTv ? TvOutput.Display : 0;

        static bool MenuUp => MenuDropdown.IsShowing || SettingsScreen.IsOpen || LeaderboardWindow.IsOpen || SavesWindow.IsOpen
            || PlayerSelect.IsOpen || SaveStatePanel.SideMenusOpen || ShareWindow.IsOpen;

        void Update()
        {
            var pad = Gamepad.current;
            bool picker = GamePicker.IsOpen;
            Active = pad != null && (MenuUp || picker);
            if (!Active) { _current = null; ShowFrame(false); return; }
            // The game list steers its own games (GamePicker): only the gold frame round the picked one here -
            // till the controller goes up onto the keys along its top.
            if (picker && !GamePicker.HeaderFocus)
            {
                _current = null;
                // Right, off the games: down to ADD ROM.
                bool right = pad.dpad.right.isPressed || pad.leftStick.ReadValue().x > Dead;
                if (right && _heldDir != Vector2.right && GamePicker.AddKey && GamePicker.AddKey.isActiveAndEnabled)
                {
                    GamePicker.HeaderFocus = true;
                    _current = GamePicker.AddKey;
                    _heldDir = Vector2.right;
                    _nextRepeat = Time.unscaledTime + RepeatFirst;
                    return;
                }
                _heldDir = right ? Vector2.right : Vector2.zero;
                var row = GamePicker.FocusedRowRect;
                if (row && ScreenRect(row, out var r) && InList(r)) Frame(r); else ShowFrame(false);
                return;
            }

            Gather();
            int at = _current ? _keys.IndexOf(_current) : -1;
            if (at < 0) { at = First(); _current = at >= 0 ? _keys[at] : null; }
            if (at < 0) { ShowFrame(false); return; }

            // Moving: a step on each push that way, then repeating while it's held.
            Vector2 stick = pad.leftStick.ReadValue();
            Vector2 dir = Vector2.zero;
            if (pad.dpad.up.isPressed || stick.y > Dead) dir = Vector2.up;
            else if (pad.dpad.down.isPressed || stick.y < -Dead) dir = Vector2.down;
            else if (pad.dpad.left.isPressed || stick.x < -Dead) dir = Vector2.left;
            else if (pad.dpad.right.isPressed || stick.x > Dead) dir = Vector2.right;
            if (dir != Vector2.zero && (dir != _heldDir || Time.unscaledTime >= _nextRepeat))
            {
                _nextRepeat = Time.unscaledTime + (dir != _heldDir ? RepeatFirst : RepeatNext);
                int next = picker ? PickerStep(at, dir) : Step(at, dir);
                if (next >= 0) { at = next; _current = _keys[at]; }
                else if (next == ToGames) { GamePicker.BackToGames(dir == Vector2.up ? -1 : dir == Vector2.down ? 1 : 0); _current = null; _heldDir = dir; return; }
            }
            _heldDir = dir;

            if (pad.buttonSouth.wasPressedThisFrame) { Press(_current, _rects[at].center); return; }
            if (pad.buttonEast.wasPressedThisFrame) { Back(); return; }
            Frame(_rects[at]);
        }

        // Every key showing on screen and on top (what a tap at its middle would reach).
        void Gather()
        {
            _keys.Clear(); _rects.Clear();
            var events = EventSystem.current;
            var size = Size;
            float screenArea = size.x * size.y;
            var settings = FindAnyObjectByType<SettingsScreen>();
            var gear = settings ? settings.openButton : null;
            bool picker = GamePicker.IsOpen;
            foreach (var key in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!key.isActiveAndEnabled || !key.interactable) continue;
                if (key.GetComponentInParent<Controls.ArcadeButton>()) continue;   // (the game's own buttons)
                if (picker && key.GetComponentInParent<GameRow>()) continue;   // (the games: the list steers those)
                if (picker && key == gear) continue;   // (Settings isn't reached from over the game list)
                var canvas = key.GetComponentInParent<Canvas>();
                if (!canvas || !canvas.enabled) continue;
                if (!Showing(key.transform)) continue;
                if (!ScreenRect((RectTransform)key.transform, out var rect)) continue;
                if (rect.width < 4f || rect.height < 4f || rect.width * rect.height > screenArea * 0.5f) continue;   // (a blocker over everything isn't a key)
                if (rect.center.x < 0f || rect.center.y < 0f || rect.center.x > size.x || rect.center.y > size.y) continue;
                // On top: a tap at its middle would land on it, not on a window over it. (The Settings gear always counts:
                // it sits apart, over the menu's tap-catcher. Keys on the cabinet in the room aren't reached by a
                // screen point's raycast at all: no hits there means nothing's over them.)
                if (events && key != gear)
                {
                    _hits.Clear();
                    events.RaycastAll(new PointerEventData(events) { position = rect.center, displayIndex = DisplayIndex }, _hits);
                    bool world = canvas.rootCanvas.renderMode == RenderMode.WorldSpace;
                    if (_hits.Count == 0 ? !world : !_hits[0].gameObject.transform.IsChildOf(key.transform)) continue;
                }
                _keys.Add(key); _rects.Add(rect);
            }
        }

        // Where it is on the screen (false: behind the camera).
        static readonly Vector3[] s_corners = new Vector3[4];
        static bool ScreenRect(RectTransform t, out Rect rect)
        {
            rect = default;
            var canvas = t.GetComponentInParent<Canvas>();
            if (!canvas) return false;
            var root = canvas.rootCanvas;
            var cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera ? root.worldCamera : Camera.main;
            t.GetWorldCorners(s_corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var c in s_corners)
            {
                if (cam && cam.WorldToViewportPoint(c).z < 0f) return false;
                var s = RectTransformUtility.WorldToScreenPoint(cam, c);
                min = Vector2.Min(min, s); max = Vector2.Max(max, s);
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        bool Showing(Transform t)
        {
            t.GetComponentsInParent(false, _groups);
            foreach (var g in _groups)
            {
                if (g.alpha < 0.1f || !g.interactable || !g.blocksRaycasts) return false;
                if (g.ignoreParentGroups) break;
            }
            return true;
        }

        // The first key: top left.
        int First()
        {
            int best = -1; float score = float.MaxValue;
            for (int i = 0; i < _rects.Count; i++)
            {
                float s = (Size.y - _rects[i].center.y) * 2f + _rects[i].center.x;
                if (s < score) { score = s; best = i; }
            }
            return best;
        }

        // The nearest key that way (straight on counts more than off to the side).
        int Step(int from, Vector2 dir)
        {
            var c = _rects[from].center;
            int best = -1; float score = float.MaxValue;
            for (int i = 0; i < _rects.Count; i++)
            {
                if (i == from) continue;
                var d = _rects[i].center - c;
                float along = Vector2.Dot(d, dir);
                if (along < 4f) continue;
                float side = Mathf.Abs(d.x * dir.y - d.y * dir.x);
                float s = along + side * 2.5f;
                if (s < score) { score = s; best = i; }
            }
            return best;
        }

        const int ToGames = -2;

        // Round the game list the way goes by where the keys are, not the nearest: SHOW ALL, SHOW HEAVY, the X along
        // the top; ADD ROM, SCAN FOLDER under the games. Down off the top and up (or left) off the bottom: the games.
        // Right runs SHOW ALL, SHOW HEAVY, X, then down to SCAN FOLDER; from the bottom ADD ROM, SCAN FOLDER, X.
        int PickerStep(int at, Vector2 dir)
        {
            var key = _keys[at];
            bool left = dir == Vector2.left, right = dir == Vector2.right, up = dir == Vector2.up, down = dir == Vector2.down;
            Button to = null;
            if (key == GamePicker.AllKey) { if (down) return ToGames; if (right) to = GamePicker.OrderKey; }
            else if (key == GamePicker.OrderKey) { if (down) return ToGames; if (right) to = GamePicker.CloseKey; else if (left) to = GamePicker.AllKey; }
            else if (key == GamePicker.CloseKey) { if (down) return ToGames; if (right) to = GamePicker.ScanKey; else if (left) to = GamePicker.OrderKey; }
            else if (key == GamePicker.AddKey) { if (up || left) return ToGames; if (right) to = GamePicker.ScanKey; }
            else if (key == GamePicker.ScanKey) { if (up) return ToGames; if (right) to = GamePicker.CloseKey; else if (left) to = GamePicker.AddKey; }
            else
            {
                int near = Step(at, dir);
                return near < 0 && down ? ToGames : near;
            }
            return to ? _keys.IndexOf(to) : -1;
        }

        // The row's frame stays in the list's window, and off while a finger's on the screen (swiping the list).
        static bool InList(Rect row)
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return false;
            var view = GamePicker.ListView;
            // (A quarter of it past the edge is still framed: the top game sits right on that edge.)
            float slack = Mathf.Max(2f, row.height * 0.25f);
            return !view || !ScreenRect(view, out var v) || (row.yMin >= v.yMin - slack && row.yMax <= v.yMax + slack);
        }

        // A tap on it: down, up, click - whatever it does on a finger.
        static void Press(Button key, Vector2 at)
        {
            if (!key) return;
            var events = EventSystem.current;
            var data = new PointerEventData(events) { position = at, button = PointerEventData.InputButton.Left, pointerPress = key.gameObject, pointerPressRaycast = default, eligibleForClick = true, displayIndex = DisplayIndex };
            ExecuteEvents.Execute(key.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(key.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(key.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        // Circle: the open window's close key; else the menu folds away.
        void Back()
        {
            for (int i = 0; i < _keys.Count; i++)
                if (_keys[i].name.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0) { Press(_keys[i], _rects[i].center); return; }
            if (MenuDropdown.IsShowing) { MenuDropdown.ToggleFromController(); return; }
            if (SaveStatePanel.SideMenusOpen) { SaveStatePanel.SideMenusOpen = false; MenuPause.Close(MenuPause.Menu.Side); }
        }

        // ---- The gold frame round the chosen key ----

        void Frame(Rect r)
        {
            if (!_canvas) Build();
            ShowFrame(true);
            float pad = Mathf.Max(3f, Size.y / 320f);
            float t = Mathf.Max(2f, Mathf.Round(Size.y / 360f));
            _frame.anchoredPosition = r.min - Vector2.one * pad;
            _frame.sizeDelta = r.size + Vector2.one * pad * 2f;
            var c = Color.Lerp(Gold, Glint, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            for (int i = 0; i < 4; i++)
            {
                _edges[i].color = c;
                var e = _edges[i].rectTransform;
                bool horizontal = i < 2;
                e.sizeDelta = horizontal ? new Vector2(0f, t) : new Vector2(t, 0f);
            }
        }

        void ShowFrame(bool on)
        {
            if (_canvas && _canvas.gameObject.activeSelf != on) _canvas.gameObject.SetActive(on);
        }

        void Build()
        {
            var go = new GameObject("Controller Menu Frame");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            var frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(go.transform, false);
            frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.zero;
            _frame = frame;
            _edges = new Image[4];
            // top, bottom, left, right
            Vector2[] anchors = { new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 0) };
            for (int i = 0; i < 4; i++)
            {
                var e = new GameObject("Edge", typeof(RectTransform)).GetComponent<RectTransform>();
                e.SetParent(frame, false);
                bool horizontal = i < 2;
                e.anchorMin = anchors[i];
                e.anchorMax = horizontal ? new Vector2(1, anchors[i].y) : new Vector2(anchors[i].x, 1);
                e.pivot = new Vector2(i == 3 ? 1 : 0, i == 0 ? 1 : 0);
                e.anchoredPosition = Vector2.zero;
                var img = e.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                _edges[i] = img;
            }
        }
    }
}
