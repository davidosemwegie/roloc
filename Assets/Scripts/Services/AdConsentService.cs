using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Roloc.Services
{
    public interface IAdConsentService
    {
        bool IsConfigured { get; }
        bool CanRequestAds { get; }
        bool PrivacyOptionsRequired { get; }
        bool TrackingAllowedByConsent { get; }
        void GatherConsent(Action completed);
        void ShowPrivacyOptions(Action completed);
    }

    /// <summary>Provider-managed regional privacy messages, independent of Apple's tracking permission.</summary>
    public sealed class GoogleUmpConsentService : IAdConsentService
    {
        readonly AdsConfiguration config;
        Action pendingCallbacks;
        bool pending;

        public GoogleUmpConsentService(AdsConfiguration config) { this.config = config; }
        public bool IsConfigured => config && config.HasConsentAppId && config.ConsentMessagesPublished && config.HasPrivacyPolicy;
        public bool CanRequestAds { get; private set; }
        public bool PrivacyOptionsRequired { get; private set; }
        public bool TrackingAllowedByConsent { get; private set; }

        public void GatherConsent(Action completed) { Begin(false, completed); }
        public void ShowPrivacyOptions(Action completed) { Begin(true, completed); }

        void Begin(bool privacyOptions, Action completed)
        {
            if (!IsConfigured)
            {
                CanRequestAds = TrackingAllowedByConsent = PrivacyOptionsRequired = false;
                completed?.Invoke();
                return;
            }
            if (privacyOptions && !PrivacyOptionsRequired) { completed?.Invoke(); return; }
            pendingCallbacks += completed;
            if (pending) return;
            pending = true;
            CanRequestAds = TrackingAllowedByConsent = false;
#if UNITY_IOS && !UNITY_EDITOR
            var context = SynchronizationContext.Current;
            if (context == null)
            {
                Finish(0);
                throw new InvalidOperationException("Request advertising consent from Unity's main thread.");
            }
            int request;
            lock (requests)
            {
                request = ++nextRequest;
                requests.Add(request, new Request { Owner = this, Context = context });
            }
            try { RRAdConsentRequest(request, privacyOptions ? 1 : 0, nativeCallback); }
            catch
            {
                lock (requests) requests.Remove(request);
                Finish(0);
                throw;
            }
#else
            // Editor and tests inject their own consent service; never synthesize consent here.
            Finish(0);
#endif
        }

        void Finish(int flags)
        {
            if (!pending) return;
            pending = false;
            CanRequestAds = (flags & 1) != 0;
            PrivacyOptionsRequired = (flags & 2) != 0;
            TrackingAllowedByConsent = CanRequestAds && (flags & 4) != 0;
            var callbacks = pendingCallbacks;
            pendingCallbacks = null;
            callbacks?.Invoke();
        }

        /// <summary>Restricts Unity Ads without replacing the CMP's GDPR or US opt-out decisions.</summary>
        public static void ApplyTrackingRestriction(bool trackingAllowed)
        {
#if UNITY_IOS && !UNITY_EDITOR
            RRAdConsentSetTrackingAllowed(trackingAllowed ? 1 : 0);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        delegate void ConsentCallback(int request, int flags);
        sealed class Request
        {
            public GoogleUmpConsentService Owner;
            public SynchronizationContext Context;
        }
        static readonly Dictionary<int, Request> requests = new Dictionary<int, Request>();
        static readonly ConsentCallback nativeCallback = OnNativeCompleted;
        static int nextRequest;

        [DllImport("__Internal")] static extern void RRAdConsentRequest(int request, int privacyOptions, ConsentCallback callback);
        [DllImport("__Internal")] static extern void RRAdConsentSetTrackingAllowed(int trackingAllowed);

        [AOT.MonoPInvokeCallback(typeof(ConsentCallback))]
        static void OnNativeCompleted(int request, int flags)
        {
            Request operation;
            lock (requests)
            {
                if (!requests.TryGetValue(request, out operation)) return;
                requests.Remove(request);
            }
            operation.Context.Post(_ => operation.Owner.Finish(flags), null);
        }
#endif
    }
}
