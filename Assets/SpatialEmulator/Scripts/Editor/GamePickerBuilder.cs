// GamePickerBuilder.cs — Tools > Spatial Emulator > Build Game Picker.
// Regenerates Prefabs/GamePicker.prefab — the pixel-art HUD on its own
// canvas: the game picker and its GAMES button, the hint banner, and the
// Lock and Delete Cabinet buttons — and puts one instance in the open scene.
// It rewires CabinetHints / CabinetManipulator / CabinetLockButton to the
// pixel versions and switches off ArcadeControls' old Hint Banner, Lock
// Cabinet and Delete Cabinet objects (left in place, just inactive). The
// arcade controls themselves are never touched, so their hand-placed
// positions are safe. Hand edits to the picker prefab are lost on a rebuild:
// change the layout here instead.
//
// Colours (Pixel UI & HUD 4 sets): picker graphics Purple, buttons Blue,
// prompts Blue scanlines, delete buttons Red.
//
// It also sets up the assets the picker uses:
// - Art/Picker/*.png (copied from the Pixel UI & HUD 4 pack) are imported as
//   point-filtered, uncompressed sprites with 9-slice borders.
// - Fonts/*.otf|ttf (Dead Revolver pixel fonts) get bitmap TMP font assets.
//
// Everything is laid out in art pixels: PixelCanvasScaler scales the canvas
// by a whole number so each art pixel is N screen pixels.

