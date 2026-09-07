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
            project.SetBuildProperty(project.GetUnityMainTargetGuid(), "DEVELOPMENT_TEAM", "TYU4JMX349");
            project.SetBuildProperty(project.GetUnityFrameworkTargetGuid(), "DEVELOPMENT_TEAM", "TYU4JMX349");
            project.WriteToFile(projectPath);
            string infoPath = Path.Combine(path, "Info.plist");
            var info = new PlistDocument(); info.ReadFromFile(infoPath);
            info.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            info.WriteToFile(infoPath);
        }
    }
}
#endif
