// GameRow.cs — one game in the picker list: title, year/maker/board/zip
// name, and a status line when it's playing or can't run yet. Games that
// aren't installed (the "show all" list) are drawn dimmed. Holding an
// installed game shows its Delete button.

using System;
using SpatialEmulator.Games;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class GameRow : MonoBehaviour
    {
        public Button button;
        public HoldGesture hold;
        public CanvasGroup group;
        public Image background;
        public Image chevron;
        public TMP_Text title;
        public TMP_Text details;
        public TMP_Text status;
        public Button deleteButton;

        [Tooltip("Opacity of a game that isn't installed.")]
        public float notInstalledAlpha = 0.45f;

        public CatalogGame Info { get; private set; }

        /// The installed romset, or null for a game from the full catalog.
        public LibraryGame Installed { get; private set; }

        const string VerticalTagColor = "54C8E8";
        const string HeavyTagColor = "E8A854";

        public void Show(CatalogGame info, LibraryGame installed, bool isCurrent, GamePicker picker,
            Action<GameRow> onTap, Action<GameRow> onHold, Action<GameRow> onDelete)
        {
            Info = info;
            Installed = installed;
            bool ready = installed != null && installed.status == RomStatus.Ready;

            group.alpha = installed != null ? 1 : notInstalledAlpha;
            background.sprite = isCurrent ? picker.rowCurrentSprite : picker.rowSprite;
            chevron.enabled = isCurrent;
            // Heavy (full screen only) and vertical games tagged, so they stand out in the list.
            string tags = (info.FullScreenOnly ? $"<color=#{HeavyTagColor}>HEAVY</color> " : "")
                + (info.Vertical ? $"<color=#{VerticalTagColor}>VERT</color> " : "");
            title.text = tags + info.Title;
            title.color = ready || installed == null ? picker.textColor : picker.dimTextColor;

            string version = info.Version;
            details.text = info.board == "demo"
                ? $"{info.year} • {info.company} • BUILT IN, NO ROM NEEDED"
                : $"{info.year} • {info.company} • {info.BoardLabel} • {info.name}.zip"
                  + (version.Length > 0 ? $"\n{version}" : "");

            string line = null;
            Color color = picker.textColor;
            switch (installed?.status)
            {
                case RomStatus.Ready:
                    if (isCurrent) line = "Playing";
                    break;
                case RomStatus.NeedsBios:
                    line = $"Needs {info.Bios}.zip ({info.BiosLabel})";
                    color = picker.warningColor;
                    break;
                case RomStatus.NeedsQSound:
                    line = "Needs qsound.zip (QSound sound chip)";
                    color = picker.warningColor;
                    break;
                case RomStatus.NeedsParent:
                    line = $"Needs its parent romset {info.parent}.zip";
                    color = picker.warningColor;
                    break;
                case RomStatus.Incomplete:
                    line = installed.missingFiles == 1 ? "Incomplete romset: 1 file missing" : $"Incomplete romset: {installed.missingFiles} files missing";
                    color = picker.errorColor;
                    break;
            }
            if (line == null && info.notWorking)
            {
                line = "Marked not working in MAME";
                color = picker.warningColor;
            }
            status.gameObject.SetActive(line != null);
            if (line != null)
            {
                status.text = line;
                status.color = color;
            }

            SetDeleteVisible(false);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (!hold.ConsumeHold()) onTap(this);
            });
            hold.onHold -= OnHoldFired;
            _onHold = onHold;
            hold.onHold += OnHoldFired;
            hold.enabled = installed != null;
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDelete(this));
        }

        Action<GameRow> _onHold;

        void OnHoldFired() => _onHold?.Invoke(this);

        /// Highlights the row the controller is on.
        public void SetFocused(bool focused, Color tint) => background.color = focused ? tint : Color.white;

        public void SetDeleteVisible(bool visible)
        {
            if (deleteButton.gameObject.activeSelf != visible)
                deleteButton.gameObject.SetActive(visible);
        }
    }
}
