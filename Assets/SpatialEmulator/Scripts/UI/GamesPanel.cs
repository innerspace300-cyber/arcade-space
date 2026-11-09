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
        GameObject _sourceContent, _content, _menuKey;
        RectTransform _moveKey;
        Image _menuLockCap;
        GameObject _menuLockLine;
        TMP_Text _menuLockLabel;
        Image _moveCap;
        Sprite _up, _down;
        CabinetManipulator _manipulator;
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
            var menuKey = MenuKey(copy.transform);
            copy.transform.SetParent(save.transform.parent, false);
            Destroy(holder);
            var panel = copy.AddComponent<GamesPanel>();
            panel._source = save;
            panel._sourceContent = save.transform.Find("Content").gameObject;
            panel._content = content.gameObject;
            panel._canvas = copy.GetComponent<Canvas>();
            if (panel._canvas) { panel._canvas.overrideSorting = true; panel._canvas.sortingOrder = SaveStatePanel.SideOrder; }   // (over the game layers, as the save panel)
            panel._menuKey = menuKey;
            panel.LateUpdate();
            return content;
        }

        const float KeySize = 22f, KeyGap = 2f, LockGap = 4f, MoveGap = 3f;   // canvas units
        // Beside the frame: the room the three bars take over MENU, and how far
        // their top bar is under the frame's top.
        const float Header = 14f, BarsTop = 1.5f;
        // On the 3D cabinet: the bars' room over MENU (a wider gap under them than by the frame).
        internal const float CabinetHeader = 20f;
        /// LOCK's row under the bars on the 3D cabinet, in canvas units (0 elsewhere): both panels hang that far lower.
        internal static float CabinetKeysRow;
        const float LockScale = 0.7f;
        const float FrameNudge = 1.5f;   // canvas units: beside the frame, MENU (bars, keys and all) a little further left
        const float CabinetKeyScale = 0.55f;   // LOCK and the move key by the 3D cabinet's bars (45% smaller than the menu's keys)

        // The 3D cabinet (the panels at its corner), not the picture frame.
        bool OnCabinet => _source && (_source.Corner != null || !_source.frame);

        bool Locked
        {
            get
            {
                if (!_manipulator) _manipulator = FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
                return _manipulator && _manipulator.locked;
            }
        }

        static void Usable(GameObject go, bool usable)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (!group) group = go.AddComponent<CanvasGroup>();
            float alpha = usable ? 1f : 0.35f;
            if (group.alpha != alpha) group.alpha = alpha;
            group.interactable = group.blocksRaycasts = usable;
        }

        // UNLOCK in the menu (its last row, only while it's locked): the
        // only way to unlock once LOCK by the frame has been pressed.
        void MakeMenuLock(RectTransform lockKey)
        {
            var lockButton = lockKey.GetComponent<CabinetLockButton>();
            if (!lockButton) return;
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            var key = Instantiate(lockKey.gameObject, holder.transform, false);
            key.name = "Menu Lock Button";
            DestroyImmediate(key.GetComponent<CabinetLockButton>());
            if (key.GetComponent<StackBelow>() is { } below) DestroyImmediate(below);
            if (key.GetComponent<CanvasGroup>() is { } group) DestroyImmediate(group);
            var rect = (RectTransform)key.transform;
            rect.localScale = Vector3.one;
            _menuLockCap = key.GetComponent<Image>();
            _menuLockLabel = key.GetComponentInChildren<TMP_Text>(true);
            _up = lockButton.unlockedSprite;
            _down = lockButton.lockedSprite;
            var button = key.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (!_manipulator) _manipulator = FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
                if (_manipulator) _manipulator.locked = !_manipulator.locked;
                Haptics.Play(Haptics.Kind.Light);
            });
            // A row of its own at the bottom of MENU, like the others (CrtEffect).
            var line = new GameObject("Lock Line", typeof(RectTransform), typeof(LayoutElement));
            line.layer = gameObject.layer;
            line.transform.SetParent(_content.transform, false);
            line.transform.SetAsLastSibling();
            var size = line.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = rect.sizeDelta.y;
            rect.SetParent(line.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            _menuLockLine = line;
            Destroy(holder);
        }

        // Only while it's locked (MENU keeps its height: everything in it
        // scales down a little to make room, SaveStatePanel.MenuGroupHeight).
        void ShowMenuLock()
        {
            bool locked = Locked;
            bool row = false;   // (LOCK itself toggles now, by the frame as on the cabinet: no UNLOCK row)
            if (_menuLockLine && _menuLockLine.activeSelf != row) _menuLockLine.SetActive(row);
            string text = locked ? "UNLOCK" : "LOCK";
            if (_menuLockLabel && _menuLockLabel.text != text) _menuLockLabel.text = text;
            if (_menuLockCap && _up && _down) _menuLockCap.sprite = locked ? _down : _up;
        }

        // The move key: LOCK's keycap, square, with a four-way arrow on it;
        // tapped, the cabinet's selected (its arrows, ring and Delete) or let go.
        void MakeMoveKey(RectTransform lockKey)
        {
            var lockButton = lockKey.GetComponent<CabinetLockButton>();
            if (!lockButton) return;
            _up = lockButton.unlockedSprite;
            _down = lockButton.lockedSprite;
            // (Copied under a switched-off holder: LOCK's own script never starts on the copy.)
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            var key = Instantiate(lockKey.gameObject, holder.transform, false);
            key.name = "Move Key";
            DestroyImmediate(key.GetComponent<CabinetLockButton>());
            if (key.GetComponent<StackBelow>() is { } below) DestroyImmediate(below);
            var label = key.GetComponentInChildren<TMP_Text>(true);
            Color ink = label ? label.color : Color.white;
            if (label) DestroyImmediate(label.gameObject);
            _moveKey = (RectTransform)key.transform;
            _moveKey.sizeDelta = new Vector2(_moveKey.sizeDelta.y + 2f, _moveKey.sizeDelta.y);
            _moveCap = key.GetComponent<Image>();
            // The arrow: a cross, a point at each end (canvas pixels; up 2 like a label).
            void Bit(float x, float y, float w, float h)
            {
                var bit = new GameObject("Arrow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                bit.gameObject.layer = key.layer;
                bit.transform.SetParent(_moveKey, false);
                bit.color = ink;
                bit.raycastTarget = false;
                var r = bit.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(w, h);
                r.anchoredPosition = new Vector2(x, y + 1f);
            }
            Bit(0f, 0f, 12f, 1.5f);
            Bit(0f, 0f, 1.5f, 12f);
            foreach (var (dx, dy) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            {
                // Each point: two steps back from the tip, wider.
                Bit(dx * 5f, dy * 5f, dx != 0f ? 1.5f : 4f, dx != 0f ? 4f : 1.5f);
                Bit(dx * 4f, dy * 4f, dx != 0f ? 1.5f : 6f, dx != 0f ? 6f : 1.5f);
            }
            var button = key.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (!_manipulator) _manipulator = FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
                if (_manipulator) _manipulator.ToggleSelected();
                Haptics.Play(Haptics.Kind.Light);
            });
            _moveKey.SetParent(transform, false);
            Destroy(holder);
            _manipulator = FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
        }

        // The three-bar key: just the bars, like the screen's own menu key
        // (MenuDropdown), by the frame's top left corner; it stays, to fold
        // the panels away again.
        static GameObject MenuKey(Transform panel)
        {
            var key = new GameObject("Menu Key", typeof(RectTransform), typeof(Image), typeof(Button));
            key.layer = panel.gameObject.layer;
            var rect = (RectTransform)key.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(KeySize, KeySize);
            key.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // (only there to be tapped)
            for (int i = -1; i <= 1; i++)
            {
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                bar.gameObject.layer = key.layer;
                bar.transform.SetParent(rect, false);
                bar.color = Color.white;
                bar.raycastTarget = false;
                var shadow = bar.gameObject.AddComponent<Shadow>();   // (seen against a bright room)
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(0.5f, -0.5f);
                var barRect = bar.rectTransform;
                barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.sizeDelta = new Vector2(14f, 1f);
                barRect.anchoredPosition = new Vector2(0f, i * 4f);
            }
            var button = key.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                // The game holds while the side menus are open; 3, 2, 1 as they close (MenuPause).
                SaveStatePanel.SideMenusOpen = !SaveStatePanel.SideMenusOpen;
                if (SaveStatePanel.SideMenusOpen) MenuPause.Open(MenuPause.Menu.Side); else MenuPause.Close(MenuPause.Menu.Side);
                Haptics.Play(Haptics.Kind.Light);
            });
            return key;
        }

        void LateUpdate()
        {
            if (!_source) { Destroy(gameObject); return; }
            if (_canvas && !_canvas.worldCamera) _canvas.worldCamera = Camera.main;
            if (_content.activeSelf != _sourceContent.activeSelf) _content.SetActive(_sourceContent.activeSelf);
            // Folded: only the three bars (and LOCK, which isn't in the menu); open: the panel too.
            bool open = SaveStatePanel.SideMenusOpen;
            RectTransform lockKey = null;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.gameObject == _menuKey) continue;
                // LOCK and the move key, by the frame: shown, but once it's locked
                // they're dimmed and can't be tapped - it's unlocked from the menu.
                // On the 3D cabinet they're part of the menu (shown only while it's open); by the
                // frame they stay out, dimmed once it's locked (the menu's UNLOCK row unlocks).
                // On the 3D cabinet they're out of the menu too, always there (LOCK toggles: tap to lock, again to unlock).
                // (By the frame too, as on the cabinet: shown, LOCK always tappable - locked it reads UNLOCK.)
                if (_moveKey && child == _moveKey) { SaveStatePanel.Fade(child.gameObject, true); Usable(child.gameObject, !Locked); continue; }
                if (child.GetComponent<StackBelow>()) { lockKey = (RectTransform)child; SaveStatePanel.Fade(child.gameObject, true); Usable(child.gameObject, true); continue; }
                SaveStatePanel.Fade(child.gameObject, open);
            }
            // How tall MENU is (the panels are scaled to the frame's height by it).
            if (_content.activeInHierarchy)
            {
                float menu = ((RectTransform)_content.transform).rect.height;
                // (Only once it's laid out - its title and keys, well over 60 -
                // or the panels would be scaled up to fit a sliver.)
                // (By the frame it's LOCK's row's top down to MENU's bottom, measured in FitToFrame.)
                _menuLaidOut = menu > 60f;
            }

            // Its mirror across the screen's middle.
            var from = _source.transform;
            if (!_stack && from.parent && from.parent.GetComponentInChildren<MobileRetroDepthLayerStack>() is { } stack) _stack = stack.transform;
            float middle = _stack ? _stack.localPosition.x : 0f;
            var place = from.localPosition;
            transform.localPosition = new Vector3(2f * middle - place.x, place.y, place.z);
            var rect = (RectTransform)transform;
            float s = from.localScale.x, half = rect.rect.width * 0.5f;
            float top = rect.rect.height * (1f - rect.pivot.y);
            // The 3D cabinet: the bars where MENU's top was (their top bar level with the
            // cabinet's top), right end level with MENU's right edge (clear of the
            // cabinet beside it), and MENU (with LOCK and the move key) a gap below them.
            Vector2 key = new Vector2(half - 7f, top + CabinetHeader - BarsTop - 4.5f);
            // The 3D cabinet: LOCK and the move key in a row under the bars, open or
            // folded alike - MENU opens below them (the panel down that far, the bars
            // and the keys kept where they are).
            float keysRow = 0f;
            if (_menuKey && lockKey) keysRow = LockGap + lockKey.rect.height * CabinetKeyScale;   // (cabinet and frame alike)
            if (Mathf.Abs(CabinetKeysRow - keysRow) > 0.01f) CabinetKeysRow = keysRow;   // (the panel this mirrors drops by it too: their tops level)
            if (keysRow > 0f && s > 0f)
            {
                if (_source.Corner == null) transform.localPosition -= transform.localRotation * Vector3.up * (keysRow * s);   // (with a corner it's down already: mirrored)
                key.y += keysRow;
            }
            // (MENU's a gap below them: the panel it mirrors is down that far already - SaveStatePanel - so their tops line up.)
            // Beside the frame: just off its left edge, laid out as on the 3D cabinet - LOCK's
            // row's top level with the frame's (FitToFrame), the bars above it, MENU down to the frame's bottom.
            if (_source.frame && _source.Corner == null && s > 0f)
            {
                float frameLeft = middle - _source.frame.OuterHalfWidth;
                float panelMiddle = frameLeft - (KeyGap + FrameNudge + half) * s;
                // (Its height set in FitToFrame; the bars and keys above it spaced as on the 3D cabinet.)
                transform.localPosition = new Vector3(panelMiddle, place.y, place.z);
                key = new Vector2(half - 7f, top + CabinetHeader + keysRow - BarsTop - 4.5f);
            }
            if (_menuKey)
            {
                ((RectTransform)_menuKey.transform).anchoredPosition = key;
                // The style's neon (the dialogue lines' colour: cyan in CLASSIC).
                var ink = CabinetStyles.DialogLine;
                foreach (var bar in _menuKey.GetComponentsInChildren<Image>())
                    if (bar.gameObject != _menuKey && bar.color != ink) bar.color = ink;
            }
            // LOCK: beside the frame, under its bottom left corner, its left
            // edge level with the frame's (otherwise under the panel, as it was).
            if (lockKey && lockKey.GetComponent<StackBelow>() is { } below)
            {
                bool besideFrame = !_menuKey && _source.frame && _source.Corner == null && s > 0f;
                // (The 3D cabinet, folded: right under the three bars - placed here, not stacked under MENU.)
                bool underBars = !besideFrame && _menuKey;
                below.enabled = !besideFrame && !underBars;
                if (!_moveKey) MakeMoveKey(lockKey);
                if (!_menuLockCap) MakeMenuLock(lockKey);
                ShowMenuLock();
                bool selected = _manipulator && _manipulator.isSelected;
                if (_moveKey && _moveCap && _up && _down) _moveCap.sprite = selected ? _down : _up;
                if (besideFrame)
                {
                    float frameLeft = middle - _source.frame.OuterHalfWidth;
                    float frameBottom = (_stack ? _stack.localPosition.y : 0f) - _source.frame.OuterHalfHeight;
                    // (Its anchor and pivot: the panel's top middle and its own top middle.)
                    float panelTop = transform.localPosition.y + rect.rect.height * (1f - rect.pivot.y) * s;
                    // 30% smaller than the panel's keys; the move key just right of it.
                    lockKey.localScale = Vector3.one * LockScale;
                    float lockWidth = lockKey.rect.width * LockScale;
                    var at = new Vector2(frameLeft + lockWidth * 0.5f * s, frameBottom - LockGap * s);
                    lockKey.anchoredPosition = new Vector2((at.x - transform.localPosition.x) / s, (at.y - panelTop) / s);
                    if (_moveKey)
                    {
                        if (!_moveKey.gameObject.activeSelf) _moveKey.gameObject.SetActive(true);
                        _moveKey.localScale = Vector3.one * LockScale;
                        _moveKey.anchoredPosition = lockKey.anchoredPosition + new Vector2(lockWidth * 0.5f + MoveGap + _moveKey.rect.width * LockScale * 0.5f, 0f);
                    }
                }
                else if (underBars)
                {
                    // The 3D cabinet: right under the three bars, small, in a row - the move
                    // key on the bars' side (the panel's right edge), LOCK left of it.
                    float k = CabinetKeyScale;
                    lockKey.localScale = Vector3.one * k;
                    // (The bars' place this frame - from the panel's middle - from its pivot.)
                    float barsBottom = key.y + (0.5f - rect.pivot.y) * rect.rect.height - KeySize * 0.5f;
                    float right = half;
                    if (_moveKey)
                    {
                        if (!_moveKey.gameObject.activeSelf) _moveKey.gameObject.SetActive(true);
                        _moveKey.localScale = Vector3.one * k;
                        float w = _moveKey.rect.width * k;
                        PlaceTop(_moveKey, right - w * 0.5f, barsBottom - LockGap, k);
                        right -= w + MoveGap;
                    }
                    PlaceTop(lockKey, right - lockKey.rect.width * k * 0.5f, barsBottom - LockGap, k);
                }
                else
                {
                    // The 3D cabinet: LOCK under MENU (StackBelow sets its height), its
                    // left edge level with the panel's; the move key under LOCK, the same.
                    lockKey.localScale = Vector3.one;
                    lockKey.anchoredPosition = new Vector2(-half + lockKey.rect.width * 0.5f, lockKey.anchoredPosition.y);
                    if (_moveKey)
                    {
                        if (!_moveKey.gameObject.activeSelf) _moveKey.gameObject.SetActive(true);
                        _moveKey.localScale = Vector3.one;
                        _moveKey.anchoredPosition = new Vector2(-half + _moveKey.rect.width * 0.5f, lockKey.anchoredPosition.y - lockKey.rect.height - LockGap);
                    }
                }
            }
            transform.localRotation = from.localRotation;
            transform.localScale = from.localScale;
            if (_source.frame && _source.Corner == null && s > 0f && lockKey && _menuKey) FitToFrame(lockKey, s);
        }

        bool _menuLaidOut;
        static readonly Vector3[] s_corners = new Vector3[4];

        // Beside the frame: LOCK's row's top level with the frame's top (the bars above it, over the
        // frame), and the panels scaled (SaveStatePanel, by MenuGroupHeight) so MENU's bottom is the frame's.
        void FitToFrame(RectTransform lockKey, float s)
        {
            var parent = transform.parent;
            if (!parent) return;
            float frameTop = (_stack ? _stack.localPosition.y : 0f) + _source.frame.OuterHalfHeight;
            lockKey.GetWorldCorners(s_corners);
            float keysTop = parent.InverseTransformPoint(s_corners[1]).y;
            transform.localPosition += Vector3.up * (frameTop - keysTop);
            if (!_menuLaidOut || !_content.activeInHierarchy) return;
            ((RectTransform)_content.transform).GetWorldCorners(s_corners);
            float bottom = parent.InverseTransformPoint(s_corners[0]).y + (frameTop - keysTop);
            float group = (frameTop - bottom) / s;   // (in the panel's own units)
            if (group > 1f && Mathf.Abs(group - SaveStatePanel.MenuGroupHeight) > 0.5f) SaveStatePanel.MenuGroupHeight = group;
        }

        // A key's middle across at x and its top at y (the panel's own units, from its pivot).
        static void PlaceTop(RectTransform key, float x, float top, float scale = 1f)
        {
            float y = top - key.rect.height * scale * (1f - key.pivot.y);
            x += key.rect.width * scale * (key.pivot.x - 0.5f);
            var at = new Vector3(x, y, key.localPosition.z);
            if (key.localPosition != at) key.localPosition = at;
        }
    }
}
