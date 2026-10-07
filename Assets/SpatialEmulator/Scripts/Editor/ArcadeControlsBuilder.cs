// ArcadeControlsBuilder.cs — builds the on-screen arcade controls (floating
// 8-way joystick, A/B/C, Coin, Start, Lock, and the cabinet's Delete button) from
// Ultimate Mobile Controls Kit artwork, saves them as
// Assets/SpatialEmulator/Prefabs/ArcadeControls.prefab and puts an instance
// in the open scene, then wires the cabinet components on the template's
// Object Spawner (SingleCabinetGate, CabinetManipulator, CabinetPlaneFader,
// CabinetHints) and turns off the template's welcome card and tutorial,
// which CabinetHints replaces.
// Only the kit's sprites are used: its controller scripts drive character
// "Motors" one direction at a time and read the legacy Input class, which
// this Input-System-only project can't use, so input comes from
// ArcadeTouchRouter driving ArcadeDPad/ArcadeButton.
//
// Tools > Spatial Emulator > Build Arcade Controls. Rebuilding replaces the
// prefab and the scene instance, so hand edits to either are lost.

using SpatialEmulator.Controls;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Templates.AR;

namespace SpatialEmulator.Editor
{
    public static class ArcadeControlsBuilder
    {
        const string KitSprites = "Assets/DowntownGameStudio/UltimateMobileControlsKit/Sprites/";
        const string CoinSpritePath = "Assets/SpatialEmulator/Art/CoinButton.png";
        const string TemplateSprites = "Assets/Samples/XR Interaction Toolkit/3.5.1/AR Starter Assets/DemoAssets/Sprites/";
        const string GizmoShaderPath = "Assets/SpatialEmulator/Shaders/GizmoOverlay.shader";
        const string PrefabPath = "Assets/SpatialEmulator/Prefabs/ArcadeControls.prefab";
        const string RootName = "ArcadeControls";
        static readonly bool ShowDPadZone = false; // tint the D-pad's touch zone, for tuning the layout

        // Portrait reference layout, scaled with screen width. Everything stays
        // clear of the AR template's own buttons: Create at (540, 278) and
        // Delete at (746, 278) in these units on an iPhone 13 Pro Max.
        static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);
        const float PadScale = 0.75f; // the joystick; 1 = the original 420-unit pad

        [MenuItem("Tools/Spatial Emulator/Build Arcade Controls")]
        public static void Build()
        {
            var existing = GameObject.Find(RootName);
            if (existing) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the AR template's UI canvas (order 0)
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            // Multi-touch: reads every finger itself, since the scene's UI input
            // module (XRI) only delivers one pointer under the Input System.
            var router = root.AddComponent<ArcadeTouchRouter>();

            BuildDPad(root.transform);

            var round = Sprite(KitSprites + "Ability Button/Cooldown Button V2.png");
            // Neo Geo panel colours; libretro-mame maps RetroPad B/A/Y to MAME Buttons 1/2/3.
            BuildButton(root.transform, "Button A", "A", RetroPadButton.B, round, new Color(0.86f, 0.22f, 0.22f), Corner.BottomRight, new Vector2(-460, 470), 170, 3);
            BuildButton(root.transform, "Button B", "B", RetroPadButton.A, round, new Color(0.96f, 0.78f, 0.16f), Corner.BottomRight, new Vector2(-290, 560), 170, 3);
            BuildButton(root.transform, "Button C", "C", RetroPadButton.Y, round, new Color(0.22f, 0.70f, 0.32f), Corner.BottomRight, new Vector2(-120, 650), 170, 3);
            // Coin and Start in a row under A/B/C.
            BuildCoin(root.transform, Corner.BottomRight, new Vector2(-400, 250));
            BuildButton(root.transform, "Start", "START", RetroPadButton.Start, round, new Color(0.62f, 0.64f, 0.68f), Corner.BottomRight, new Vector2(-220, 260), 150, 3);
            var deleteButton = BuildDeleteButton(root.transform);
            var (hintLabel, hintGroup) = BuildHintBanner(root.transform);
            var lockButton = BuildLockButton(root.transform, round);

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
            ConnectCabinetComponents(router, deleteButton, hintLabel, hintGroup);
            lockButton.manipulator = Object.FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
            EditorUtility.SetDirty(lockButton);
            DisableTemplateOnboarding();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[ArcadeControlsBuilder] Built {PrefabPath} and placed it in {root.scene.name}.");
        }

