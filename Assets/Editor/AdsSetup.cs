using Roloc.Services;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Roloc.Editor
{
    public static class AdsSetup
    {
        [MenuItem("Ring Rush/Advertising configuration")]
        public static void SelectConfiguration()
        {
            const string path = "Assets/Resources/AdsConfiguration.asset";
            var configuration = AssetDatabase.LoadAssetAtPath<AdsConfiguration>(path);
            if (!configuration)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                configuration = ScriptableObject.CreateInstance<AdsConfiguration>();
                AssetDatabase.CreateAsset(configuration, path);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = configuration;
            EditorGUIUtility.PingObject(configuration);
            Debug.Log("Configure your published HTTPS privacy policy and LevelPlay iOS app/ad-unit identifiers. "
                + "Use Unity Ads as the demand network and dashboard test devices during development. "
                + "Keep LevelPlay automatic initialization disabled: Ring Rush initializes after privacy choices.");
        }
    }

    // A fresh UPM install can replace these files with newer native versions asynchronously.
    // Fail explicitly instead of silently shipping a different SDK than the reviewed lock.
    public sealed class AdsDependencyValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            Check("IronSourceSDKDependencies.xml", "9.5.0");
            Check("ISUnityAdsAdapterDependencies.xml", "5.11.0.0");
        }
        static void Check(string file, string expected)
        {
            var path = Path.Combine("Assets/LevelPlay/Editor", file);
            if (!File.Exists(path) || XDocument.Load(path).Root?.Element("unityversion")?.Value != expected)
                throw new BuildFailedException("LevelPlay changed " + file + ". Restore the committed dependency XML after package import, then build again. Expected " + expected + ".");
        }
    }
}
