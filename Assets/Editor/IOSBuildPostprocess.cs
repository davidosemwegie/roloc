#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Roloc.Editor
{
    public static class IOSBuildPostprocess
    {
        [PostProcessBuild(100)]
        public static void Complete(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "Security.framework", false);
            project.SetBuildProperty(project.GetUnityMainTargetGuid(), "DEVELOPMENT_TEAM", PlayerSettings.iOS.appleDeveloperTeamID);
            project.SetBuildProperty(project.GetUnityFrameworkTargetGuid(), "DEVELOPMENT_TEAM", PlayerSettings.iOS.appleDeveloperTeamID);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
            project.WriteToFile(projectPath);
            string infoPath = Path.Combine(path, "Info.plist");
            var info = new PlistDocument(); info.ReadFromFile(infoPath);
            info.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            info.root.SetString("NSUserTrackingUsageDescription", "Your permission helps us show relevant ads and measure their performance.");
            var ads = UnityEngine.Resources.Load<Roloc.Services.AdsConfiguration>("AdsConfiguration");
            if (ads && ads.HasConsentAppId)
                info.root.SetString("GADApplicationIdentifier", ads.IosConsentAppId);
            else
                info.root.values.Remove("GADApplicationIdentifier");
            info.WriteToFile(infoPath);
        }
    }
}
#endif