        // The template spawns cabinets from its Object Spawner. The components
        // there allow one cabinet at a time, handle selecting, moving and
        // deleting it, and fade the plane visuals around that.
        static void ConnectCabinetComponents(ArcadeTouchRouter router, Button deleteButton, TMP_Text hintLabel, CanvasGroup hintGroup)
        {
            var spawner = Object.FindAnyObjectByType<ObjectSpawner>(FindObjectsInactive.Include);
            if (!spawner)
            {
                Debug.LogWarning("[ArcadeControlsBuilder] No ObjectSpawner in the scene; cabinet components not added.");
                return;
            }
            var host = spawner.gameObject;
            var gate = GetOrAdd<SingleCabinetGate>(host);
            gate.spawner = spawner;
            gate.spawnTrigger = spawner.GetComponent<ARInteractorSpawnTrigger>();
            gate.router = router;

            var manipulator = GetOrAdd<CabinetManipulator>(host);
            manipulator.gate = gate;
            manipulator.router = router;
            manipulator.deleteButton = deleteButton;
            manipulator.gizmoShader = AssetDatabase.LoadAssetAtPath<Shader>(GizmoShaderPath);
            if (!manipulator.gizmoShader) Debug.LogError($"[ArcadeControlsBuilder] Missing shader: {GizmoShaderPath}");

            var planeManager = Object.FindAnyObjectByType<ARPlaneManager>(FindObjectsInactive.Include);
            var fader = GetOrAdd<CabinetPlaneFader>(host);
            fader.planeManager = planeManager;
            fader.gate = gate;
            fader.manipulator = manipulator;

            var hints = GetOrAdd<CabinetHints>(host);
            hints.gate = gate;
            hints.manipulator = manipulator;
            hints.planeManager = planeManager;
            hints.label = hintLabel;
            hints.group = hintGroup;

            EditorUtility.SetDirty(gate);
            EditorUtility.SetDirty(manipulator);
            EditorUtility.SetDirty(fader);
            EditorUtility.SetDirty(hints);
        }

        // The template's welcome card leads into a tutorial for XRI's gestures,
        // which this project replaced; CabinetHints does the guiding instead.
        // The GoalManager's Continue button was also what switched on the
        // template's menu (Create, options, its own Delete), so that stays off.
        static void DisableTemplateOnboarding()
        {
            var goals = Object.FindAnyObjectByType<GoalManager>(FindObjectsInactive.Include);
            if (!goals) return;
            var serialized = new SerializedObject(goals);
            if (serialized.FindProperty("m_GreetingPrompt").objectReferenceValue is GameObject greeting)
            {
                greeting.SetActive(false);
                EditorUtility.SetDirty(greeting);
            }
            var steps = serialized.FindProperty("m_StepList");
            for (int i = 0; i < steps.arraySize; i++)
                if (steps.GetArrayElementAtIndex(i).FindPropertyRelative("stepObject").objectReferenceValue is GameObject step)
                {
                    step.SetActive(false);
                    EditorUtility.SetDirty(step);
                }
            goals.enabled = false;
            EditorUtility.SetDirty(goals);
        }

        static T GetOrAdd<T>(GameObject host) where T : Component
            => host.TryGetComponent<T>(out var component) ? component : host.AddComponent<T>();

