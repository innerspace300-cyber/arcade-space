// PlayerSelect.cs — the "choose your player" window for games where the
// coin slot picks your character (PlayerSlots): opened by holding COIN, one
// keycap per player ("P2 HOMER"); tapping one makes the controls play as
// that player. Switching mid-game (see PlayerSlots): if your player is out
// (dead, on the continue countdown) you just join as the new one; if they're
// still playing, the keycap asks "SURE? RESTARTS" and a second tap restarts
// the game as the new player. Games whose player flags aren't known ask
// JOIN or RESTART instead. Built by GamePickerBuilder, same style as Settings.

using SpatialEmulator.Games;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class PlayerSelect : MonoBehaviour
    {
        static PlayerSelect s_instance;

        public static bool IsOpen => s_instance && s_instance.window.activeSelf;

        public GameObject window;
        public Button backdropButton;
        public Button closeButton;
        public Button[] playerButtons;
        public TMP_Text[] playerLabels;
        public GameObject choiceGroup;
        public TMP_Text choiceText;
        public Button joinButton;
        public Button restartButton;
        public Color labelColor = Color.white;
        public Color chosenColor = new Color32(0, 220, 255, 255);

        [Tooltip("Height the JOIN / RESTART question adds to the window; the window is that much shorter while it's hidden.")]
        public float choiceHeight = 44f;

        int _armedSlot = -1;
        RectTransform _panel;
        float _fullHeight;

        void Awake()
        {
            s_instance = this;
            window.SetActive(false);
            _panel = (RectTransform)choiceGroup.transform.parent;
            _fullHeight = _panel.sizeDelta.y;
            closeButton.onClick.AddListener(Close);
            backdropButton.onClick.AddListener(Close);
            for (int i = 0; i < playerButtons.Length; i++)
            {
                int slot = i;
                playerButtons[i].onClick.AddListener(() => Choose(slot));
            }
            joinButton.onClick.AddListener(() => Join(_armedSlot));
            restartButton.onClick.AddListener(() => RestartAs(_armedSlot));
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        /// Opens the chooser if the game picks players by coin slot.
        public static void Open()
        {
            if (!s_instance || PlayerSlots.Current == null) return;
            s_instance.Show();
        }

        void Show()
        {
            _armedSlot = -1;
            SetChoiceVisible(false);
            var names = PlayerSlots.Current;
            int chosen = PlayerSlots.CurrentSlot;
            for (int i = 0; i < playerButtons.Length; i++)
            {
                bool used = i < names.Length;
                playerButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                playerLabels[i].text = (i == chosen ? "> " : "") + $"P{i + 1} {names[i].ToUpperInvariant()}";
                playerLabels[i].color = i == chosen ? chosenColor : labelColor;
            }
            window.SetActive(true);
        }

        public void Close() => window.SetActive(false);

        void Choose(int slot)
        {
            if (slot == PlayerSlots.CurrentSlot) { Close(); return; }
            bool? stillIn = PlayerSlots.IsInGame(PlayerSlots.CurrentSlot);
            if (stillIn == false || (stillIn == null && !PlayerSlots.GameInProgress)) { Join(slot); return; }
            if (stillIn == true)
            {
                // Your player is still playing: switching means starting over.
                if (_armedSlot == slot) { RestartAs(slot); return; }
                Show();
                _armedSlot = slot;
                playerLabels[slot].text = "SURE? RESTARTS";
                return;
            }
            // Flags unknown and the game's under way: ask.
            Show();
            _armedSlot = slot;
            playerLabels[slot].text = $"> P{slot + 1} {PlayerSlots.Current[slot].ToUpperInvariant()}?";
            choiceText.text = $"P{PlayerSlots.CurrentSlot + 1} out? JOIN. Or RESTART";
            SetChoiceVisible(true);
        }

        // The window only has room for the question while it's showing.
        void SetChoiceVisible(bool visible)
        {
            choiceGroup.SetActive(visible);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, visible ? _fullHeight : _fullHeight - choiceHeight);
        }

        void Join(int slot)
        {
            if (slot < 0) return;
            PlayerSlots.SetCurrentSlot(slot);
            Close();
            Message($"You're {PlayerSlots.Describe(slot)}. Insert a coin to join");
        }

        void RestartAs(int slot)
        {
            if (slot < 0) return;
            PlayerSlots.RestartAs(slot);
            Close();
            Message($"Restarted as {PlayerSlots.Describe(slot)}. Insert a coin to start");
        }

        static void Message(string text)
        {
            var picker = FindAnyObjectByType<GamePicker>();
            if (picker) picker.ShowMessage(text);
        }
    }
}