using System.Collections.Generic;
using System.IO;
using SpatialEmulator.Games;
using SpatialEmulator.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace SpatialEmulator.Editor
{
    public static class GamePickerBuilder
    {
        const string Root = "Assets/SpatialEmulator";
        const string ArtDir = Root + "/Art/Picker";
        const string ControlsArtDir = Root + "/Art/Controls";
        const string FontDir = Root + "/Fonts";
        const string PrefabPath = Root + "/Prefabs/GamePicker.prefab";
        const string CatalogPath = Root + "/Data/GameCatalog.txt";

        // Native sizes: one font pixel = one art pixel (1024-unit em /
        // 96-unit pixels for Arcade, /128 for Game Compact).
        const float ArcadeSize = 1024f / 96f;
        const float CompactSize = 8f;

        static readonly Color32 Lavender = new Color32(232, 224, 255, 255); // main text
        static readonly Color32 Violet = new Color32(150, 126, 236, 255);   // secondary text
        static readonly Color32 Ink = new Color32(27, 25, 29, 255);         // on the blue keycaps
        static readonly Color32 PromptText = new Color32(236, 240, 255, 255);
        static readonly Color32 NeonBlue = new Color32(0, 220, 255, 255);

        // 9-slice borders in pixels: left, bottom, right, top.
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "PanelLarge", new Vector4(4, 5, 4, 4) },
            { "ListItem", new Vector4(2, 3, 2, 2) },
            { "ListItemSelected", new Vector4(3, 3, 3, 3) },
            { "ButtonKeyB_Unpressed", new Vector4(6, 7, 6, 5) },
            { "ButtonKeyB_Pressed", new Vector4(6, 7, 6, 5) },
            { "ButtonKeyBRed_Unpressed", new Vector4(6, 7, 6, 5) },
            { "ButtonKeyBRed_Pressed", new Vector4(6, 7, 6, 5) },
            { "ButtonKeyBPurple_Unpressed", new Vector4(6, 7, 6, 5) },
            { "ButtonKeyBPurple_Pressed", new Vector4(6, 7, 6, 5) },
            { "DividerF", new Vector4(12, 0, 12, 0) },
            { "SpeechScanlines", new Vector4(5, 5, 5, 5) },
            { "SliderFull", new Vector4(4, 0, 4, 0) },
            { "SliderFullEmpty", new Vector4(4, 0, 4, 0) },
            { "ScreenFrame", new Vector4(14, 14, 14, 14) },
        };

        // Old plain-uGUI objects in ArcadeControls that the pixel HUD replaces.
        static readonly string[] ReplacedControls = { "Hint Banner", "Lock Cabinet", "Delete Cabinet" };

        [MenuItem("Tools/Spatial Emulator/Build Game Picker")]
        public static void Build()
        {
            ConfigureSprites();
            var arcade = PixelFont("DeadRevolverArcade.otf", "DeadRevolverArcade Pixel");
            var compact = PixelFont("DeadRevolverGameCompact.ttf", "DeadRevolverGameCompact Pixel");
            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);

            var root = BuildHierarchy(arcade, compact, catalog);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            PlaceInScene();
            StyleStartButton(compact);
            StyleArcadeControls(arcade);
            BuildScreenFrame();
            BuildSavePanel(arcade, compact);
            SetupGamepad();
            Debug.Log("[GamePickerBuilder] built " + PrefabPath);
        }

        // ---- assets ----

        static void ConfigureSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir, ControlsArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                string name = Path.GetFileNameWithoutExtension(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; // = the canvas's reference PPU: 1 sprite pixel = 1 canvas unit
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = name == "CheckerTile" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = Borders.TryGetValue(name, out var border) ? border : Vector4.zero;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        // Bitmap (not SDF) font asset with every printable ASCII character
        // baked in, point filtered: sampled at 32 so a font pixel is a whole
        // number of atlas texels (3 for Arcade, 4 for Game Compact).
        static TMP_FontAsset PixelFont(string fileName, string assetName)
        {
            string assetPath = $"{FontDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{fileName}");
            var asset = TMP_FontAsset.CreateFontAsset(font, 32, 2, GlyphRenderMode.RASTER, 512, 512, AtlasPopulationMode.Dynamic, false);
            asset.name = assetName;
            var chars = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++) chars.Append(c);
            chars.Append("•…©");
            asset.TryAddCharacters(chars.ToString(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[GamePickerBuilder] {assetName} has no glyphs for: {missing}");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(asset, assetPath);
            var atlas = asset.atlasTextures[0];
            atlas.name = assetName + " Atlas";
            atlas.filterMode = FilterMode.Point;
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // ---- hierarchy ----

        static GameObject BuildHierarchy(TMP_FontAsset arcade, TMP_FontAsset compact, TextAsset catalog)
        {
            var root = new GameObject("GamePicker", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // above ArcadeControls (10)
            canvas.pixelPerfect = true;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100;
            root.AddComponent<PixelCanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            var filePicker = root.AddComponent<NativeFilePicker>();
            var picker = root.AddComponent<GamePicker>();
            picker.catalog = catalog;
            picker.filePicker = filePicker;
            picker.rowSprite = Art("ListItem");
            picker.rowCurrentSprite = Art("ListItemSelected");
            picker.textColor = Lavender;
            picker.dimTextColor = Violet;

            // In-game HUD, drawn under the picker window (earlier siblings draw first).
            BuildHud(root.transform, compact);

            // GAMES button: top left, mirroring the Lock button top right
            // (130 x 280 reference units in from that corner = 22 x 48 art px).
            // The keycap at its native 32 x 20, about the Lock button's size.
            var games = KeyButton("Games Button", root.transform, compact, CompactSize, "GAMES", new Vector2(32, 20));
            Place(games, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(22, -48), new Vector2(32, 20));
            picker.openButton = games.gameObject;

            // Settings: a steel gear-and-wrench button just right of GAMES
            // (Art/Picker/GearButton*.png, shaded like the arcade buttons),
            // as tall as GAMES. The 32 px art is shown at 20 px: redrawn
            // natively at 20 px the wrench was unreadable.
            var gear = UI("Settings Button", root.transform);
            Place(gear, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(55, -48), new Vector2(20, 20)); // 7 px gap after GAMES
            var gearImage = Img(gear, Art("GearButton"), Image.Type.Simple, Color.white);
            SpriteSwapButton(gear.gameObject, gearImage, Art("GearButtonPressed"));

            var window = Stretch(UI("Window", root.transform));
            picker.window = window.gameObject;
            var backdrop = Stretch(UI("Backdrop", window));
            Img(backdrop, Art("White"), Image.Type.Simple, new Color(0, 0, 0, 0.85f));

            var safe = Stretch(UI("Safe Area", window));
            safe.gameObject.AddComponent<SafeAreaFitter>();

            var panel = UI("Panel", safe);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(4, 4);
            panel.offsetMax = new Vector2(-4, -24); // room for the banner's starburst
            Img(panel, Art("PanelLarge"), Image.Type.Sliced, Color.white);

            var checker = UI("Checker", panel);
            checker.anchorMin = Vector2.zero;
            checker.anchorMax = Vector2.one;
            checker.offsetMin = new Vector2(3, 4);
            checker.offsetMax = new Vector2(-3, -3);
            Img(checker, Art("CheckerTile"), Image.Type.Tiled, Color.white).raycastTarget = false;

            var banner = UI("Title Banner", panel);
            Place(banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(144, 40));
            Img(banner, Art("TitleBannerDecoratorA"), Image.Type.Simple, Color.white).raycastTarget = false;
            var title = Text(UI("Label", banner), arcade, ArcadeSize, Lavender, TextAlignmentOptions.Center, "SELECT GAME");
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 12));

            // Close: a 13 px icon with a 25 px touch area.
            var close = UI("Close Button", panel);
            Place(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-12, -14), new Vector2(25, 25));
            Img(close, Art("White"), Image.Type.Simple, new Color(1, 1, 1, 0));
            var closeIcon = UI("Icon", close);
            Place(closeIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13, 13));
            var closeImage = Img(closeIcon, Art("ButtonCloseB_Unpressed"), Image.Type.Simple, Color.white);
            closeImage.raycastTarget = false;
            picker.closeButton = SpriteSwapButton(close.gameObject, closeImage, Art("ButtonCloseB_Pressed"));

            // Show-all toggle on the left (Close has the top right), count on the right.
            var showAll = KeyButton("Show All Button", panel, compact, CompactSize, "SHOW ALL", new Vector2(46, 16));
            Place(showAll, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(8, -30), new Vector2(46, 16));
            picker.showAllButton = showAll.GetComponent<Button>();
            picker.showAllLabel = showAll.GetComponentInChildren<TMP_Text>();

            var count = Text(UI("Count", panel), compact, CompactSize, Violet, TextAlignmentOptions.Right, "0 games");
            TopStrip(count.rectTransform, -30, 8, 10);
            picker.countLabel = count;

            var divider = UI("Divider", panel);
            TopStrip(divider, -43, 9, 6);
            Img(divider, Art("DividerF"), Image.Type.Sliced, Color.white).raycastTarget = false;

            // Scrolling list of games.
            var listRect = UI("List", panel);
            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.one;
            listRect.offsetMin = new Vector2(6, 32);
            listRect.offsetMax = new Vector2(-6, -51);
            Img(listRect, Art("White"), Image.Type.Simple, new Color(1, 1, 1, 0)); // catches drags between rows
            var viewport = Stretch(UI("Viewport", listRect));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UI("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = listRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.scrollSensitivity = 8;
            picker.list = scroll;
            picker.rowTemplate = Row(content, compact);

            var empty = Text(UI("Empty State", listRect), compact, CompactSize, Violet, TextAlignmentOptions.Center,
                "No games yet\n\nTap ADD ROM to import zip romsets from the Files app, or SCAN FOLDER to add every game in a folder at once\n\nCapcom CPS1/CPS2, Neo Geo, Irem M92, Konami, CAVE, IGS PGM, Sega System 16/18, Namco, Midway, Taito F3, Technos and Toaplan games are supported. Neo Geo games also need neogeo.zip, PGM games pgm.zip, CPS2 games qsound.zip");
            var emptyRect = empty.rectTransform;
            emptyRect.anchorMin = Vector2.zero;
            emptyRect.anchorMax = Vector2.one;
            emptyRect.offsetMin = new Vector2(8, 8);
            emptyRect.offsetMax = new Vector2(-8, -8);
            empty.textWrappingMode = TextWrappingModes.Normal;
            picker.emptyState = empty.gameObject;

            var addRom = KeyButton("Add Rom Button", panel, arcade, ArcadeSize, "ADD ROM", new Vector2(64, 20));
            Place(addRom, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 17), new Vector2(64, 20));
            picker.addRomButton = addRom.GetComponent<Button>();

            BuildSettings(root, gear.GetComponent<Button>(), arcade, compact);
            BuildPlayerSelect(root, arcade, compact);

            // Toast: outside Window so import messages show even when closed.
            var toastArea = Stretch(UI("Toast Area", root.transform));
            toastArea.gameObject.AddComponent<SafeAreaFitter>();
            var toast = UI("Toast", toastArea);
            toast.anchorMin = new Vector2(0, 0);
            toast.anchorMax = new Vector2(1, 0);
            toast.pivot = new Vector2(0.5f, 0);
            toast.offsetMin = new Vector2(12, 32);
            toast.offsetMax = new Vector2(-12, 50);
            Img(toast, Art("SpeechScanlines"), Image.Type.Tiled, Color.white).raycastTarget = false;
            var toastLayout = toast.gameObject.AddComponent<VerticalLayoutGroup>();
            toastLayout.padding = new RectOffset(8, 8, 6, 6);
            toastLayout.childControlWidth = true;
            toastLayout.childControlHeight = true;
            toastLayout.childForceExpandHeight = false;
            toast.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            picker.toast = toastGroup;
            var toastLabel = Text(UI("Label", toast), compact, CompactSize, PromptText, TextAlignmentOptions.Center, "");
            toastLabel.textWrappingMode = TextWrappingModes.Normal;
            picker.toastLabel = toastLabel;

            return root;
        }

        // Hint banner, Lock and Delete Cabinet at the spots the ArcadeControls
        // versions had (converted from 1080-wide reference units to art pixels).
        static void BuildHud(Transform root, TMP_FontAsset compact)
        {
            // Hint banner: Blue scanlines, never takes touches.
            // Its top sits 11 px below the GAMES / LOCK / DELETE row (bottom at
            // -58); it grows downward to fit 1-3 lines.
            var hint = UI("Hint Banner", root);
            Place(hint, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -69), new Vector2(172, 30));
            Img(hint, Art("SpeechScanlines"), Image.Type.Tiled, Color.white).raycastTarget = false;
            var hintLayout = hint.gameObject.AddComponent<VerticalLayoutGroup>();
            hintLayout.padding = new RectOffset(7, 7, 6, 6);
            hintLayout.childControlWidth = true;
            hintLayout.childControlHeight = true;
            hintLayout.childForceExpandHeight = false;
            hint.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = false;
            hintGroup.interactable = false;
            hintGroup.alpha = 0;
            var hintLabel = Text(UI("Label", hint), compact, CompactSize, PromptText, TextAlignmentOptions.Center, "");
            hintLabel.textWrappingMode = TextWrappingModes.Normal;
            hintLabel.richText = true;

            // LOCK lives on the cabinet now, under the saves panel (BuildSavePanel).

            // Delete Cabinet: top centre, red, shown while the cabinet is selected.
            var delete = KeyButton("Delete Cabinet Button", root, compact, CompactSize, "DELETE", new Vector2(44, 20), "ButtonKeyBRed", Lavender);
            Place(delete, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(44, 20));
            delete.gameObject.SetActive(false);
        }

        // An 11 x 11 icon sprite inside a button's label area (so it sinks with
        // the label when pressed).
        static Image Icon(RectTransform parent, string sprite, Vector2 anchor, Vector2 position)
        {
            var rect = UI("Icon", parent);
            Place(rect, anchor, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(11, 11));
            var image = Img(rect, Art(sprite), Image.Type.Simple, Color.white);
            image.raycastTarget = false;
            return image;
        }

        // The player chooser (hold COIN in games where the coin slot picks the
        // player): a small framed window in the middle of the screen, one
        // keycap per player. Tapping outside it closes it.
        static void BuildPlayerSelect(GameObject root, TMP_FontAsset arcade, TMP_FontAsset compact)
        {
            var chooser = root.AddComponent<PlayerSelect>();
            chooser.labelColor = Ink;
            chooser.chosenColor = Color.white;

            var window = Stretch(UI("Player Select Window", root.transform));
            chooser.window = window.gameObject;
            var backdrop = Stretch(UI("Backdrop", window));
            var backdropImage = Img(backdrop, Art("White"), Image.Type.Simple, new Color(0, 0, 0, 0.6f));
            chooser.backdropButton = backdrop.gameObject.AddComponent<Button>();
            chooser.backdropButton.targetGraphic = backdropImage;
            chooser.backdropButton.transition = Selectable.Transition.None;
            chooser.backdropButton.navigation = new Navigation { mode = Navigation.Mode.None };

            const int rows = 4;
            var panel = UI("Panel", window);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(128, 36 + rows * 23 + 44));
            Img(panel, Art("PanelLarge"), Image.Type.Sliced, Color.white);
            var checker = UI("Checker", panel);
            checker.anchorMin = Vector2.zero;
            checker.anchorMax = Vector2.one;
            checker.offsetMin = new Vector2(3, 4);
            checker.offsetMax = new Vector2(-3, -3);
            Img(checker, Art("CheckerTile"), Image.Type.Tiled, Color.white).raycastTarget = false;

            var banner = UI("Title Banner", panel);
            Place(banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(144, 40));
            Img(banner, Art("TitleBannerDecoratorA"), Image.Type.Simple, Color.white).raycastTarget = false;
            var title = Text(UI("Label", banner), arcade, ArcadeSize, Lavender, TextAlignmentOptions.Center, "PLAY AS");
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 12));

            var close = UI("Close Button", panel);
            Place(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-12, -14), new Vector2(25, 25));
            Img(close, Art("White"), Image.Type.Simple, new Color(1, 1, 1, 0));
            var closeIcon = UI("Icon", close);
            Place(closeIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13, 13));
            var closeImage = Img(closeIcon, Art("ButtonCloseB_Unpressed"), Image.Type.Simple, Color.white);
            closeImage.raycastTarget = false;
            chooser.closeButton = SpriteSwapButton(close.gameObject, closeImage, Art("ButtonCloseB_Pressed"));

            chooser.playerButtons = new Button[rows];
            chooser.playerLabels = new TMP_Text[rows];
            for (int i = 0; i < rows; i++)
            {
                var key = KeyButton($"Player {i + 1}", panel, compact, CompactSize, $"P{i + 1}", new Vector2(104, 20));
                Place(key, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -34 - i * 23), new Vector2(104, 20));
                chooser.playerButtons[i] = key.GetComponent<Button>();
                chooser.playerLabels[i] = key.GetComponentInChildren<TMP_Text>();
            }

            // Mid-game switch: JOIN (keep playing, the old player is out) or
            // RESTART, under the players. Hidden until a player is picked.
            var choice = UI("Switch Choice", panel);
            Place(choice, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(112, 40));
            chooser.choiceGroup = choice.gameObject;
            var question = Text(UI("Question", choice), compact, CompactSize, Lavender, TextAlignmentOptions.Center, "Is your player out?");
            Place(question.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 12));
            chooser.choiceText = question;
            var join = KeyButton("Join Button", choice, compact, CompactSize, "JOIN", new Vector2(50, 20));
            Place(join, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(2, 0), new Vector2(50, 20));
            chooser.joinButton = join.GetComponent<Button>();
            var restart = KeyButton("Restart Button", choice, compact, CompactSize, "RESTART", new Vector2(54, 20), "ButtonKeyBPurple");
            Place(restart, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-2, 0), new Vector2(54, 20));
            chooser.restartButton = restart.GetComponent<Button>();
        }

        // The Settings window: same frame as the picker, one row per setting.
        static void BuildSettings(GameObject root, Button openButton, TMP_FontAsset arcade, TMP_FontAsset compact)
        {
            var settings = root.AddComponent<SettingsScreen>();
            settings.openButton = openButton;

            var window = Stretch(UI("Settings Window", root.transform));
            settings.window = window.gameObject;
            settings.windowGroup = window.gameObject.AddComponent<CanvasGroup>();
            Img(Stretch(UI("Backdrop", window)), Art("White"), Image.Type.Simple, new Color(0, 0, 0, 0.85f));
            var safe = Stretch(UI("Safe Area", window));
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var panel = UI("Panel", safe);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(4, 4);
            panel.offsetMax = new Vector2(-4, -24);
            Img(panel, Art("PanelLarge"), Image.Type.Sliced, Color.white);
            var checker = UI("Checker", panel);
            checker.anchorMin = Vector2.zero;
            checker.anchorMax = Vector2.one;
            checker.offsetMin = new Vector2(3, 4);
            checker.offsetMax = new Vector2(-3, -3);
            Img(checker, Art("CheckerTile"), Image.Type.Tiled, Color.white).raycastTarget = false;

            var banner = UI("Title Banner", panel);
            Place(banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(144, 40));
            Img(banner, Art("TitleBannerDecoratorA"), Image.Type.Simple, Color.white).raycastTarget = false;
            var title = Text(UI("Label", banner), arcade, ArcadeSize, Lavender, TextAlignmentOptions.Center, "SETTINGS");
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 12));

            var close = UI("Close Button", panel);
            Place(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-12, -14), new Vector2(25, 25));
            Img(close, Art("White"), Image.Type.Simple, new Color(1, 1, 1, 0));
            var closeIcon = UI("Icon", close);
            Place(closeIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13, 13));
            var closeImage = Img(closeIcon, Art("ButtonCloseB_Unpressed"), Image.Type.Simple, Color.white);
            closeImage.raycastTarget = false;
            settings.closeButton = SpriteSwapButton(close.gameObject, closeImage, Art("ButtonCloseB_Pressed"));

            var list = UI("Rows", panel);
            list.anchorMin = new Vector2(0, 1);
            list.anchorMax = new Vector2(1, 1);
            list.pivot = new Vector2(0.5f, 1);
            list.offsetMin = new Vector2(6, 0);
            list.offsetMax = new Vector2(-6, -30);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            settings.controllerStatus = SettingsRow(list, compact, "CONTROLLER", "", null, out _);
            settings.touchControlsDetail = SettingsRow(list, compact, "ON-SCREEN CONTROLS", "", "AUTO", out var touchButton);
            settings.touchControlsButton = touchButton;
            settings.touchControlsValue = touchButton.GetComponentInChildren<TMP_Text>();
            SettingsRow(list, compact, "RESUME SAVES", "Load a game's newest save when it starts", "OFF", out var resumeButton);
            settings.resumeButton = resumeButton;
            settings.resumeValue = resumeButton.GetComponentInChildren<TMP_Text>();
            SettingsRow(list, compact, "GAMES IN AR", "Show your games in the cabinet's frame", "OFF", out var pickerInARButton);
            settings.pickerInARButton = pickerInARButton;
            settings.pickerInARValue = pickerInARButton.GetComponentInChildren<TMP_Text>();
            var spacingRow = SettingsRow(list, compact, "LAYER SPACING", "Default", null, out _);
            settings.spacingValue = spacingRow;
            settings.spacingSlider = SpacingSlider((RectTransform)spacingRow.transform.parent);

            var about = KeyButton("About Button", panel, arcade, ArcadeSize, "ABOUT", new Vector2(64, 20));
            Place(about, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 17), new Vector2(64, 20));
            settings.aboutButton = about.GetComponent<Button>();
            BuildAbout(settings, safe, arcade, compact);
        }

        // ABOUT (over the Settings window): credits, the GPL notice, a SOURCE
        // CODE link and the licence text, scrolling. SettingsScreen fills it.
        static void BuildAbout(SettingsScreen settings, RectTransform safe, TMP_FontAsset arcade, TMP_FontAsset compact)
        {
            var panel = UI("About Window", safe);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(4, 4);
            panel.offsetMax = new Vector2(-4, -24);
            Img(panel, Art("PanelLarge"), Image.Type.Sliced, Color.white);
            settings.aboutWindow = panel.gameObject;
            var checker = UI("Checker", panel);
            checker.anchorMin = Vector2.zero;
            checker.anchorMax = Vector2.one;
            checker.offsetMin = new Vector2(3, 4);
            checker.offsetMax = new Vector2(-3, -3);
            Img(checker, Art("CheckerTile"), Image.Type.Tiled, Color.white).raycastTarget = false;

            var banner = UI("Title Banner", panel);
            Place(banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(144, 40));
            Img(banner, Art("TitleBannerDecoratorA"), Image.Type.Simple, Color.white).raycastTarget = false;
            var title = Text(UI("Label", banner), arcade, ArcadeSize, Lavender, TextAlignmentOptions.Center, "ABOUT");
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 12));

            var close = UI("Close Button", panel);
            Place(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-12, -14), new Vector2(25, 25));
            Img(close, Art("White"), Image.Type.Simple, new Color(1, 1, 1, 0));
            var closeIcon = UI("Icon", close);
            Place(closeIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13, 13));
            var closeImage = Img(closeIcon, Art("ButtonCloseB_Unpressed"), Image.Type.Simple, Color.white);
            closeImage.raycastTarget = false;
            settings.aboutCloseButton = SpriteSwapButton(close.gameObject, closeImage, Art("ButtonCloseB_Pressed"));

            var listRect = UI("Text", panel);
            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.one;
            listRect.offsetMin = new Vector2(6, 32);
            listRect.offsetMax = new Vector2(-6, -30);
            Img(listRect, Art("White"), Image.Type.Simple, new Color(1, 1, 1, 0)); // catches drags
            var viewport = Stretch(UI("Viewport", listRect));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UI("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 2, 6);
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = listRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.scrollSensitivity = 8;
            settings.aboutContent = content;
            var template = Text(UI("Block Template", content), compact, CompactSize, Lavender, TextAlignmentOptions.TopLeft, "");
            template.textWrappingMode = TextWrappingModes.Normal;
            template.raycastTarget = false;
            settings.aboutTextTemplate = template;
            settings.licenseText = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/GPL-2.0.txt");

            var source = KeyButton("Source Button", panel, arcade, ArcadeSize, "SOURCE CODE", new Vector2(96, 20));
            Place(source, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 17), new Vector2(96, 20));
            settings.sourceButton = source.GetComponent<Button>();
        }

        // The Layer spacing slider (pack's Blue slider: neon track, diamond
        // handle), with a tick at the centre = default spacing. The whole
        // 18 px tall line takes touches; the track is 9 px.
        static Slider SpacingSlider(RectTransform row)
        {
            var line = UI("Slider", row);
            var element = line.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 18;
            Img(line, Art("White"), Image.Type.Simple, new Color(1, 1, 1, 0)); // touch area

            var track = UI("Track", line);
            track.anchorMin = new Vector2(0, 0.5f);
            track.anchorMax = new Vector2(1, 0.5f);
            track.sizeDelta = new Vector2(-8, 9);
            Img(track, Art("SliderFullEmpty"), Image.Type.Sliced, Color.white).raycastTarget = false;

            // Over the handle's default (1) position: the handle travels the
            // line less 4 px at each end.
            float tickAt = 1f / AppSettings.MaxLayerSpacingScale;
            var tick = UI("Default Tick", line);
            Place(tick, new Vector2(tickAt, 0.5f), new Vector2(tickAt, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(Mathf.Round(4f * (1f - 2f * tickAt)), 7), new Vector2(1, 3));
            Img(tick, Art("White"), Image.Type.Simple, Lavender).raycastTarget = false;

            var fillArea = UI("Fill Area", line);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-8, 9);
            var fill = UI("Fill", fillArea);
            fill.sizeDelta = Vector2.zero;
            Img(fill, Art("SliderFull"), Image.Type.Sliced, Color.white).raycastTarget = false;

            // The slider stretches the handle to its area's height, so the
            // area is exactly the diamond's 8 px.
            var handleArea = UI("Handle Area", line);
            handleArea.anchorMin = new Vector2(0, 0.5f);
            handleArea.anchorMax = new Vector2(1, 0.5f);
            handleArea.sizeDelta = new Vector2(-8, 8);
            var handle = UI("Handle", handleArea);
            handle.sizeDelta = new Vector2(8, 8);
            var handleImage = Img(handle, Art("SliderDiamondHandle"), Image.Type.Simple, Color.white);

            var slider = line.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.transition = Selectable.Transition.SpriteSwap;
            slider.spriteState = new SpriteState { pressedSprite = Art("SliderDiamondHandleHovered") };
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = AppSettings.MaxLayerSpacingScale;
            slider.value = 1;
            return slider;
        }

        // A settings row: title, wrapping detail text, and optionally a value
        // keycap (tap to change). Returns the detail text.
        static TMP_Text SettingsRow(RectTransform list, TMP_FontAsset compact, string title, string detail, string value, out Button valueButton)
        {
            var row = UI(title, list);
            Img(row, Art("ListItem"), Image.Type.Sliced, Color.white).raycastTarget = false;
            var layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(7, 7, 5, 6);
            layout.spacing = 3;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var titleText = Text(UI("Title", row), compact, CompactSize, Lavender, TextAlignmentOptions.TopLeft, title);
            titleText.textWrappingMode = TextWrappingModes.Normal;
            var detailText = Text(UI("Detail", row), compact, CompactSize, Violet, TextAlignmentOptions.TopLeft, detail);
            detailText.textWrappingMode = TextWrappingModes.Normal;
            valueButton = null;
            if (value != null)
            {
                var line = UI("Value", row);
                var element = line.gameObject.AddComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = 18;
                var key = KeyButton("Value Button", line, compact, CompactSize, value, new Vector2(46, 18));
                Place(key, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(46, 18));
                valueButton = key.GetComponent<Button>();
            }
            return detailText;
        }

        static GameRow Row(RectTransform content, TMP_FontAsset compact)
        {
            var rowRect = UI("Row Template", content);
            var row = rowRect.gameObject.AddComponent<GameRow>();
            row.background = Img(rowRect, Art("ListItem"), Image.Type.Sliced, Color.white);
            var button = rowRect.gameObject.AddComponent<Button>();
            button.targetGraphic = row.background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            row.button = button;

            var layout = rowRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 5, 5, 6);
            layout.spacing = 2;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var chevron = UI("Chevron", rowRect);
            chevron.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(chevron, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -5), new Vector2(5, 9));
            row.chevron = Img(chevron, Art("Chevron"), Image.Type.Simple, Lavender);
            row.chevron.raycastTarget = false;

            row.title = RowText("Title", rowRect, compact, Lavender);
            row.details = RowText("Details", rowRect, compact, Violet);
            row.status = RowText("Status", rowRect, compact, Lavender);

            row.group = rowRect.gameObject.AddComponent<CanvasGroup>();
            row.hold = rowRect.gameObject.AddComponent<HoldGesture>();

            // Shown by holding the row; a red keycap on the row's right edge.
            var delete = KeyButton("Delete Button", rowRect, compact, CompactSize, "DELETE", new Vector2(40, 16), "ButtonKeyBRed", Lavender);
            delete.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(delete, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(40, 16));
            delete.gameObject.SetActive(false);
            row.deleteButton = delete.GetComponent<Button>();
            return row;
        }

        static TMP_Text RowText(string name, RectTransform parent, TMP_FontAsset font, Color color)
        {
            var text = Text(UI(name, parent), font, CompactSize, color, TextAlignmentOptions.TopLeft, name);
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        // A keycap button (tan by default) with a label that sinks when pressed.
        static RectTransform KeyButton(string name, Transform parent, TMP_FontAsset font, float fontSize, string label, Vector2 size,
            string sprite = "ButtonKeyB", Color32? labelColor = null)
        {
            var rect = UI(name, parent);
            rect.sizeDelta = size;
            var image = Img(rect, Art(sprite + "_Unpressed"), Image.Type.Sliced, Color.white);
            SpriteSwapButton(rect.gameObject, image, Art(sprite + "_Pressed"));
            // The cap's face is the top 13-14 px; the rest is its shadow.
            var text = Text(UI("Label", rect), font, fontSize, labelColor ?? Ink, TextAlignmentOptions.Center, label);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(2, 5);
            textRect.offsetMax = new Vector2(-2, -1);
            var press = rect.gameObject.AddComponent<PressOffset>();
            press.content = textRect;
            press.pressedOffset = new Vector2(0, -1);
            return rect;
        }

        static Button SpriteSwapButton(GameObject go, Image target, Sprite pressed)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = pressed };
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        // ---- helpers ----

        static RectTransform UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Full-width strip centred `y` below the parent's top, `inset` in from each side.
        static void TopStrip(RectTransform rect, float y, float height, float inset)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(-2 * inset, height);
        }

        static Image Img(RectTransform rect, Sprite sprite, Image.Type type, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.pixelsPerUnitMultiplier = 1;
            return image;
        }

        static TextMeshProUGUI Text(RectTransform rect, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, string value)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.text = value;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // ---- save states (in the cabinet prefab) ----

        const string CabinetPrefabPath = Root + "/Prefabs/ARCabinet.prefab";

        // A world-space pixel panel right of the cabinet's screen: SAVE button
        // and the newest three saves (SaveStatePanel places it at runtime).
        // 1 art pixel = 2 mm at cabinet scale 1.
        // A fancy pixel frame in front of the cabinet's top layer (ScreenFrame
        // sizes and places it at runtime).
        static void BuildScreenFrame()
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(CabinetPrefabPath);
            var cabinet = scope.prefabContentsRoot;
            var old = cabinet.transform.Find("Screen Frame");
            if (old) Object.DestroyImmediate(old.gameObject);
            var stack = cabinet.GetComponentInChildren<SpatialEmulator.Mobile.MobileRetroDepthLayerStack>(true);

            var root = UI("Screen Frame", cabinet.transform);
            root.sizeDelta = new Vector2(322, 248);
            root.localScale = Vector3.one * (stack ? stack.screenHeight / 224f : 0.0018f);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.gameObject.AddComponent<ScreenFrame>().stack = stack;

            var checker = Stretch(UI("Checker", root));
            checker.offsetMin = new Vector2(10, 10);
            checker.offsetMax = new Vector2(-10, -10);
            Img(checker, Art("CheckerTile"), Image.Type.Tiled, Color.white).raycastTarget = false;
            var frame = Img(Stretch(UI("Frame", root)), Art("ScreenFrame"), Image.Type.Sliced, Color.white);
            frame.fillCenter = false;
            frame.raycastTarget = false;
            var crest = UI("Crest", root);
            Place(crest, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6.5f), new Vector2(33, 15));
            Img(crest, Art("ScreenFrameCrest"), Image.Type.Simple, Color.white).raycastTarget = false;
        }

        static void BuildSavePanel(TMP_FontAsset arcade, TMP_FontAsset compact)
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(CabinetPrefabPath);
            var cabinet = scope.prefabContentsRoot;
            var old = cabinet.transform.Find("Save Panel");
            if (old) Object.DestroyImmediate(old.gameObject);
            var oldLock = cabinet.transform.Find("Lock Panel"); // LOCK used to sit left of the frame
            if (oldLock) Object.DestroyImmediate(oldLock.gameObject);
            var stack = cabinet.GetComponentInChildren<SpatialEmulator.Mobile.MobileRetroDepthLayerStack>(true);

            var root = UI("Save Panel", cabinet.transform);
            root.sizeDelta = new Vector2(56, 180);
            root.localScale = Vector3.one * 0.002f;
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var panel = root.gameObject.AddComponent<SaveStatePanel>();
            panel.canvas = canvas;
            panel.stack = stack;
            panel.frame = cabinet.GetComponentInChildren<ScreenFrame>(true);

            // Content: the framed panel, top-aligned, growing to fit.
            var content = UI("Content", root);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero; // exactly the panel's 56 px width
            content.anchoredPosition = Vector2.zero;
            Img(content, Art("PanelLarge"), Image.Type.Sliced, Color.white);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 6, 7);
            layout.spacing = 3;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.content = content.gameObject;

            Text(UI("Title", content), arcade, ArcadeSize, Lavender, TextAlignmentOptions.Center, "SAVES");
            panel.countLabel = Text(UI("Count", content), compact, CompactSize, Violet, TextAlignmentOptions.Center, "0 SAVES");

            // SAVE keycap with a floppy icon.
            var saveLine = UI("Save Line", content);
            var saveLineElement = saveLine.gameObject.AddComponent<LayoutElement>();
            saveLineElement.minHeight = saveLineElement.preferredHeight = 20;
            var save = KeyButton("Save Button", saveLine, compact, CompactSize, "SAVE", new Vector2(44, 20));
            Place(save, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 20));
            var saveLabel = save.GetComponentInChildren<TMP_Text>().rectTransform;
            saveLabel.offsetMin = new Vector2(15, saveLabel.offsetMin.y);
            Icon(saveLabel, "IconSave", new Vector2(0, 0.5f), new Vector2(-6.5f, 0.5f));
            panel.saveButton = save.GetComponent<Button>();

            var empty = Text(UI("Empty", content), compact, CompactSize, Violet, TextAlignmentOptions.Center, "No saves yet");
            empty.textWrappingMode = TextWrappingModes.Normal;
            panel.emptyLabel = empty.gameObject;

            var slots = new List<SaveStatePanel.Slot>();
            for (int i = 0; i < 3; i++)
            {
                var slotRect = UI("Slot " + (i + 1), content);
                var bg = Img(slotRect, Art("ListItem"), Image.Type.Sliced, Color.white);
                var slotLayout = slotRect.gameObject.AddComponent<VerticalLayoutGroup>();
                slotLayout.padding = new RectOffset(3, 3, 3, 4);
                slotLayout.spacing = 2;
                slotLayout.childControlWidth = true;
                slotLayout.childControlHeight = true;
                slotLayout.childForceExpandWidth = true;
                slotLayout.childForceExpandHeight = false;
                var button = slotRect.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var colors = button.colors;
                colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
                colors.highlightedColor = colors.selectedColor = Color.white;
                button.colors = colors;

                var thumbRect = UI("Thumbnail", slotRect);
                var thumbElement = thumbRect.gameObject.AddComponent<LayoutElement>();
                thumbElement.minHeight = thumbElement.preferredHeight = 30; // 40 x 30, 4:3
                var thumb = thumbRect.gameObject.AddComponent<RawImage>();
                thumb.raycastTarget = false;
                var time = Text(UI("Time", slotRect), compact, CompactSize, Lavender, TextAlignmentOptions.Center, "--");

                var delete = KeyButton("Delete Button", slotRect, compact, CompactSize, "DELETE", new Vector2(40, 16), "ButtonKeyBRed", Lavender);
                delete.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Place(delete, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(40, 16));

                slots.Add(new SaveStatePanel.Slot
                {
                    root = slotRect.gameObject,
                    button = button,
                    hold = slotRect.gameObject.AddComponent<HoldGesture>(),
                    thumbnail = thumb,
                    time = time,
                    deleteButton = delete.GetComponent<Button>(),
                });
            }
            panel.slots = slots.ToArray();

            // RESTART under the saves: purple keycap (Art/Picker/ButtonKeyBPurple*,
            // the blue keycap in the picker's purples) so it stands apart from SAVE; tap twice (the label asks
            // "SURE?" first).
            var restartLine = UI("Restart Line", content);
            var restartLineElement = restartLine.gameObject.AddComponent<LayoutElement>();
            restartLineElement.minHeight = restartLineElement.preferredHeight = 20;
            var restart = KeyButton("Restart Button", restartLine, compact, CompactSize, "RESTART", new Vector2(44, 20), "ButtonKeyBPurple");
            Place(restart, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 20));
            panel.restartButton = restart.GetComponent<Button>();
            panel.restartLabel = restart.GetComponentInChildren<TMP_Text>();

            // LOCK: its own keycap under the saves frame (not inside it),
            // moving down as the frame grows with saves. Locked shows the
            // pressed cap.
            var lockRect = KeyButton("Lock Button", root, compact, CompactSize, "LOCK", new Vector2(44, 20));
            Place(lockRect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(44, 20));
            var lockButton = lockRect.gameObject.AddComponent<CabinetLockButton>();
            lockButton.background = lockRect.GetComponent<Image>();
            lockButton.label = lockRect.GetComponentInChildren<TMP_Text>();
            lockButton.unlockedSprite = Art("ButtonKeyB_Unpressed");
            lockButton.lockedSprite = Art("ButtonKeyB_Pressed");
            var below = lockRect.gameObject.AddComponent<StackBelow>();
            below.above = content;
            below.spacing = 6;
        }

        // Controllers: GamepadInput on the arcade controls (it also shows/hides
        // them per Settings), and the UI no longer reacts to controller
        // buttons (Cross would "click" whatever button was last tapped).
        static void SetupGamepad()
        {
            var router = Object.FindAnyObjectByType<SpatialEmulator.Controls.ArcadeTouchRouter>(FindObjectsInactive.Include);
            if (router)
            {
                var canvas = router.GetComponentInParent<Canvas>();
                var gamepad = canvas.GetComponent<SpatialEmulator.Controls.GamepadInput>();
                if (!gamepad) gamepad = Undo.AddComponent<SpatialEmulator.Controls.GamepadInput>(canvas.gameObject);
                gamepad.touchControls = canvas;
                gamepad.router = router;
                EditorUtility.SetDirty(gamepad);
            }
            var module = Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>(FindObjectsInactive.Include);
            if (module)
            {
                Undo.RecordObject(module, "No controller UI navigation");
                module.enableGamepadInput = false;
                module.enableJoystickInput = false;
                EditorUtility.SetDirty(module);
            }
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---- scene ----

        static Sprite ControlArt(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ControlsArtDir}/{name}.png");

        // A/B/C become arcade buttons (oval coloured caps in a bezel, black
        // letters, dropped into the bezel when held) and the floating joystick a neon ring with
        // the pack's arrows and a classic red ball knob. Sizes are in art
        // pixels (PixelArtSizer), matching the pixel HUD. Positions are NOT
        // touched - Wayne places the controls by hand in the scene.
        static void StyleArcadeControls(TMP_FontAsset arcade)
        {
            var router = Object.FindAnyObjectByType<SpatialEmulator.Controls.ArcadeTouchRouter>(FindObjectsInactive.Include);
            if (!router) return;
            var root = router.GetComponentInParent<Canvas>().transform;

            var buttons = new[]
            {
                ("Button A", "Red"),
                ("Button B", "Yellow"),
                ("Button C", "Green"),
            };
            foreach (var (name, color) in buttons)
            {
                var t = root.Find(name);
                if (!t) continue;
                var image = t.GetComponent<Image>();
                var label = t.GetComponentInChildren<TMP_Text>(true);
                var arcadeButton = t.GetComponent<SpatialEmulator.Controls.ArcadeButton>();
                Undo.RecordObjects(new Object[] { t, image, label, arcadeButton }, "Pixel arcade button");
                string letter = name.Substring(name.Length - 1);
                image.sprite = ControlArt("Button" + color);
                image.type = Image.Type.Simple;
                image.color = Color.white;
                // The letter is a pixel sprite (white, dark outline) - TMP's
                // bitmap font can't outline - so the text label is hidden.
                label.gameObject.SetActive(false);
                // An existing Glyph keeps Wayne's hand layout (he nudged and
                // scaled the letters); only a new one gets the default centring.
                var glyphTransform = t.Find("Glyph");
                bool newGlyph = !glyphTransform;
                var glyphRect = glyphTransform ? (RectTransform)glyphTransform : UI("Glyph", t);
                if (newGlyph) glyphRect.anchorMin = glyphRect.anchorMax = glyphRect.pivot = new Vector2(0.5f, 0.5f);
                var glyph = glyphRect.GetComponent<Image>();
                if (!glyph) glyph = glyphRect.gameObject.AddComponent<Image>();
                glyph.sprite = ControlArt("Letter" + letter);
                glyph.raycastTarget = false;
                var glyphSizer = glyphRect.GetComponent<PixelArtSizer>();
                if (!glyphSizer) glyphSizer = glyphRect.gameObject.AddComponent<PixelArtSizer>();
                glyphSizer.artSize = new Vector2(11, 11);
                // Centred on the cap's face, on whole art pixels (36 x 32 button,
                // face centre 18, 16.5 from its top-left).
                if (newGlyph)
                {
                    glyphSizer.useArtOffset = true;
                    glyphSizer.artOffset = new Vector2(0.5f, -0.5f);
                }
                arcadeButton.visual = image;
                arcadeButton.pressedSprite = ControlArt("Button" + color + "Pressed");
                // The cap sinks 2 px into its collar when held, and its letter with it.
                arcadeButton.pressedContent = glyphRect;
                arcadeButton.pressedDrop = 2f;
                arcadeButton.pressedLabel = null;
                arcadeButton.pressedLabelColor = Color.clear;
                var sizer = t.GetComponent<PixelArtSizer>();
                if (!sizer) sizer = Undo.AddComponent<PixelArtSizer>(t.gameObject);
                // 36 x 32 Neo Geo style button: shallow glossy cap on a raised
                // silver collar, edges shaded in their own hue (no black outline).
                sizer.artSize = new Vector2(36, 32);
                sizer.image = null;
                sizer.label = null;
                arcadeButton.sizer = sizer;
                foreach (var o in new Object[] { t, image, label, arcadeButton, sizer, glyph, glyphSizer }) EditorUtility.SetDirty(o);
            }

            // Joystick: zone narrowed so it stays clear of A.
            var zone = root.Find("DPad Zone");
            var dpad = zone ? zone.GetComponent<SpatialEmulator.Controls.ArcadeDPad>() : null;
            if (dpad && dpad.pad)
            {
                Undo.RecordObjects(new Object[] { zone, dpad }, "Pixel joystick");
                ((RectTransform)zone).sizeDelta = new Vector2(430, 650);
                var pad = dpad.pad;
                var padSizer = pad.GetComponent<PixelArtSizer>();
                if (!padSizer) padSizer = Undo.AddComponent<PixelArtSizer>(pad.gameObject);
                padSizer.artSize = new Vector2(60, 60);
                dpad.padSizer = padSizer;

                var baseRect = (RectTransform)pad.Find("Base");
                baseRect.anchorMin = Vector2.zero;
                baseRect.anchorMax = Vector2.one;
                baseRect.offsetMin = baseRect.offsetMax = Vector2.zero;
                var baseImage = baseRect.GetComponent<Image>();
                baseImage.sprite = ControlArt("JoyRing");
                baseImage.color = Color.white;
                baseImage.raycastTarget = false;

                // Arrows just inside the ring; they light up in their direction.
                var arrowColor = new Color(1f, 1f, 1f, 0.55f);
                Image Arrow(string name, string sprite, Vector2 offset, Vector3 scale)
                {
                    var existing = baseRect.Find(name);
                    var rect = existing ? (RectTransform)existing : UI(name, baseRect);
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.localScale = scale;
                    var img = rect.GetComponent<Image>();
                    if (!img) img = rect.gameObject.AddComponent<Image>();
                    img.sprite = ControlArt(sprite);
                    img.color = arrowColor;
                    img.raycastTarget = false;
                    var sizer = rect.GetComponent<PixelArtSizer>();
                    if (!sizer) sizer = rect.gameObject.AddComponent<PixelArtSizer>();
                    sizer.artSize = new Vector2(img.sprite.rect.width, img.sprite.rect.height);
                    sizer.useArtOffset = true;
                    sizer.artOffset = offset;
                    return img;
                }
                dpad.upKey = Arrow("Arrow Up", "JoyArrowUp", new Vector2(0, 21), Vector3.one);
                dpad.downKey = Arrow("Arrow Down", "JoyArrowUp", new Vector2(0, -21), new Vector3(1, -1, 1));
                dpad.rightKey = Arrow("Arrow Right", "JoyArrowRight", new Vector2(21, 0), Vector3.one);
                dpad.leftKey = Arrow("Arrow Left", "JoyArrowRight", new Vector2(-21, 0), new Vector3(-1, 1, 1));
                dpad.pressedTint = new Color(1f, 1f, 1f, 1.8f); // brighten (alpha 0.55 -> 1)

                if (dpad.knob)
                {
                    var knobImage = dpad.knob.GetComponent<Image>();
                    knobImage.sprite = ControlArt("JoyKnob");
                    knobImage.color = Color.white;
                    knobImage.raycastTarget = false;
                    var knobSizer = dpad.knob.GetComponent<PixelArtSizer>();
                    if (!knobSizer) knobSizer = Undo.AddComponent<PixelArtSizer>(dpad.knob.gameObject);
                    knobSizer.artSize = new Vector2(28, 28); // classic red ball top (JoyKnob.png is 28 px)
                    dpad.knobTravel = 0.45f;                 // keeps the knob inside the ring
                    // (localScale left alone - Wayne tunes the knob by hand)
                    dpad.knob.SetAsLastSibling();
                }
                dpad.showAtRest = true;
                // Rests beside A, level with it, so the right arrow points at it.
                var buttonA = root.Find("Button A");
                dpad.restBeside = buttonA ? (RectTransform)buttonA : null;
                dpad.restGapArt = 4f;
                foreach (var o in new Object[] { zone, dpad, pad, baseRect, baseImage }) EditorUtility.SetDirty(o);
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
        }

        // ArcadeControls' Start button becomes a Blue keycap like the HUD's,
        // in place (its hand-set position is kept). It stays an ArcadeButton
        // on the controls canvas so the multi-touch router still drives it;
        // PixelArtSizer matches its pixels to the pixel HUD's.
        static void StyleStartButton(TMP_FontAsset compact)
        {
            var router = Object.FindAnyObjectByType<SpatialEmulator.Controls.ArcadeTouchRouter>(FindObjectsInactive.Include);
            var start = router ? router.GetComponentInParent<Canvas>().transform.Find("Start") : null;
            if (!start) { Debug.LogWarning("[GamePickerBuilder] no Start button found"); return; }

            var image = start.GetComponent<Image>();
            var label = start.GetComponentInChildren<TMP_Text>(true);
            var arcade = start.GetComponent<SpatialEmulator.Controls.ArcadeButton>();
            Undo.RecordObjects(new Object[] { image, label, arcade, label.rectTransform, start }, "Pixel Start button");
            image.sprite = Art("ButtonKeyB_Unpressed");
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            label.font = compact;
            label.color = Ink;
            label.text = "START";
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            arcade.visual = image;
            arcade.pressedSprite = Art("ButtonKeyB_Pressed");
            arcade.pressedContent = label.rectTransform;

            var sizer = start.GetComponent<PixelArtSizer>();
            if (!sizer) sizer = Undo.AddComponent<PixelArtSizer>(start.gameObject);
            sizer.artSize = new Vector2(36, 20);
            sizer.image = image;
            sizer.label = label;
            sizer.labelArtSize = CompactSize;
            arcade.sizer = sizer;
            foreach (var o in new Object[] { image, label, arcade, sizer }) EditorUtility.SetDirty(o);

            var scene = start.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void PlaceInScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var old in Object.FindObjectsByType<GamePicker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "GamePicker"; // NativeFilePicker's UnitySendMessage target

            var manipulator = Object.FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
            var hints = Object.FindAnyObjectByType<CabinetHints>(FindObjectsInactive.Include);
            var hint = instance.transform.Find("Hint Banner");
            if (hints && hint)
            {
                Undo.RecordObject(hints, "Pixel hint banner");
                hints.label = hint.GetComponentInChildren<TMP_Text>(true);
                hints.group = hint.GetComponent<CanvasGroup>();
                hints.secondLineColor = NeonBlue;
                EditorUtility.SetDirty(hints);
            }
            var delete = instance.transform.Find("Delete Cabinet Button");
            if (manipulator && delete)
            {
                Undo.RecordObject(manipulator, "Pixel delete button");
                manipulator.deleteButton = delete.GetComponent<Button>();
                EditorUtility.SetDirty(manipulator);
            }

            var controls = Object.FindAnyObjectByType<SpatialEmulator.Controls.ArcadeTouchRouter>(FindObjectsInactive.Include);
            if (controls)
                foreach (string name in ReplacedControls)
                {
                    var old = controls.GetComponentInParent<Canvas>().transform.Find(name);
                    if (old && old.gameObject.activeSelf)
                    {
                        Undo.RecordObject(old.gameObject, "Hide replaced control");
                        old.gameObject.SetActive(false);
                    }
                }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
