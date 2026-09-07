using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Roloc.Services
{
    /// <summary>iPhone platform features. Editor secrets live only in memory.</summary>
    public static class NativeServices
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern IntPtr RRReadSecret(string key);
        [DllImport("__Internal")] static extern int RRSaveSecret(string key, string value);
        [DllImport("__Internal")] static extern void RRFreeString(IntPtr value);
        [DllImport("__Internal")] static extern int RRReduceMotion();
        [DllImport("__Internal")] static extern void RRHaptic(int strength);
        [DllImport("__Internal")] static extern void RRShare(string text, string imagePath);
#else
        static readonly Dictionary<string, string> secrets = new Dictionary<string, string>();
#endif
        public static string ReadSecret(string key)
        {
#if UNITY_IOS && !UNITY_EDITOR
            IntPtr pointer = RRReadSecret(key);
            try { return pointer == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(pointer); }
            finally { if (pointer != IntPtr.Zero) RRFreeString(pointer); }
#else
            return secrets.TryGetValue(key, out var value) ? value : "";
#endif
        }
        public static bool SaveSecret(string key, string value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return RRSaveSecret(key, value ?? "") != 0;
#else
            secrets[key] = value ?? "";
            return true;
#endif
        }
        public static bool ReduceMotion
        {
            get {
#if UNITY_IOS && !UNITY_EDITOR
                return RRReduceMotion() != 0;
#else
                return false;
#endif
            }
        }
        public static void Haptic(bool enabled, bool milestone = false)
        {
            if (!enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            RRHaptic(milestone ? 1 : 0);
#endif
        }
        public static void Share(string text, string imagePath = "")
        {
#if UNITY_IOS && !UNITY_EDITOR
            RRShare(text, imagePath ?? "");
#else
            GUIUtility.systemCopyBuffer = text;
            Debug.Log("Ring Rush result copied to clipboard.");
#endif
        }
    }
}
