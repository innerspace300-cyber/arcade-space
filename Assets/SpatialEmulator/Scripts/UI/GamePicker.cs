// GamePicker.cs — the on-screen game picker: lists the romsets in
// Documents/roms (RomLibrary), imports new ones through the Files picker
// (ADD ROM: zips; SCAN FOLDER: every game in a folder at once), and switches the cabinet's game (GameSelection). Built by Tools > Spatial
// Emulator > Build Game Picker (GamePickerBuilder), on its own canvas above
// the arcade controls.
//
// While it's open the game is paused, the arcade controls are off
// (GamepadInput reads IsOpen), and cabinet placement is held
// (SingleCabinetGate reads IsOpen). It opens by
// itself when there's no game to play yet, and when a game fails to start.
//
// "Show all" lists every supported game that isn't installed (main versions
// only - clones and bootlegs still import) below the installed ones, dimmed,
// with the zip name to look for. Holding an installed game shows a Delete button on its row.
//
// With Settings > Games in AR on and a cabinet placed, the window shows in
// the cabinet's frame instead of on the screen: it moves onto a world-space
// canvas that follows the frame, and the game's layers hide while it's open.
// If the cabinet goes away, the window drops back onto the screen.
//
// A controller works it too: up/down picks a row, Cross/A plays it,
// Circle/B closes, Triangle/Y toggles Show all.

