// ControlColorsWindow.cs — Settings > COLORS: the on-screen controls'
// colours (ControlColors) and the cabinet's frame (CabinetStyles). Three
// sections in the settings rows' style - the JOYSTICK's knob, the BUTTONS
// and the CABINET - each a grid of the choices drawn as the thing itself
// (the frame by its crest); tapping one puts it on the controls at once, and the
// chosen one has a gold frame. Built at run time from the ABOUT window (its
// panel, banner and close button) and the settings rows, next to ABOUT.

using SpatialEmulator.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class ControlColorsWindow : MonoBehaviour
    {
        // The controls three to a row; the cabinet's crests, smaller, four.
        static readonly Vector2 Cell = new Vector2(46f, 34f), CrestCell = new Vector2(34f, 22f);

        GameObject _window;
        // One per section: its choices' names and colours, which is chosen, and its marks and name line.
        class Choice
        {
            public string[] names;
            public Color[] colors;
            public System.Func<int> current;
            public GameObject[] marks;
            public TMP_Text chosen;
        }
        readonly System.Collections.Generic.List<Choice> _choices = new System.Collections.Generic.List<Choice>();

        /// The COLORS button beside ABOUT and its window.
        public static void Build(SettingsScreen settings)
        {
            if (!settings.aboutButton || !settings.aboutWindow) return;
            var host = settings.gameObject.AddComponent<ControlColorsWindow>();
            host.BuildWindow(settings);
        }

        void BuildWindow(SettingsScreen settings)
        {
            ControlColors.Apply();   // (and notes the controls' own art)

            // ABOUT and COLORS side by side under the rows.
            var about = (RectTransform)settings.aboutButton.transform;
            var open = Instantiate(about.gameObject, about.parent).GetComponent<RectTransform>();
            open.name = "Colors Button";
            float half = about.sizeDelta.x * 0.5f + 4f;
            about.anchoredPosition += new Vector2(-half, 0f);
            open.anchoredPosition = about.anchoredPosition + new Vector2(half * 2f, 0f);
            var openButton = open.GetComponent<Button>();
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Open);
            var openLabel = open.GetComponentInChildren<TMP_Text>(true);
            if (openLabel) openLabel.text = "COLORS";

            // The window: ABOUT's, emptied.
            _window = Instantiate(settings.aboutWindow, settings.aboutWindow.transform.parent);
            _window.name = "Colors Window";
            var area = (RectTransform)_window.transform.Find("Text");
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(_window.transform, false);
            if (area)
            {
                content.anchorMin = area.anchorMin; content.anchorMax = area.anchorMax;
                content.offsetMin = area.offsetMin; content.offsetMax = area.offsetMax;
                Destroy(area.gameObject);
            }
            var source = _window.transform.Find("Source Button");
            if (source) Destroy(source.gameObject);
            var title = _window.transform.Find("Title Banner")?.GetComponentInChildren<TMP_Text>(true);
            if (title) title.text = "COLORS";
            var close = _window.transform.Find("Close Button")?.GetComponent<Button>();
            if (close)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(Close);
            }

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            // The settings rows' look (a ListItem panel, a title and a detail line).
            var row = settings.window.transform.Find("Safe Area/Panel/Rows/CONTROLLER");
            var rowImage = row ? row.GetComponent<Image>() : null;
            var rowTitle = row ? row.Find("Title")?.GetComponent<TMP_Text>() : null;

            Section(content, rowImage, rowTitle, "JOYSTICK", ControlColors.Names, ControlColors.Swatches, () => ControlColors.Joystick,
                i => ControlColors.Knob(i) ?? ControlColors.ArtKnob, i => ControlColors.Joystick = i);
            Section(content, rowImage, rowTitle, "BUTTONS", ControlColors.Names, ControlColors.Swatches, () => ControlColors.Buttons,
                i => ControlColors.Button(i) ?? ControlColors.ArtButton, i => ControlColors.Buttons = i);
            Section(content, rowImage, rowTitle, "CABINET", CabinetStyles.Names, CabinetStyles.Swatches, () => CabinetStyles.Current,
                i => CabinetStyles.Art(i, 1), i => CabinetStyles.Current = i, 4, CrestCell);
            _window.SetActive(false);
        }

        void Section(RectTransform parent, Image rowImage, TMP_Text rowTitle, string name, string[] names, Color[] colors,
            System.Func<int> current, System.Func<int, Sprite> art, System.Action<int> pick, int columns = 3, Vector2 cell = default)
        {
            if (cell == default) cell = Cell;
            var section = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            section.SetParent(parent, false);
            var background = section.GetComponent<Image>();
            if (rowImage) { background.sprite = rowImage.sprite; background.type = rowImage.type; background.color = rowImage.color; }
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(7, 7, 5, 6);
            layout.spacing = 3f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            Label(section, rowTitle, name, rowTitle ? rowTitle.color : Color.white);
            var chosen = Label(section, rowTitle, "", Color.white);

            var grid = new GameObject("Colours", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(section, false);
            var cells = grid.gameObject.AddComponent<GridLayoutGroup>();
            cells.cellSize = cell;
            cells.spacing = new Vector2(4f, 2f);
            cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cells.constraintCount = columns;
            cells.childAlignment = TextAnchor.UpperCenter;
            int rows = (names.Length + columns - 1) / columns;
            grid.gameObject.AddComponent<LayoutElement>().preferredHeight = rows * cell.y + (rows - 1) * 2f;

            var marks = new GameObject[names.Length];
            var frame = Resources.Load<Sprite>("ControlColors/SwatchSelect");
            for (int i = 0; i < marks.Length; i++)
            {
                int index = i;
                var choice = new GameObject(names[i], typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
                choice.SetParent(grid, false);
                choice.GetComponent<Image>().color = Color.clear;   // only there to be tapped
                choice.GetComponent<Button>().onClick.AddListener(() => { pick(index); Refresh(); });
                var sprite = art(i);
                if (sprite)
                {
                    var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    icon.SetParent(choice, false);
                    icon.sizeDelta = sprite.rect.size;   // one art pixel to a canvas unit, like the rest
                    var image = icon.GetComponent<Image>();
                    image.sprite = sprite;
                    image.raycastTarget = false;
                }
                var mark = new GameObject("Chosen", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                mark.SetParent(choice, false);
                mark.anchorMin = Vector2.zero; mark.anchorMax = Vector2.one;
                mark.offsetMin = mark.offsetMax = Vector2.zero;
                var markImage = mark.GetComponent<Image>();
                markImage.sprite = frame;
                markImage.type = Image.Type.Sliced;
                markImage.raycastTarget = false;
                marks[i] = mark.gameObject;
            }
            _choices.Add(new Choice { names = names, colors = colors, current = current, marks = marks, chosen = chosen });
        }

        static TMP_Text Label(RectTransform parent, TMP_Text like, string text, Color color)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            if (like) { label.font = like.font; label.fontSize = like.fontSize; label.fontSharedMaterial = like.fontSharedMaterial; }
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            // One line, as tall as the settings rows' (TMP reports none here).
            var line = label.gameObject.AddComponent<LayoutElement>();
            line.minHeight = line.preferredHeight = Mathf.Max(8f, like ? like.fontSize : 8f);
            return label;
        }

        void Open()
        {
            _window.SetActive(true);
            Refresh();
        }

        void Close() => _window.SetActive(false);

        void Refresh()
        {
            foreach (var c in _choices)
            {
                int chosen = c.current();
                for (int i = 0; i < c.marks.Length; i++) c.marks[i].SetActive(i == chosen);
                c.chosen.text = c.names[chosen];
                // (Black, named in grey: it'd vanish on the dark panel.)
                var color = c.colors[chosen];
                c.chosen.color = color.maxColorComponent < 0.4f ? new Color32(150, 150, 170, 255) : color;
            }
        }

        void Update()
        {
            // Settings closing takes this with it.
            if (_window && _window.activeSelf && !SettingsScreen.IsOpen) _window.SetActive(false);
        }
    }
}
