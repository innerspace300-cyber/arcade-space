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
        const float LockScale = 0.7f;

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
            bool row = locked && !OnCabinet;   // (on the cabinet LOCK itself is in the menu)
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
                if (_moveKey && child == _moveKey) { if (OnCabinet) SaveStatePanel.Fade(child.gameObject, open); else Usable(child.gameObject, !Locked); continue; }
                if (child.GetComponent<StackBelow>()) { lockKey = (RectTransform)child; if (OnCabinet) SaveStatePanel.Fade(child.gameObject, open); else Usable(child.gameObject, !Locked); continue; }
                SaveStatePanel.Fade(child.gameObject, open);
            }
            // How tall MENU is (the panels are scaled to the frame's height by it).
            if (_content.activeInHierarchy)
            {
                float menu = ((RectTransform)_content.transform).rect.height;
                // (Only once it's laid out - its title and keys, well over 60 -
                // or the panels would be scaled up to fit a sliver.)
                // (The three bars over it count: together they're the frame's height.)
                float group = menu + Header;
                if (menu > 60f && Mathf.Abs(group - SaveStatePanel.MenuGroupHeight) > 0.5f) SaveStatePanel.MenuGroupHeight = group;
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
            // (MENU's a gap below them: the panel it mirrors is down that far already - SaveStatePanel - so their tops line up.)
            // Beside the frame: the bars just off its top left corner (their
            // top bar a pixel and a half under the frame's top), and MENU
            // under them, its right edge in line with theirs.
            if (_source.frame && _source.Corner == null && s > 0f)
            {
                float frameLeft = middle - _source.frame.OuterHalfWidth;
                float keyMiddle = frameLeft - (KeyGap + KeySize * 0.5f) * s;
                float panelMiddle = frameLeft - (KeyGap + half) * s;
                transform.localPosition = new Vector3(panelMiddle, place.y - Header * s, place.z);
                key = new Vector2((keyMiddle - panelMiddle) / s, top + Header - BarsTop - 4.5f);
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
                bool besideFrame = _source.frame && _source.Corner == null && s > 0f;
                below.enabled = !besideFrame;
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
        }
    }
}
