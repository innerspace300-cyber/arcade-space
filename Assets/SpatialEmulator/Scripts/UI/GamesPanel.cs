// GamesPanel.cs — a panel on the cabinet's left, titled MENU, with the GAMES key (opens
// the game picker) and, under it, the CRT MODE / FULL / CAP / LOCK keys
// (CrtEffect puts them in); the mirror of the save panel on its right
// (SaveStatePanel): a copy of that panel's look, kept at its
// mirror place across the screen, at its size, and shown when it is. With a
// cabinet placed in AR this is where GAMES is (GamePicker hides the screen's
// key then). Made at run time from the save panel (CrtEffect, which also
// adds that panel's own extra keys).

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpatialEmulator.Mobile;

namespace SpatialEmulator.UI
{
    public class GamesPanel : MonoBehaviour
    {
        SaveStatePanel _source;
        GameObject _sourceContent, _content;
        Canvas _canvas;
        Transform _stack;

        /// Makes the panel beside the save panel (once); its Content, for
        /// more keys to go under GAMES, or null.
        public static Transform Create(SaveStatePanel save)
        {
            if (!save) return null;
            var made = save.transform.parent.Find("Games Panel");
            if (made) return made.Find("Content");
            // Copied under a switched-off holder, so none of the copy's
            // components start up before the parts it doesn't need are gone.
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            var copy = Instantiate(save.gameObject, holder.transform, false);
            copy.name = "Games Panel";
            DestroyImmediate(copy.GetComponent<SaveStatePanel>());
            var content = copy.transform.Find("Content");
            var keep = content ? content.Find("Controls Line") : null;
            if (!content || !keep) { Destroy(holder); return null; }
            // Its title (MENU) and the line under it, then the key.
            var title = content.Find("Title");
            var rule = content.Find("Demo Rule");
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                if (child != keep && child != title && child != rule) DestroyImmediate(child.gameObject);
            }
            if (title && title.GetComponent<TMP_Text>() is { } heading) { heading.text = "MENU"; title.gameObject.SetActive(true); }
            if (rule) rule.gameObject.SetActive(true);
            for (int i = copy.transform.childCount - 1; i >= 0; i--)   // (the LOCK key, beside Content)
                if (copy.transform.GetChild(i) != content) DestroyImmediate(copy.transform.GetChild(i).gameObject);
            keep.name = "Games Line";
            keep.gameObject.SetActive(true);
            var key = keep.GetComponentInChildren<Button>(true);
            key.name = "Games Button";
            var label = key.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = "GAMES";
            key.onClick = new Button.ButtonClickedEvent();
            key.onClick.AddListener(() =>
            {
                Haptics.Play(Haptics.Kind.Light);
                var picker = FindAnyObjectByType<GamePicker>(FindObjectsInactive.Include);
                if (picker) picker.Open();
            });
            copy.transform.SetParent(save.transform.parent, false);
            Destroy(holder);
            var panel = copy.AddComponent<GamesPanel>();
            panel._source = save;
            panel._sourceContent = save.transform.Find("Content").gameObject;
            panel._content = content.gameObject;
            panel._canvas = copy.GetComponent<Canvas>();
            panel.LateUpdate();
            return content;
        }

        void LateUpdate()
        {
            if (!_source) { Destroy(gameObject); return; }
            if (_canvas && !_canvas.worldCamera) _canvas.worldCamera = Camera.main;
            if (_content.activeSelf != _sourceContent.activeSelf) _content.SetActive(_sourceContent.activeSelf);
            // Its mirror across the screen's middle.
            var from = _source.transform;
            if (!_stack && from.parent && from.parent.GetComponentInChildren<MobileRetroDepthLayerStack>() is { } stack) _stack = stack.transform;
            float middle = _stack ? _stack.localPosition.x : 0f;
            var place = from.localPosition;
            transform.localPosition = new Vector3(2f * middle - place.x, place.y, place.z);
            transform.localRotation = from.localRotation;
            transform.localScale = from.localScale;
        }
    }
}
