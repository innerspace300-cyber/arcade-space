// HudStyle.cs — ENDLESS KNIGHT's HUD in the cabinet's style (Settings >
// COLORS; CabinetStyles): the band's border and the score panel's (their
// light line recoloured, Resources/CabinetStyles/HudBandBorder<style>), the
// dividers' and title lines' light side, the corner ornaments and the SCORE /
// HI SCORE titles. CLASSIC is the HUD as built (EndlessKnightBuilder).
// Added by PitDemoGame; restyles when the style changes.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.Demo
{
    public class HudStyle : MonoBehaviour
    {
        // Per style (CabinetStyles' order): the line work, the corner ornaments, the titles.
        static readonly Color[] Lines =
        {
            new Color32(182, 151, 239, 255), new Color32(142, 244, 255, 255), new Color32(255, 122, 24, 255), new Color32(57, 255, 60, 255),
        };
        static readonly Color[] Corners =
        {
            new Color(184 / 255f, 160 / 255f, 240 / 255f, 0.9f), new Color(1f, 46 / 255f, 200 / 255f, 0.9f),
            new Color(1f, 176 / 255f, 0f, 0.9f), new Color(200 / 255f, 1f, 48 / 255f, 0.9f),
        };
        static readonly Color[] Titles =
        {
            new Color32(184, 160, 240, 255), new Color32(142, 244, 255, 255), new Color32(255, 170, 100, 255), new Color32(140, 255, 140, 255),
        };

        Transform _accents, _texts;

        void Start()
        {
            var canvas = transform.Find("Responsive Health Bar ARK/Canvas");
            if (!canvas) return;
            _accents = FindDeep(canvas, "HUD ACCENTS");
            _texts = FindDeep(canvas, "HUD TEXT");
            Restyle();
            UI.CabinetStyles.Changed += Restyle;
        }

        void OnDestroy() => UI.CabinetStyles.Changed -= Restyle;

        void Restyle()
        {
            int style = UI.CabinetStyles.Current;
            if (_accents)
            {
                var border = Resources.Load<Sprite>("CabinetStyles/HudBandBorder" + UI.CabinetStyles.ArtSuffix);
                foreach (var image in _accents.GetComponentsInChildren<Image>(true))
                {
                    string n = image.name;
                    if ((n == "Band Border" || n == "Score Panel") && border) image.sprite = border;
                    else if (n.StartsWith("Corner ")) image.color = Corners[style];
                    else if (n.EndsWith(" Light") || n == "Score Divider Left" || n == "Score Divider Right") image.color = Lines[style];
                }
            }
            if (_texts)
                foreach (var text in _texts.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name == "Score Title" || text.name == "Hi Score Title") text.color = Titles[style];
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
