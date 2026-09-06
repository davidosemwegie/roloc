using System;
using System.IO;
using Roloc.Core;
using Roloc.Presentation;
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

        [MenuItem("ROLOC/Set up game")]
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
            camera.backgroundColor = new Color32(246, 245, 241, 255); camera.transform.position = new Vector3(0, 0, -10);
            var root = new GameObject("ROLOC 2");
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

        [MenuItem("ROLOC/Configure mobile build")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Osazi"; PlayerSettings.productName = "ROLOC 2";
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
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.requiresFullScreen = true;
            QualitySettings.vSyncCount = 0; QualitySettings.antiAliasing = 0;
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        [MenuItem("ROLOC/Build iPhone development project")]
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
            Build(BuildTarget.StandaloneOSX, Argument("-buildOutput") ?? "Builds/Mac/ROLOC 2.app", BuildOptions.Development);
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