        static void BuildDPad(Transform parent)
        {
            // The zone is the pad's touch area: the pad appears wherever a thumb
            // lands in it. It stops short of the template's Create button.
            var zone = Rect("DPad Zone", parent, Corner.BottomLeft, Vector2.zero, new Vector2(500, 650));
            zone.pivot = Vector2.zero;
            zone.anchoredPosition = Vector2.zero;
            if (ShowDPadZone)
            {
                var tint = zone.gameObject.AddComponent<Image>();
                tint.color = new Color(1f, 0.6f, 0.2f, 0.18f);
                tint.raycastTarget = false;
            }
            var dpad = zone.gameObject.AddComponent<ArcadeDPad>();

            var pad = new GameObject("Pad", typeof(RectTransform)).GetComponent<RectTransform>();
            pad.SetParent(zone, false);
            pad.anchorMin = pad.anchorMax = pad.pivot = new Vector2(0.5f, 0.5f);
            pad.sizeDelta = new Vector2(420, 420) * PadScale;
            pad.localPosition = new Vector2(220, 330); // editor preview spot; hidden until touched at runtime
            dpad.pad = pad;

            // Arcade joystick from the kit: a dark base with arrow cut-outs and a
            // red ball-top knob that follows the thumb.
            var baseImage = new GameObject("Base", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var baseRect = baseImage.rectTransform;
            baseRect.SetParent(pad, false);
            baseRect.anchorMin = Vector2.zero;
            baseRect.anchorMax = Vector2.one;
            baseRect.offsetMin = baseRect.offsetMax = Vector2.zero;
            baseImage.sprite = Sprite(KitSprites + "Joystick/Background/White Background With Arrow holes.png");
            baseImage.color = new Color(0.14f, 0.14f, 0.16f, 0.8f);
            baseImage.raycastTarget = false;

            var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            knob.rectTransform.SetParent(pad, false);
            knob.rectTransform.sizeDelta = Vector2.one * 420f * PadScale * 1.2f;
            knob.sprite = Sprite(KitSprites + "Joystick/Button/Button1.png");
            knob.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            knob.raycastTarget = false;
            dpad.knob = knob.rectTransform;
            dpad.knobTravel = 1f; // the knob tracks the thumb right out to the base's edge
            dpad.deadZone = 0.15f;
        }

        static void BuildButton(Transform parent, string name, string label, RetroPadButton retro, Sprite sprite, Color color,
                                Corner corner, Vector2 position, float size, int minHoldFrames)
        {
            var rect = Rect(name, parent, corner, position, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            var button = rect.gameObject.AddComponent<ArcadeButton>();
            button.button = retro;
            button.minHoldFrames = minHoldFrames;
            button.visual = image;

            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var textRect = (RectTransform)text.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = label.Length == 1 ? size * 0.42f : size * 0.2f;
            text.color = new Color(0.12f, 0.12f, 0.14f, 1f);
            text.raycastTarget = false;
        }

        // The template's trash icon on a red circle, top center, away
        // from the play controls. CabinetManipulator shows it only while the
        // cabinet is selected and wires its click.
        static Button BuildDeleteButton(Transform parent)
        {
            const float size = 130f;
            var rect = new GameObject("Delete Cabinet", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, -260);
            rect.sizeDelta = new Vector2(size, size);
            var background = rect.GetComponent<Image>();
            background.sprite = Sprite(TemplateSprites + "Button - Circular BG.png");
            background.color = new Color(0.86f, 0.22f, 0.22f, 0.95f); // the icon is white

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            icon.SetParent(rect, false);
            icon.sizeDelta = new Vector2(size * 0.62f, size * 0.62f);
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = Sprite(TemplateSprites + "Icon - Delete.png");
            iconImage.raycastTarget = false;

            var button = rect.GetComponent<Button>();
            button.targetGraphic = background;
            rect.gameObject.SetActive(false);
            return button;
        }

        // Top right: locks the cabinet against
        // selection so up-close play can't bring up the gizmo.
        static CabinetLockButton BuildLockButton(Transform parent, Sprite round)
        {
            const float size = 150f;
            var rect = new GameObject("Lock Cabinet", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CabinetLockButton)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-130, -280);
            rect.sizeDelta = new Vector2(size, size);
            var background = rect.GetComponent<Image>();
            background.sprite = round;
            rect.GetComponent<Button>().targetGraphic = background;

            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var textRect = (RectTransform)text.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = size * 0.17f;
            text.color = new Color(0.12f, 0.12f, 0.14f, 1f);
            text.text = "LOCK";
            text.raycastTarget = false;

            var lockButton = rect.GetComponent<CabinetLockButton>();
            lockButton.background = background;
            lockButton.label = text;
            return lockButton;
        }

        // CabinetHints' banner, below the top row of buttons. Nothing in it
        // takes touches, so taps pass through to the scene.
        static (TMP_Text, CanvasGroup) BuildHintBanner(Transform parent)
        {
            var rect = new GameObject("Hint Banner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -410);
            rect.sizeDelta = new Vector2(960, 150);
            var background = rect.GetComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.07f, 0.62f);
            background.raycastTarget = false;
            var group = rect.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var textRect = (RectTransform)text.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(30, 12);
            textRect.offsetMax = new Vector2(-30, -12);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 40;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return (text, group);
        }

        // Pixel-art coin above a slot. Pressing it drops the coin into the
        // slot (CoinInsertAnimation); a RectMask2D ending at the slot line
        // hides it as it goes in.
        static void BuildCoin(Transform parent, Corner corner, Vector2 position)
        {
            const float width = 150f, height = 190f, coinSize = 140f, slotY = -70f;
            var rect = Rect("Coin", parent, corner, position, new Vector2(width, height));
            var button = rect.gameObject.AddComponent<ArcadeButton>();
            button.button = RetroPadButton.Select;
            button.minHoldFrames = 6; // some games miss shorter coin pulses
            button.pressedScale = 1f; // the drop animation is the feedback

            var clip = new GameObject("Coin Clip", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            clip.SetParent(rect, false);
            clip.anchorMin = new Vector2(0, 0);
            clip.anchorMax = new Vector2(1, 1);
            clip.offsetMin = new Vector2(0, height * 0.5f + slotY); // bottom edge on the slot line
            clip.offsetMax = Vector2.zero;
            float clipCenterY = (slotY + height * 0.5f) * 0.5f;

            var coin = new GameObject("Coin Art", typeof(RectTransform), typeof(CanvasGroup), typeof(Image)).GetComponent<RectTransform>();
            coin.SetParent(clip, false);
            coin.sizeDelta = new Vector2(coinSize, coinSize);
            coin.anchoredPosition = new Vector2(0, 20f - clipCenterY); // 20 above the button's center
            var art = coin.GetComponent<Image>();
            art.sprite = PixelSprite(CoinSpritePath);
            art.raycastTarget = false;

            var slot = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<RectTransform>();
            slot.SetParent(rect, false);
            slot.sizeDelta = new Vector2(110, 12);
            slot.anchoredPosition = new Vector2(0, slotY);
            var slotImage = slot.GetComponent<Image>();
            slotImage.color = new Color(0.05f, 0.05f, 0.06f, 1f);
            slotImage.raycastTarget = false;
            var rim = slot.GetComponent<Outline>();
            rim.effectColor = new Color(0.42f, 0.42f, 0.45f, 1f);
            rim.effectDistance = new Vector2(3, 3);

            var anim = rect.gameObject.AddComponent<CoinInsertAnimation>();
            anim.button = button;
            anim.coin = coin;
            anim.coinGroup = coin.GetComponent<CanvasGroup>();
        }

        enum Corner { BottomLeft, BottomRight, TopLeft }

        static Vector2 Anchor(Corner corner)
            => corner == Corner.BottomLeft ? new Vector2(0, 0) : corner == Corner.BottomRight ? new Vector2(1, 0) : new Vector2(0, 1);

        static RectTransform Rect(string name, Transform parent, Corner corner, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Anchor(corner);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Sprite Sprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) Debug.LogError($"[ArcadeControlsBuilder] Missing sprite: {path}");
            return sprite;
        }

        // Pixel art: sprite, point filtering, no mipmaps or compression.
        static Sprite PixelSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point ||
                 importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return Sprite(path);
        }
    }
}
