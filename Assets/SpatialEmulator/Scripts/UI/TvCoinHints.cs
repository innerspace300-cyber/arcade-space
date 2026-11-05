// TvCoinHints.cs — on a TV with a controller (TvOutput.MenusOnTv), in a game that takes
// its coin and start on the shoulders (GamepadInput.CoinOnShoulders: every arcade game,
// not the built-in demos - nothing on screen says so otherwise): PRESS L1 TO INSERT COIN under the three bars
// (top left, MenuDropdown) and PRESS R1 TO START under the Settings gear (top right), out in
// the black either side of the picture. In a game where the coin slot picks the player (PlayerSlots: the
// 4-player cabinets) HOLD L1 TO SELECT PLAYER goes under the coin hint (GamepadInput.ShoulderCoin). Hidden while a menu's open (it drops down there).
// Made at run time, on the game picker's canvas with the keys.

using SpatialEmulator.Controls;
using SpatialEmulator.Games;
using TMPro;
using UnityEngine;

namespace SpatialEmulator.UI
{
    public class TvCoinHints : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.18f);
        const float Gap = 4f;   // (art pixels under the key)
        static string CoinHint => $"PRESS {PadLabels.Coin}\nTO INSERT COIN";
        static string CoinAndPlayerHint => $"{CoinHint}\n\nHOLD {PadLabels.Coin}\nTO SELECT PLAYER";

        GamePicker _picker;
        SettingsScreen _settings;
        RectTransform _menu;
        TextMeshProUGUI _coin, _start;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var picker = FindAnyObjectByType<GamePicker>(FindObjectsInactive.Include);
            if (picker) picker.gameObject.AddComponent<TvCoinHints>()._picker = picker;
        }

        void LateUpdate()
        {
            bool show = TvOutput.MenusOnTv && GamepadInput.CoinOnShoulders
                && !MenuDropdown.IsShowing && !SettingsScreen.IsOpen && !GamePicker.IsOpen;
            if (show && !_coin && !Build()) return;
            if (!_coin) return;
            if (_coin.gameObject.activeSelf != show) { _coin.gameObject.SetActive(show); _start.gameObject.SetActive(show); }
            if (!show) return;
            _coin.text = PlayerSlots.Current != null ? CoinAndPlayerHint : CoinHint;
            _start.text = $"PRESS {PadLabels.Start}\nTO START";
            // Under the three bars, its left edge with theirs; under the gear, its right edge with the gear's.
            var bars = Bottom(_menu);
            _coin.rectTransform.anchoredPosition = new Vector2(bars.x - _menu.rect.width * 0.5f, bars.y - Gap);
            if (_settings && _settings.openButton)
            {
                var gear = (RectTransform)_settings.openButton.transform;
                var under = Bottom(gear);
                _start.rectTransform.anchoredPosition = new Vector2(under.x + gear.rect.width * 0.5f, under.y - Gap);
            }
        }

        // The key's bottom middle, in its parent's space from the top left (its anchors are top left / top right).
        static Vector2 Bottom(RectTransform key)
        {
            var parent = (RectTransform)key.parent;
            var corners = new Vector3[4];
            key.GetWorldCorners(corners);
            Vector2 bottomMiddle = parent.InverseTransformPoint((corners[0] + corners[3]) * 0.5f);
            return bottomMiddle - new Vector2(parent.rect.xMin, parent.rect.yMax);   // (from the parent's top left)
        }

        bool Build()
        {
            if (!_picker || !_picker.openButton) return false;
            var root = _picker.openButton.transform.parent;
            _menu = root.Find("Menu Button") as RectTransform;
            if (!_menu) return false;   // (MenuDropdown not made yet)
            _settings = FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            var like = _picker.openButton.GetComponentInChildren<TMP_Text>(true);
            _coin = Label(root, "TV Coin Hint", CoinHint, like, false);
            _start = Label(root, "TV Start Hint", "PRESS R1\nTO START", like, true);
            return true;
        }

        static TextMeshProUGUI Label(Transform root, string name, string text, TMP_Text like, bool right)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = root.gameObject.layer;
            go.transform.SetParent(root, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (like) { label.font = like.font; label.fontSize = like.fontSize; }
            label.text = text;
            label.color = Gold;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = right ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);   // (placed from the top left: LateUpdate)
            rect.pivot = new Vector2(right ? 1f : 0f, 1f);
            rect.sizeDelta = new Vector2(120f, 30f);
            go.SetActive(false);
            return label;
        }
    }
}
