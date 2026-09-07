using System;
using System.IO;
using Roloc.Core;
using Roloc.Presentation;
using Roloc.Services;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Roloc.Editor
{
    public static class ProjectBuilder
    {
        public const string ScenePath = "Assets/Scenes/Roloc.unity";

        [MenuItem("Ring Rush/Set up game")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before setting up the scene.");
            Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Settings"); AssetDatabase.Refresh();
            var difficulty = AssetDatabase.LoadAssetAtPath<DifficultySettings>("Assets/Settings/Difficulty.asset");
            if (!difficulty)
            {
                difficulty = ScriptableObject.CreateInstance<DifficultySettings>();
                AssetDatabase.CreateAsset(difficulty, "Assets/Settings/Difficulty.asset");
            }
            Configure();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(240, 246, 252, 255); camera.transform.position = new Vector3(0, 0, -10);
            var root = new GameObject("Ring Rush");
            var game = root.AddComponent<RolocGame>();
            game.difficulty = difficulty;
            game.backgroundMusic = Audio("playing.wav", true);
            game.matchSound = Audio("match.wav", false);
            game.gameOverSound = Audio("game-over.wav", false);
            game.typeface = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            game.puckPrefab = Piece("Puck", false);
            game.ringPrefab = Piece("Ring", true);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.RemoveConfigObject("com.unity.input.settings.actions");
            AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            AssetDatabase.DeleteAsset("Assets/InputSystem_Actions.inputactions");
            if (File.Exists("Assets/.empty")) File.Delete("Assets/.empty");
            AssetDatabase.SaveAssets();
            Debug.Log("ROLOC ready. Open Assets/Scenes/Roloc.unity and press Play.");
        }

        static GameObject Piece(string name, bool ring)
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing;
            var piece = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SoftShape));
            var shape = piece.GetComponent<SoftShape>();
            shape.kind = ring ? SoftShape.Shape.Ring : SoftShape.Shape.Disc;
            shape.color = RolocGame.Palette[0]; shape.raycastTarget = !ring;
            ((RectTransform)piece.transform).sizeDelta = Vector2.one * (ring ? 137 : 79);
            if (!ring) { piece.AddComponent<CanvasGroup>(); piece.AddComponent<PuckView>(); }
            var prefab = PrefabUtility.SaveAsPrefabAsset(piece, path);
            UnityEngine.Object.DestroyImmediate(piece); return prefab;
        }

        static AudioClip Audio(string name, bool music)
        {
            string path = "Assets/Audio/" + name;
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (!importer) throw new FileNotFoundException("Original ROLOC audio is missing", path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = 1;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false; importer.loadInBackground = music;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        [MenuItem("Ring Rush/Configure mobile build")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Osazi"; PlayerSettings.productName = "Ring Rush";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 400; PlayerSettings.defaultScreenHeight = 860;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.osazi.roloc.unitydev");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.osazi.roloc.unitydev");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.buildNumber = Environment.GetEnvironmentVariable("RING_RUSH_BUILD_NUMBER") ?? PlayerSettings.iOS.buildNumber;
            PlayerSettings.iOS.appleDeveloperTeamID = "TYU4JMX349";
            ConfigureDaily();
            var settings = AssetDatabase.LoadAssetAtPath<DifficultySettings>("Assets/Settings/Difficulty.asset");
            if (settings)
            {
                if (settings.Variations == null) settings.Variations = new VariationSettings();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            const string palettesPath = "Assets/Resources/VariationPalettes.asset";
            var palettes = AssetDatabase.LoadAssetAtPath<VariationPalettes>(palettesPath);
            if (!palettes)
            {
                palettes = ScriptableObject.CreateInstance<VariationPalettes>();
                AssetDatabase.CreateAsset(palettes, palettesPath);
                AssetDatabase.SaveAssets();
            }
            if (!VariationPalettes.IsValid(palettes.Palettes))
                throw new InvalidOperationException("Variation palettes require six sets of four distinct opaque colors.");
            PlayerSettings.iOS.requiresFullScreen = true;
            QualitySettings.vSyncCount = 0; QualitySettings.antiAliasing = 0;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            ConfigureBrandAssets();
            ConfigureAppIcon();
        }

        public static void ConfigureDaily()
        {
            string url = Environment.GetEnvironmentVariable("RING_RUSH_CONVEX_URL");
            string code = Environment.GetEnvironmentVariable("RING_RUSH_CLOSED_TEST_CODE");
            if (string.IsNullOrWhiteSpace(url)) return;
            const string path = "Assets/Resources/DailyConnection.asset";
            var config = AssetDatabase.LoadAssetAtPath<DailyConnection>(path);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<DailyConnection>();
                AssetDatabase.CreateAsset(config, path);
            }
            config.url = url; config.closedTestCode = code ?? "";
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        }

        public static void BuildTestFlight()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RING_RUSH_BUILD_NUMBER")))
                throw new InvalidOperationException("Set a unique RING_RUSH_BUILD_NUMBER for TestFlight.");
            Configure();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            Build(BuildTarget.iOS, Argument("-buildOutput") ?? "Builds/iOS", BuildOptions.None);
        }

        static void ConfigureBrandAssets()
        {
            const string logoPath = "Assets/Resources/Brand/RingRushLogo.png";
            AssetDatabase.ImportAsset(logoPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(logoPath) as TextureImporter;
            if (!importer) throw new FileNotFoundException("Ring Rush logo is missing", logoPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        [MenuItem("Ring Rush/Configure app icon")]
        public static void ConfigureAppIcon()
        {
            const string path = "Assets/Art/RingRushIcon.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new FileNotFoundException("ROLOC app icon is missing", path);
            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            int assigned = 0;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var slot in slots) { slot.SetTexture(icon); assigned++; }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, slots);
            }
            if (assigned == 0) throw new InvalidOperationException("No iOS app icon slots are available.");
            AssetDatabase.SaveAssets();
            Debug.Log("ROLOC app icon configured: default icon and " + assigned + " iOS slots.");
        }

        [MenuItem("Ring Rush/Build iPhone development project")]
        public static void BuildIOS()
        {
            Configure();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            Build(BuildTarget.iOS, Argument("-buildOutput") ?? "Builds/iOS", BuildOptions.Development);
        }

        public static void BuildSimulator()
        {
            Configure();
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            try { Build(BuildTarget.iOS, Argument("-buildOutput") ?? "Builds/iOSSimulator", BuildOptions.Development); }
            finally { PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK; }
        }

        public static void BuildMac()
        {
            Configure();
            Build(BuildTarget.StandaloneOSX, Argument("-buildOutput") ?? "Builds/Mac/Ring Rush.app", BuildOptions.Development);
        }

        static void Build(BuildTarget target, string output, BuildOptions options)
        {
            if (!File.Exists(ScenePath)) Setup();
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = target, locationPathName = output, options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("ROLOC build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
            Debug.Log("ROLOC build succeeded: " + output);
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