using System.Collections;
using System.Collections.Generic;
using SpatialEmulator.Controls;
using SpatialEmulator.Games;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class GamePicker : MonoBehaviour
    {
        // A tap that closes the picker must not also place a cabinet when
        // the finger lifts, so IsOpen stays true this long after closing.
        const float CloseLinger = 0.3f;

        static bool s_open;
        static float s_closedAt = -10f;

        /// True while the picker is showing (and briefly after it closes).
        public static bool IsOpen => s_open || Time.unscaledTime < s_closedAt + CloseLinger;

        /// Opened to pick a game for AR (FULL SCREEN's AR key, from a game
        /// that plays full screen only): the full-screen-only games are left
        /// off the list, and picking one leaves full screen for a fresh
        /// cabinet. Cleared when the list closes.
        public static bool ArOnly { get; private set; }

        public void OpenForAR()
        {
            ArOnly = true;
            Open();
        }

        [Header("Data")]
        public TextAsset catalog;
        public NativeFilePicker filePicker;

        [Header("Layout")]
        public GameObject openButton;
        public GameObject window;
        public ScrollRect list;
        public GameRow rowTemplate;
        // SHOW HEAVY / SHOW VERT, one key right of SHOW ALL: SHOW HEAVY puts
        // the heavy games first, and the key then reads SHOW VERT, which puts
        // the vertical ones first (and back to SHOW HEAVY). SHOW ALL / MY GAMES
        // go back to A-Z. Made at run time.
        enum Order { Alphabetical, HeavyFirst, VerticalFirst }
        Order _order;
        TMP_Text _orderLabel;
        public TMP_Text countLabel;
        public GameObject emptyState;
        public Button addRomButton;
        public Button closeButton;
        public Button showAllButton;
        public TMP_Text showAllLabel;
        public CanvasGroup toast;
        public TMP_Text toastLabel;

        [Header("Look")]
        public Sprite rowSprite;
        public Sprite rowCurrentSprite;
        public Color textColor = new Color32(225, 206, 169, 255);
        public Color dimTextColor = new Color32(165, 123, 109, 255);
        public Color warningColor = new Color32(232, 168, 84, 255);
        public Color errorColor = new Color32(226, 104, 88, 255);
        public float toastSeconds = 4f;
        [Tooltip("Tint of the row the controller is on.")]
        public Color focusTint = new Color(0.55f, 1f, 1f, 1f);
        [Tooltip("Seconds a held direction waits before repeating, then between repeats.")]
        public float repeatDelay = 0.4f, repeatInterval = 0.09f;

        readonly List<GameRow> _rows = new List<GameRow>();
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        Coroutine _toastRoutine;
        bool _showAll;
        GameRow _deleteRow;
        // The press that put the Delete button away shouldn't also tap a row.
        bool _swallowTap;
        int _releaseFrame;
        float _deleteScrollY;

        // In AR: the world-space canvas that follows the cabinet's frame.
        Canvas _arCanvas;
        ScreenFrame _arFrame;
        Transform _windowHome;
        int _windowSibling;
        GameObject _backdrop;
        SafeAreaFitter _safeArea;

        // Controller focus: an index into the active rows, -1 = none yet.
        int _focus = -1;
        int _repeatDir;
        float _nextRepeat;

        void Awake()
        {
            GameCatalog.Load(catalog);
            rowTemplate.gameObject.SetActive(false);
            list.gameObject.AddComponent<ScrollBoost>();   // (scrolls half again as fast: a long list)
            toast.alpha = 0;
            toast.gameObject.SetActive(false);
            openButton.GetComponent<Button>().onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            addRomButton.onClick.AddListener(AddRom);
            AddScanButton();
            showAllButton.onClick.AddListener(ToggleShowAll);
            AddOrderButton();
            GameSelection.LoadFailed += OnLoadFailed;
            GamepadInput.ControllerChanged += OnControllerChanged;
            window.SetActive(false);

            _windowHome = window.transform.parent;
            _windowSibling = window.transform.GetSiblingIndex();
            var backdrop = window.transform.Find("Backdrop");
            _backdrop = backdrop ? backdrop.gameObject : null;
            _safeArea = window.GetComponentInChildren<SafeAreaFitter>(true);

            var ar = new GameObject("Game Picker (AR)", typeof(RectTransform));
            ar.layer = gameObject.layer;
            _arCanvas = ar.AddComponent<Canvas>();
            _arCanvas.renderMode = RenderMode.WorldSpace;
            _arCanvas.sortingOrder = 5; // over the frame's checkerboard
            ar.AddComponent<GraphicRaycaster>();
            ar.SetActive(false);
        }

        void OnDestroy()
        {
            GameSelection.LoadFailed -= OnLoadFailed;
            GamepadInput.ControllerChanged -= OnControllerChanged;
            if (s_open) SetOpen(false);
            if (_arCanvas) Destroy(_arCanvas.gameObject);
        }

        void Start()
        {

            // A touch has to move this far before it scrolls the list instead
            // of tapping a row; the default 10 screen pixels is under a
            // millimetre on a phone.
            var eventSystem = EventSystem.current;
            if (eventSystem) eventSystem.pixelDragThreshold = Mathf.Max(eventSystem.pixelDragThreshold, Mathf.RoundToInt(Screen.dpi > 0 ? Screen.dpi * 0.08f : 30));

            var imported = RomLibrary.ImportLooseZips();
            // A fresh install starts on the demo (in the picture frame, placed
            // like any cabinet); the remembered game gone: start in the picker;
            // a full-screen-only one: straight into full screen, no AR at all.
            string last = GameSelection.Current;
            if (last != Demo.PitDemoGame.GameName && GameSelection.CurrentPath == null) Open();
            else if (GameCatalog.Find(last) is { FullScreenOnly: true }) FullScreenTest.EnterWithCabinet();
            ReportImports(imported);
        }

        public void Open()
        {
            SetOpen(true);
            ReportImports(RomLibrary.ImportLooseZips());
            Refresh();
            ScrollToCurrent();
        }

        // Scrolls the list just far enough to show the playing game's row.
        void ScrollToCurrent()
        {
            string current = GameSelection.Current;
            var row = _rows.Find(r => r.gameObject.activeSelf && r.Installed != null && r.Info.name == current);
            if (row) ScrollTo(row);
        }

        // Scrolls the list just far enough to show row.
        void ScrollTo(GameRow row)
        {
            Canvas.ForceUpdateCanvases();
            var content = list.content;
            var rowRect = (RectTransform)row.transform;
            float viewHeight = list.viewport.rect.height;
            float top = -rowRect.anchoredPosition.y - rowRect.rect.height * (1 - rowRect.pivot.y);
            float bottom = top + rowRect.rect.height;
            float y = content.anchoredPosition.y;
            if (top < y) y = top;
            else if (bottom > y + viewHeight) y = bottom - viewHeight;
            y = Mathf.Clamp(y, 0, Mathf.Max(0, content.rect.height - viewHeight));
            list.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Round(y));
        }

        public void Close() => SetOpen(false);

        void SetOpen(bool open)
        {
            if (open == s_open && window.activeSelf == open) return;
            s_open = open;
            if (!open) { s_closedAt = Time.unscaledTime; ArOnly = false; }
            HideDelete();
            window.SetActive(open);
            if (open || !MenuDropdown.Exists) openButton.SetActive(!open);
            MobileRetroDepthLayerStack.Paused = open || SettingsScreen.IsOpen;
            if (open) MountInFrame();
            else Unmount();
            SetFocus(-1);
        }

        // ---- in AR ----

        void MountInFrame()
        {
            if (!AppSettings.PickerInAR || FullScreenTest.Enabled) return;
            var frame = FindAnyObjectByType<ScreenFrame>();
            if (!frame || !frame.stack) return;
            _arFrame = frame;
            _arCanvas.gameObject.SetActive(true);
            _arCanvas.worldCamera = Camera.main;
            var rect = (RectTransform)window.transform;
            rect.SetParent(_arCanvas.transform, false);
            Fill(rect);
            if (_backdrop) _backdrop.SetActive(false);
            if (_safeArea)
            {
                _safeArea.enabled = false;
                Fill((RectTransform)_safeArea.transform);
            }
            frame.stack.LayersVisible = false;
            FollowFrame();
        }

        void Unmount()
        {
            if (_arFrame && _arFrame.stack) _arFrame.stack.LayersVisible = true;
            _arFrame = null;
            if (window.transform.parent == _windowHome) return;
            var rect = (RectTransform)window.transform;
            rect.SetParent(_windowHome, false);
            rect.SetSiblingIndex(_windowSibling);
            Fill(rect);
            if (_backdrop) _backdrop.SetActive(true);
            if (_safeArea) _safeArea.enabled = true;
            _arCanvas.gameObject.SetActive(false);
        }

        static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        // Covers the frame's opening, a hair in front of it - or, with the CRT
        // cabinet up (CrtCabinet), lies on its tube, with scanlines over it.
        void FollowFrame()
        {
            var frame = (RectTransform)_arFrame.transform;
            var rect = (RectTransform)_arCanvas.transform;
            rect.sizeDelta = _arFrame.InnerSize;
            var crt = _arFrame.GetComponentInParent<CrtCabinet>();
            bool onTube = crt && crt.Shown;
            ScanLines(onTube);
            if (onTube)
            {
                crt.TubePose(out var position, out var rotation, out float width);
                rect.localScale = Vector3.one * (width / Mathf.Max(1f, _arFrame.InnerSize.x));
                rect.SetPositionAndRotation(position, rotation);
                return;
            }
            rect.localScale = frame.lossyScale;
            float cabinetScale = frame.parent ? frame.parent.lossyScale.x : 1f;
            // The viewer is on the frame's -Z side.
            rect.SetPositionAndRotation(frame.position - frame.forward * (0.0005f * cabinetScale), frame.rotation);
        }

        // The CRT's dark lines between rows, over the picker on the tube.
        RawImage _scanLines;
        void ScanLines(bool on)
        {
            if (on && !_scanLines)
            {
                var texture = new Texture2D(1, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                texture.SetPixels32(new[] { new Color32(0, 0, 0, 110), new Color32(0, 0, 0, 0) });
                texture.Apply();
                var go = new GameObject("CRT Lines", typeof(RectTransform));
                go.layer = _arCanvas.gameObject.layer;
                go.transform.SetParent(_arCanvas.transform, false);
                _scanLines = go.AddComponent<RawImage>();
                _scanLines.texture = texture;
                _scanLines.raycastTarget = false;
                Fill((RectTransform)go.transform);
            }
            if (!_scanLines) return;
            _scanLines.gameObject.SetActive(on);
            if (!on) return;
            _scanLines.transform.SetAsLastSibling();
            // (A dark line every two of its pixels: the texture's dark row, once a repeat.)
            _scanLines.uvRect = new Rect(0f, 0f, 1f, _arFrame.InnerSize.y / 2f);
        }

        void LateUpdate()
        {
            if (window.transform.parent == _windowHome) return;
            // The cabinet went away: carry on on the screen.
            if (!_arFrame || !_arFrame.isActiveAndEnabled) { Unmount(); return; }
            FollowFrame();
        }

        // ---- controller ----

        void UpdateController()
        {
            var pad = Gamepad.current;
            if (pad == null || !s_open || (filePicker && filePicker.IsOpen)) return;

            if (pad.buttonEast.wasPressedThisFrame) { Close(); return; }
            if (pad.buttonNorth.wasPressedThisFrame) { ToggleShowAll(); return; }
            if (pad.buttonSouth.wasPressedThisFrame)
            {
                var focused = FocusedRow();
                if (focused) OnRowTapped(focused);
                else SetFocus(CurrentRowIndex());
                return;
            }

            Vector2 stick = pad.leftStick.ReadValue();
            int dir = pad.dpad.up.isPressed || stick.y > 0.5f ? -1 : pad.dpad.down.isPressed || stick.y < -0.5f ? 1 : 0;
            if (dir == 0) { _repeatDir = 0; return; }
            if (dir == _repeatDir && Time.unscaledTime < _nextRepeat) return;
            _nextRepeat = Time.unscaledTime + (dir == _repeatDir ? repeatInterval : repeatDelay);
            _repeatDir = dir;
            int count = ActiveRowCount();
            if (count == 0) return;
            SetFocus(_focus < 0 ? CurrentRowIndex() : Mathf.Clamp(_focus + dir, 0, count - 1));
        }

        int ActiveRowCount()
        {
            int count = 0;
            foreach (var row in _rows) if (row.gameObject.activeSelf) count++;
            return count;
        }

        // The playing game's row, or the first.
        int CurrentRowIndex()
        {
            string current = GameSelection.Current;
            int index = _rows.FindIndex(r => r.gameObject.activeSelf && r.Installed != null && r.Info.name == current);
            return Mathf.Max(0, index);
        }

        GameRow FocusedRow() => _focus >= 0 && _focus < _rows.Count && _rows[_focus].gameObject.activeSelf ? _rows[_focus] : null;

        void SetFocus(int index)
        {
            var old = FocusedRow();
            if (old) old.SetFocused(false, focusTint);
            _focus = index;
            var row = FocusedRow();
            if (!row) { _focus = -1; return; }
            row.SetFocused(true, focusTint);
            ScrollTo(row);
        }

        void AddOrderButton()
        {
            var all = (RectTransform)showAllButton.transform;
            var key = (RectTransform)Instantiate(all.gameObject, all.parent, false).transform;
            key.name = "Show Heavy Vert Button";
            key.SetSiblingIndex(all.GetSiblingIndex() + 1);
            key.sizeDelta = new Vector2(50, all.sizeDelta.y);
            key.anchoredPosition = new Vector2(all.anchoredPosition.x + all.sizeDelta.x + 4, all.anchoredPosition.y);
            _orderLabel = key.GetComponentInChildren<TMP_Text>(true);
            var button = key.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                _order = _order == Order.HeavyFirst ? Order.VerticalFirst : Order.HeavyFirst;
                Refresh();
                list.verticalNormalizedPosition = 1;
            });
        }

        void ToggleShowAll()
        {
            _showAll = !_showAll;
            _order = Order.Alphabetical;   // (back to A-Z)
            Refresh();
            list.verticalNormalizedPosition = 1;
        }

        void Refresh()
        {
            HideDelete();
            var installed = RomLibrary.Scan();
            string current = GameSelection.Current;

            // Installed games, plus (show all) every main version not installed.
            var entries = new List<(CatalogGame info, LibraryGame game)>();
            var installedNames = new HashSet<string>();
            foreach (var game in installed)
            {
                if (ArOnly && game.game.FullScreenOnly) continue;
                entries.Add((game.game, game));
                installedNames.Add(game.game.name);
            }
            if (_showAll)
            {
                // Installed games stay at the top (already sorted); the rest follow, alphabetically.
                var others = new List<(CatalogGame info, LibraryGame game)>();
                foreach (var info in GameCatalog.All)
                {
                    bool mainVersion = info.parent.Length == 0 || GameCatalog.Find(info.parent) is { isBios: true };
                    if (!info.isBios && mainVersion && !installedNames.Contains(info.name) && !(ArOnly && info.FullScreenOnly))
                        others.Add((info, null));
                }
                others.Sort((a, b) => string.Compare(a.info.description, b.info.description, System.StringComparison.OrdinalIgnoreCase));
                entries.AddRange(others);
            }

            // SHOW HEAVY / SHOW VERT: those games first (each part keeps its order).
            if (_order != Order.Alphabetical)
            {
                System.Predicate<(CatalogGame info, LibraryGame game)> first = _order == Order.HeavyFirst
                    ? e => e.info.FullScreenOnly
                    : e => e.info.Vertical;
                var front = entries.FindAll(first);
                entries.RemoveAll(first);
                entries.InsertRange(0, front);
            }
            if (_orderLabel) _orderLabel.text = _order == Order.HeavyFirst ? "SHOW VERT" : "SHOW HEAVY";

            for (int i = 0; i < entries.Count; i++)
            {
                if (i == _rows.Count)
                {
                    var row = Instantiate(rowTemplate, rowTemplate.transform.parent);
                    _rows.Add(row);
                }
                var (info, game) = entries[i];
                _rows[i].gameObject.SetActive(true);
                _rows[i].Show(info, game, game != null && info.name == current, this, OnRowTapped, OnRowHeld, OnRowDelete);
            }
            for (int i = entries.Count; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetFocused(i == _focus, focusTint);
            if (_focus >= entries.Count) SetFocus(entries.Count - 1);

            emptyState.SetActive(entries.Count == 0);
            string count = installed.Count == 1 ? "1 game" : $"{installed.Count} games";
            countLabel.text = _showAll ? $"{count} of {entries.Count}" : count;
            showAllLabel.text = _showAll ? "MY GAMES" : "SHOW ALL";
        }

        void OnRowTapped(GameRow row)
        {
            if (_swallowTap) { _swallowTap = false; return; }
            if (_deleteRow) { HideDelete(); return; }
            var game = row.Installed;
            if (game == null)
            {
                ShowToast($"Not installed. Add {row.Info.name}.zip with ADD ROM to play it", textColor);
                return;
            }
            switch (game.status)
            {
                case RomStatus.Ready:
                    // Picked for AR: out of full screen, to place a cabinet for it.
                    if (ArOnly) FullScreenTest.ReturnToAR();
                    // Too heavy for AR: straight into full screen.
                    else if (game.game.FullScreenOnly && !FullScreenTest.Enabled) FullScreenTest.EnterWithCabinet();
                    // Tapping the game that's already on just goes back to it.
                    if (game.game.name != GameSelection.Current || !(LibretroCore.IsRunning || Demo.PitDemoGame.IsRunning))
                        GameSelection.Select(game.game.name);
                    Close();
                    break;
                case RomStatus.NeedsBios:
                    ShowToast($"{game.game.BoardLabel} games need the {game.game.BiosLabel}: add {game.game.Bios}.zip with ADD ROM", warningColor);
                    break;
                case RomStatus.NeedsQSound:
                    ShowToast("CPS2 games need the QSound sound chip: add qsound.zip (or qsound_hle.zip) once with ADD ROM", warningColor);
                    break;
                case RomStatus.NeedsParent:
                    ShowToast($"This version shares files with {game.game.parent}.zip: add that romset too", warningColor);
                    break;
                case RomStatus.Incomplete:
                    ShowToast("This romset is missing files MAME needs. Try a complete (non-merged) set", errorColor);
                    break;
            }
        }

        void OnRowHeld(GameRow row)
        {
            HideDelete();
            if (row.Info.board == "demo") return; // built in: nothing to delete
            _deleteRow = row;
            _deleteScrollY = list.content.anchoredPosition.y;
            row.SetDeleteVisible(true);
        }

        void OnRowDelete(GameRow row)
        {
            var info = row.Info;
            HideDelete();
            if (RomLibrary.Delete(info.name))
                ShowToast($"Deleted {info.Title} ({info.name}.zip)", textColor);
            else
                ShowToast($"Couldn't delete {info.name}.zip", errorColor);
            Refresh();
        }

        void HideDelete()
        {
            if (_deleteRow) _deleteRow.SetDeleteVisible(false);
            _deleteRow = null;
        }

        // A press anywhere except the Delete button puts it away.
        // (Update runs whether or not a Delete button is showing, to expire _swallowTap.)
        SingleCabinetGate _gate;

        void Update()
        {
            UpdateController();
            // The screen's GAMES key: before a cabinet's placed and in full
            // screen; a placed cabinet has its own (GamesPanel).
            if (!_gate) _gate = FindAnyObjectByType<SingleCabinetGate>();
            bool screenKey = !s_open && !MenuDropdown.Exists && (FullScreenTest.Enabled || !(_gate && _gate.cabinet));
            if (openButton.activeSelf != screenKey) openButton.SetActive(screenKey);
            var pointer = Pointer.current;
            if (pointer == null) return;
            if (_swallowTap)
            {
                // The release didn't land on a row: nothing left to swallow.
                if (pointer.press.wasReleasedThisFrame) _releaseFrame = Time.frameCount;
                else if (!pointer.press.isPressed && Time.frameCount > _releaseFrame + 1) _swallowTap = false;
            }
            if (!_deleteRow) return;
            // Scrolling the list puts it away too.
            if (Mathf.Abs(list.content.anchoredPosition.y - _deleteScrollY) > 2) { HideDelete(); return; }
            if (!pointer.press.wasPressedThisFrame || !EventSystem.current) return;
            var data = new PointerEventData(EventSystem.current) { position = pointer.position.ReadValue() };
            EventSystem.current.RaycastAll(data, _hits);
            var deleteButton = _deleteRow.deleteButton.transform;
            foreach (var hit in _hits)
                if (hit.gameObject.transform.IsChildOf(deleteButton)) return;
            HideDelete();
            _swallowTap = true;
            _releaseFrame = int.MaxValue;
        }

        void AddRom()
        {
            if (!filePicker || filePicker.IsOpen) return;
            filePicker.PickZips(paths =>
            {
                if (paths.Length == 0) return;
                StartCoroutine(ImportBehindCurtain(paths, deleteSource: !Application.isEditor, skipExisting: false, ReportImports));
            });
        }

        // SCAN FOLDER, beside ADD ROM (the pair centred under the list): a
        // copy of ADD ROM's key, wider for its label.
        Button _scanButton;
        bool _scanning;

        void AddScanButton()
        {
            var add = (RectTransform)addRomButton.transform;
            var scan = (RectTransform)Instantiate(add.gameObject, add.parent, false).transform;
            scan.name = "Scan Folder Button";
            scan.SetSiblingIndex(add.GetSiblingIndex() + 1);
            var label = scan.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = "SCAN FOLDER";
            const float gap = 6f, scanWidth = 84f;
            float addWidth = add.sizeDelta.x, left = -(addWidth + gap + scanWidth) * 0.5f;
            scan.sizeDelta = new Vector2(scanWidth, add.sizeDelta.y);
            add.anchoredPosition = new Vector2(left + addWidth * 0.5f, add.anchoredPosition.y);
            scan.anchoredPosition = new Vector2(left + addWidth + gap + scanWidth * 0.5f, add.anchoredPosition.y);
            _scanButton = scan.GetComponent<Button>();
            _scanButton.onClick.AddListener(ScanFolder);
            var empty = emptyState ? emptyState.GetComponent<TMP_Text>() : null;
            if (empty) empty.text = empty.text.Replace("from the Files app", "from the Files app, or SCAN FOLDER to add every game in a folder at once");
        }

        // Every zip in a folder (and its subfolders) that's a game on the
        // list, or a support set, copied into the library - the player's
        // own files left where they are; ones already added skipped. A zip
        // a frame, the count showing as it goes; then what came of it.
        void ScanFolder()
        {
            if (!filePicker || filePicker.IsOpen || _scanning) return;
            filePicker.PickFolder((paths, inCloud) =>
            {
                if (paths.Length == 0 && inCloud == 0) { filePicker.EndFolderAccess(); return; }
                StartCoroutine(Scan(paths, inCloud));
            });
        }

        IEnumerator Scan(string[] paths, int inCloud)
        {
            yield return ImportBehindCurtain(paths, deleteSource: false, skipExisting: true, results => Report(results, paths.Length, inCloud));
        }

        void Report(List<RomLibrary.ImportResult> results, int zips, int inCloud)
        {
            filePicker.EndFolderAccess();
            int added = 0, already = 0, notGames = 0, failed = 0;
            string lastFailure = null;
            foreach (var result in results)
            {
                if (result.ok && result.already) already++;
                else if (result.ok) added++;
                else if (result.unsupported) notGames++;
                else { failed++; lastFailure = result.message; }
            }

            var parts = new List<string>();
            parts.Add(added == 1 ? "Added 1 game" : $"Added {added} games");
            if (already > 0) parts.Add($"{already} already added");
            if (notGames > 0) parts.Add($"{notGames} not on the list");
            if (failed > 0) parts.Add(failed == 1 ? lastFailure : $"{failed} couldn't be copied");
            if (inCloud > 0) parts.Add($"{inCloud} still in iCloud - downloading, scan again in a bit");
            string summary = zips == 0 && inCloud == 0 ? "No zip files in that folder" : string.Join(" · ", parts);
            if (added > 0)
            {
                // What the new ones still need, if anything (the rows show it too).
                var bios = new List<string>();
                bool qsound = false, parent = false;
                foreach (var game in RomLibrary.Scan())
                {
                    if (game.status == RomStatus.NeedsBios && !bios.Contains(game.game.Bios)) bios.Add(game.game.Bios);
                    qsound |= game.status == RomStatus.NeedsQSound;
                    parent |= game.status == RomStatus.NeedsParent;
                }
                foreach (string zip in bios)
                    summary += $". {GameCatalog.Find(zip)?.BoardLabel ?? zip} games also need {zip}.zip";
                if (qsound) summary += ". CPS2 games also need qsound.zip";
                if (parent) summary += ". Some need their parent game's zip too";
            }
            ShowToast(summary, failed > 0 ? errorColor : textColor);
        }

        // Imports a batch with the screen covered (the room's camera would
        // stutter while big zips copy): a black curtain fades up with a count
        // and a bar, the zips copy off the main thread, then it fades away
        // and `done` (called before it lifts) reports what came of it.
        IEnumerator ImportBehindCurtain(string[] paths, bool deleteSource, bool skipExisting, System.Action<List<RomLibrary.ImportResult>> done)
        {
            _scanning = true;
            var curtain = Curtain(out var label, out var bar);
            const float fade = 0.25f;
            for (float t = 0; t < fade; t += Time.unscaledDeltaTime) { curtain.alpha = t / fade; yield return null; }
            curtain.alpha = 1f;

            _ = RomLibrary.RomsDir;   // (known before the worker needs it)
            var results = new RomLibrary.ImportResult[paths.Length];
            int finished = 0;
            var work = System.Threading.Tasks.Task.Run(() =>
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    results[i] = RomLibrary.Import(paths[i], deleteSource, skipExisting);
                    System.Threading.Interlocked.Increment(ref finished);
                }
            });
            while (!work.IsCompleted)
            {
                int n = System.Threading.Volatile.Read(ref finished);
                label.text = $"IMPORTING GAMES\n\n{n} / {paths.Length}";
                bar.anchorMax = new Vector2(paths.Length > 0 ? n / (float)paths.Length : 1f, 1f);
                yield return null;
            }
            label.text = $"IMPORTING GAMES\n\n{paths.Length} / {paths.Length}";
            bar.anchorMax = Vector2.one;
            if (work.IsFaulted) Debug.LogError($"[GamePicker] import failed: {work.Exception}");

            // (The list and the report worked out while it's still dark.)
            Refresh();
            var list = new List<RomLibrary.ImportResult>();
            foreach (var result in results) if (result.message != null) list.Add(result);
            done(list);
            yield return null;
            for (float t = 0; t < fade; t += Time.unscaledDeltaTime) { curtain.alpha = 1f - t / fade; yield return null; }
            Destroy(curtain.gameObject);
            _scanning = false;
        }

        // A black screen over everything (touches too), in the picker's font.
        CanvasGroup Curtain(out TMP_Text label, out RectTransform bar)
        {
            var go = new GameObject("Import Curtain", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = go.AddComponent<CanvasScaler>();
            var home = toastLabel.canvas ? toastLabel.canvas.rootCanvas.GetComponent<CanvasScaler>() : null;
            if (home)
            {
                scaler.uiScaleMode = home.uiScaleMode;
                scaler.referenceResolution = home.referenceResolution;
                scaler.screenMatchMode = home.screenMatchMode;
                scaler.matchWidthOrHeight = home.matchWidthOrHeight;
                scaler.scaleFactor = home.scaleFactor;
                scaler.referencePixelsPerUnit = home.referencePixelsPerUnit;
            }
            go.AddComponent<GraphicRaycaster>();
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var back = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            back.SetParent(go.transform, false);
            back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one; back.offsetMin = back.offsetMax = Vector2.zero;
            back.GetComponent<Image>().color = Color.black;

            label = Instantiate(toastLabel, go.transform, false);
            label.name = "Label";
            label.alignment = TextAlignmentOptions.Center;
            label.color = textColor;
            label.text = "IMPORTING GAMES";
            var lr = label.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(220, 60);
            lr.anchoredPosition = new Vector2(0, 14);

            var track = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            track.SetParent(go.transform, false);
            track.anchorMin = track.anchorMax = track.pivot = new Vector2(0.5f, 0.5f);
            track.sizeDelta = new Vector2(120, 4);
            track.anchoredPosition = new Vector2(0, -24);
            track.GetComponent<Image>().color = new Color(textColor.r, textColor.g, textColor.b, 0.25f);
            bar = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            bar.SetParent(track, false);
            bar.anchorMin = Vector2.zero; bar.anchorMax = new Vector2(0f, 1f); bar.offsetMin = bar.offsetMax = Vector2.zero;
            bar.GetComponent<Image>().color = textColor;
            return group;
        }

        void ReportImports(List<RomLibrary.ImportResult> results)
        {
            if (results.Count == 0) return;
            int added = 0;
            string lastMessage = null;
            RomLibrary.ImportResult? failure = null;
            foreach (var result in results)
            {
                if (result.ok) { added++; lastMessage = result.message; }
                else failure = result;
            }
            if (failure.HasValue)
                ShowToast(failure.Value.message, errorColor);
            else if (added == 1)
                ShowToast(lastMessage, textColor);
            else
                ShowToast($"Added {added} romsets", textColor);
            if (s_open) Refresh();
        }

        void OnControllerChanged(string controller)
            => ShowToast(controller != null ? $"Controller connected: {controller}" : "Controller disconnected", textColor);

        /// Shows a message in the picker's pop-up (also when it's closed).
        public void ShowMessage(string message, bool error = false) => ShowToast(message, error ? errorColor : textColor);

        void OnLoadFailed(string name, string message)
        {
            var game = GameCatalog.Find(name);
            SetOpen(true);
            Refresh();
            ShowToast($"{message}: {(game != null ? game.Title : name)}", errorColor);
        }

        void ShowToast(string message, Color color)
        {
            if (string.IsNullOrEmpty(message)) return;
            toastLabel.text = message;
            toastLabel.color = color;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            toast.gameObject.SetActive(true);
            toast.alpha = 1;
            yield return new WaitForSecondsRealtime(toastSeconds);
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                toast.alpha = 1 - t / 0.4f;
                yield return null;
            }
            toast.alpha = 0;
            toast.gameObject.SetActive(false);
            _toastRoutine = null;
        }
    }
}
