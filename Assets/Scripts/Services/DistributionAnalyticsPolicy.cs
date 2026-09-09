using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Roloc.Services
{
    /// <summary>Usage analytics is permitted only in distribution builds running on a physical iOS device.</summary>
    public static class DistributionAnalyticsPolicy
    {
        public static bool BuildEligible
        {
            get
            {
#if RING_RUSH_DISTRIBUTION && UNITY_IOS && !UNITY_EDITOR && !DEVELOPMENT_BUILD
                bool physical;
                try { physical = RingRushIsPhysicalDevice(); }
                catch (Exception) { return false; } // Missing native bridge is never permission to capture.
                return Evaluate(true, true, false, false, Application.platform == RuntimePlatform.IPhonePlayer,
                    physical, Debug.isDebugBuild);
#else
                return false;
#endif
            }
        }
        public static bool Evaluate(bool distribution, bool ios, bool editor, bool development,
            bool iPhonePlayer, bool physicalDevice, bool debugBuild) => distribution && ios && !editor
                && !development && iPhonePlayer && physicalDevice && !debugBuild;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool RingRushIsPhysicalDevice();
#endif
    }
}
