using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
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
        [DllImport("__Internal")] static extern int RRTrackingAuthorizationStatus();
        [DllImport("__Internal")] static extern void RRRequestTrackingAuthorization(TrackingCallback callback);
        [DllImport("__Internal")] static extern float RRLogicalScreenWidth();
        delegate void TrackingCallback(int status);
        static readonly TrackingCallback trackingCallback = OnTrackingAuthorization;
        static Action<int> pendingTrackingCallbacks;
        static SynchronizationContext trackingContext;

        [AOT.MonoPInvokeCallback(typeof(TrackingCallback))]
        static void OnTrackingAuthorization(int status)
        {
            // Native completion can arrive off Unity's synchronization context.
            var context = trackingContext;
            context.Post(_ =>
            {
                var callbacks = pendingTrackingCallbacks;
                pendingTrackingCallbacks = null;
                callbacks?.Invoke(status);
            }, null);
        }
#else
        static readonly Dictionary<string, string> secrets = new Dictionary<string, string>();
#endif
        public static int TrackingAuthorizationStatus
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RRTrackingAuthorizationStatus();
#else
                return 2;
#endif
            }
        }

        public static void RequestTrackingAuthorization(Action<int> completed)
        {
#if UNITY_IOS && !UNITY_EDITOR
            int status = TrackingAuthorizationStatus;
            if (status != 0) { completed?.Invoke(status); return; }
            bool pending = pendingTrackingCallbacks != null;
            pendingTrackingCallbacks += completed;
            if (pending) return;
            trackingContext = SynchronizationContext.Current;
            if (trackingContext == null)
            {
                pendingTrackingCallbacks = null;
                throw new InvalidOperationException("Request tracking permission from Unity's main thread.");
            }
            RRRequestTrackingAuthorization(trackingCallback);
#else
            completed?.Invoke(2);
#endif
        }

        public static float ScreenPixelsPerPoint
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                float width = RRLogicalScreenWidth();
                return width > 0 ? Screen.width / width : 1f;
#else
                return 1f;
#endif
            }
        }
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
        public static void Haptic(bool enabled, bool milestone = false, bool perfect = false)
        {
            if (!enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            RRHaptic(perfect ? 2 : milestone ? 1 : 0);
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
