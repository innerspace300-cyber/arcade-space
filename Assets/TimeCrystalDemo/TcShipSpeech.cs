using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TimeCrystal
{
    // A text box from the player's ship: a dark pixel panel edged in the pilot's colour,
    // their name on a tab at its top left, a tail down to the ship, the line typed out
    // in the HUD's font - following the ship about, then fading. (After each TIME CRYSTAL:
    // TcStageDirector.CrystalFinale.)
    public static class TcShipSpeech
    {
        // Canvas units (the HUD's 1920 x 2560 over the field, (0, 0) the middle).
        const float Width = 1500f, Pad = 60f, Px = 3.2f * 3f;   // (Px: the border, three picture pixels)
        const float Cps = 26f, Hold = 2.4f, Fade = 0.3f;

        public static IEnumerator Say(TcStageDirector director, string text)
        {
            var line = director ? director.MakeLine("SHIP SPEECH LINE", 0.5f, 1.15f) : null;
            if (!line) yield break;
            var canvas = (RectTransform)line.transform.parent;
            var pilot = TcPilots.Current;

            var box = new GameObject("SHIP SPEECH", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            box.SetParent(canvas, false);
            box.SetAsLastSibling();
            var sorting = box.gameObject.AddComponent<Canvas>();
            sorting.overrideSorting = true;
            sorting.sortingOrder = 31000;
            var group = box.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            // The line: fitted to the box's width, left to wrap; its height sets the box's.
            float k = Mathf.Max(0.001f, line.transform.localScale.x);
            line.transform.SetParent(box, false);
            line.textWrappingMode = TextWrappingModes.Normal;
            line.alignment = TextAlignmentOptions.Center;
            line.color = Color.white;
            line.lineSpacing = 6f;
            var lr = (RectTransform)line.transform;
            lr.sizeDelta = new Vector2((Width - Pad * 2f) / k, 2000f);
            line.text = text;
            line.ForceMeshUpdate();
            float height = line.preferredHeight * k + Pad * 2f;
            lr.sizeDelta = new Vector2(lr.sizeDelta.x, line.preferredHeight);
            lr.anchoredPosition = Vector2.zero;
            line.maxVisibleCharacters = 0;

            var edge = pilot.Color;
            Image Block(string name, Color color, Vector2 size, Vector2 at)
            {
                var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(box, false);
                image.sprite = TcArt.Pixel;
                image.color = color;
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = size;
                image.rectTransform.anchoredPosition = at;
                return image;
            }
            // Border, then the fill inside it (a pixel step at the corners), then the tail: stepped pixel blocks.
            var size = new Vector2(Width, height);
            Block("EDGE H", edge, size + new Vector2(0f, Px * 2f) - new Vector2(Px * 2f, 0f), Vector2.zero);
            Block("EDGE V", edge, size + new Vector2(Px * 2f, 0f) - new Vector2(0f, Px * 2f), Vector2.zero);
            Block("FILL", new Color(0.01f, 0.015f, 0.05f, 0.96f), size - new Vector2(Px * 2f, Px * 2f), Vector2.zero);
            float bottom = -height * 0.5f - Px;
            for (int i = 0; i < 4; i++)
            {
                float w = Px * (7 - i * 2);
                Block("TAIL EDGE " + i, edge, new Vector2(w + Px * 2f, Px * 2f), new Vector2(0f, bottom - Px * (2 * i + 0.5f)));
                if (i < 3) Block("TAIL " + i, new Color(0.01f, 0.015f, 0.05f, 0.96f), new Vector2(w, Px * 2f), new Vector2(0f, bottom - Px * (2 * i + 0.5f) + Px));
            }
            // The pilot's name on a tab over the top left corner.
            var tag = director.MakeLine("SHIP SPEECH NAME", 0.5f, 0.75f);
            if (tag)
            {
                float tk = Mathf.Max(0.001f, tag.transform.localScale.x);
                tag.text = pilot.Name;
                tag.color = Color.black;
                tag.textWrappingMode = TextWrappingModes.NoWrap;
                tag.ForceMeshUpdate();
                var tagSize = new Vector2(tag.preferredWidth * tk + 44f, tag.preferredHeight * tk + 14f);
                var tagAt = new Vector2(-Width * 0.5f + Pad + tagSize.x * 0.5f, height * 0.5f + Px);
                Block("NAME TAB", edge, tagSize, tagAt);
                tag.transform.SetParent(box, false);
                tag.transform.SetAsLastSibling();
                ((RectTransform)tag.transform).anchoredPosition = tagAt;
            }
            line.transform.SetAsLastSibling();
            // (On the HUD's layer, which only its own camera draws.)
            foreach (var piece in box.GetComponentsInChildren<Transform>(true)) piece.gameObject.layer = canvas.gameObject.layer;

            // Where the ship is, in canvas units: the box over it (its tail's tip just above the ship), kept in the picture.
            float lift = height * 0.5f + Px * 9f + 150f;
            void Follow()
            {
                var f = TcWorld.Field;
                var ship = TcPlayerShip.Instance ? (Vector2)TcPlayerShip.Instance.transform.position : f.center;
                var at = new Vector2((ship.x - f.center.x) / f.width * 1920f, (ship.y - f.center.y) / f.height * 2560f + lift);
                at.x = Mathf.Clamp(at.x, -960f + Width * 0.5f + 30f, 960f - Width * 0.5f - 30f);
                at.y = Mathf.Min(at.y, 1280f - 330f - height * 0.5f);   // (under the scores)
                box.anchoredPosition = at;
            }

            // In: a quick pop, then typed out with a blip, held, faded.
            int total = line.textInfo.characterCount;
            float t = 0f, typed = 0f, endAt = -1f;
            int shown = 0;
            while (true)
            {
                t += Time.deltaTime;
                Follow();
                float pop = Mathf.Clamp01(t / 0.15f);
                box.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, 1f - (1f - pop) * (1f - pop));
                if (t > 0.15f && shown < total)
                {
                    typed += Cps * Time.deltaTime;
                    int now = Mathf.Min(total, Mathf.FloorToInt(typed));
                    if (now != shown && now % 2 == 0 && now <= text.Length && !char.IsWhiteSpace(text[now - 1])) director.Beep(1.5f + 0.1f * (now % 3));
                    shown = now;
                    line.maxVisibleCharacters = shown;
                    if (shown >= total) endAt = t + Hold;
                }
                if (endAt > 0f)
                {
                    group.alpha = Mathf.Clamp01((endAt + Fade - t) / Fade);
                    if (t >= endAt + Fade) break;
                }
                yield return null;
            }
            Object.Destroy(box.gameObject);
        }
    }
}
