using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Roloc.Services.Tests
{
    public sealed class DistributionBuildTests
    {
        [TestCase(BuildTarget.iOS, BuildOptions.None, "com.clearjar.ringrush", iOSSdkVersion.DeviceSDK, true, true)]
        [TestCase(BuildTarget.iOS, BuildOptions.None, "com.clearjar.ringrush", iOSSdkVersion.DeviceSDK, false, false)]
        [TestCase(BuildTarget.iOS, BuildOptions.Development, "com.clearjar.ringrush", iOSSdkVersion.DeviceSDK, true, false)]
        [TestCase(BuildTarget.iOS, BuildOptions.None, "com.clearjar.ringrush", iOSSdkVersion.SimulatorSDK, true, false)]
        [TestCase(BuildTarget.iOS, BuildOptions.None, "com.clearjar.ringrush.dev", iOSSdkVersion.DeviceSDK, true, false)]
        [TestCase(BuildTarget.StandaloneOSX, BuildOptions.None, "com.clearjar.ringrush", iOSSdkVersion.DeviceSDK, true, false)]
        public void OnlyExplicitStoreDeviceReleaseExportGetsAnalyticsDefine(BuildTarget target, BuildOptions options,
            string identifier, iOSSdkVersion sdk, bool distributionExport, bool expected)
        {
            var builder = Type.GetType("Roloc.Editor.ProjectBuilder, Roloc.Editor", true);
            var method = builder.GetMethod("DistributionDefines", BindingFlags.Static | BindingFlags.NonPublic);
            var defines = (string[])method.Invoke(null, new object[] { target, options, identifier, sdk, distributionExport });
            Assert.That(Array.IndexOf(defines, "RING_RUSH_DISTRIBUTION") >= 0, Is.EqualTo(expected));
            Assert.That(Array.IndexOf(defines, "RING_RUSH_DISTRIBUTION_ADS") >= 0, Is.EqualTo(expected));
        }
    }
}
