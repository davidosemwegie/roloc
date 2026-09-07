using System;
using System.Collections;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Roloc.Services
{
    public enum RewardedAdEvent { Rewarded, Closed, Failed }

    public interface IAdService : IDisposable
    {
        bool IsConfigured { get; }
        bool IsInitialized { get; }
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }
        int BannerHeightPixels { get; }
        void SetDeviceDataConsent(bool allowed);
        void Initialize(bool personalized, Action<bool> completed);
        void SetBannerVisible(bool visible);
        // Closed is not a terminal reward result: the SDK may deliver Rewarded afterwards.
        void ShowRewarded(Action<RewardedAdEvent> callback);
        void ShowInterstitial(Action completed);
    }

    /// <summary>Owns native ad inventory. Only iOS device builds use the SDK; tests inject a fake.</summary>
    public sealed class LevelPlayAdService : IAdService
    {
        readonly AdsConfiguration config;
        readonly AdsRuntimeHost host;
        LevelPlayBannerAd banner;
        LevelPlayRewardedAd rewarded;
        LevelPlayRewardedAd activeRewarded;
        LevelPlayRewardedAd retiredRewarded;
        LevelPlayInterstitialAd interstitial;
        Action<bool> initializedCallbacks;
        Action interstitialCompleted;
        Action<RewardedAdEvent> activeRewardCallback;
        bool initializing, disposed, bannerWanted, bannerLoaded, fullScreen, personalized;
        bool interstitialDisplayed;
        bool deviceDataAllowed, sdkInitialized;
        bool? nativeBannerVisible;
        int generation, initializationFailures;
        int initializationAttempt;

        public LevelPlayAdService(AdsConfiguration config)
        {
            this.config = config;
            host = new GameObject("Advertising lifecycle").AddComponent<AdsRuntimeHost>();
            UnityEngine.Object.DontDestroyOnLoad(host.gameObject);
        }

        public bool IsConfigured
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return !disposed && config && config.HasIosIdentifiers && config.HasPrivacyPolicy;
#else
                return false;
#endif
            }
        }

        public bool IsInitialized => sdkInitialized && deviceDataAllowed && !disposed;
        public bool IsRewardedReady => IsInitialized && !disposed && !fullScreen && rewarded != null && rewarded.IsAdReady();
        public bool IsInterstitialReady => IsInitialized && !disposed && !fullScreen && interstitial != null && interstitial.IsAdReady();
        // LevelPlay iOS anchors bottom-center banners to the native safeAreaLayoutGuide.
        // The gameplay safe-area container reserves this additional 50pt above its own bottom inset.
        public int BannerHeightPixels => IsConfigured ? Mathf.CeilToInt(50f * NativeServices.ScreenPixelsPerPoint) : 0;

        public void SetDeviceDataConsent(bool allowed)
        {
            if (disposed || deviceDataAllowed == allowed) return;
            deviceDataAllowed = allowed;
            if (allowed) return;
            // LevelPlay has no SDK shutdown API. Stop our inventory and future requests, and
            // restrict the already initialized SDK; do not claim this erases prior processing.
            GoogleUmpConsentService.ApplyTrackingRestriction(false);
            personalized = false;
            initializationAttempt++;
            initializing = false;
            host.StopAllCoroutines();
            var callbacks = initializedCallbacks;
            initializedCallbacks = null;
            DestroyInventory();
            callbacks?.Invoke(false);
        }

        public void Initialize(bool allowPersonalized, Action<bool> completed)
        {
            if (!IsConfigured || !deviceDataAllowed) { completed?.Invoke(false); return; }
            // LevelPlay imports actual UMP regulatory choices. ATT is an additional tracking
            // restriction, never a substitute GDPR consent or US sale/sharing decision.
            GoogleUmpConsentService.ApplyTrackingRestriction(allowPersonalized);
            bool changed = personalized != allowPersonalized;
            personalized = allowPersonalized;
            if (sdkInitialized)
            {
                if (changed) DestroyInventory();
                CreateInventory();
                completed?.Invoke(true);
                return;
            }
            initializedCallbacks += completed;
            if (initializing) return;
            initializing = true;
            int attempt = ++initializationAttempt;
            LevelPlay.OnInitSuccess -= OnInitialized;
            LevelPlay.OnInitFailed -= OnInitializationFailed;
            LevelPlay.OnInitSuccess += OnInitialized;
            LevelPlay.OnInitFailed += OnInitializationFailed;
            try { LevelPlay.Init(config.IosAppKey); }
            catch (Exception error) { Debug.LogWarning("Ads initialization failed: " + error.Message); FinishInitialization(false); }
            host.After(20, () => { if (initializing && !disposed && initializationAttempt == attempt) FinishInitialization(false); });
        }

        void OnInitialized(LevelPlayConfiguration _)
        {
            if (disposed) return;
            sdkInitialized = true;
            if (!deviceDataAllowed) return;
            CreateInventory();
            FinishInitialization(true);
        }
        void OnInitializationFailed(LevelPlayInitError _) { if (!disposed) FinishInitialization(false); }
        void FinishInitialization(bool success)
        {
            if (!deviceDataAllowed || disposed) return;
            if (!success && !initializing) return;
            initializing = false;
            var callback = initializedCallbacks;
            initializedCallbacks = null;
            callback?.Invoke(success);
            if (success) { initializationFailures = 0; return; }
            // Offline startup is transient. Retry with capped backoff without blocking play.
            int attempt = initializationAttempt;
            float delay = Mathf.Min(60, 5 * Mathf.Pow(2, Mathf.Min(4, initializationFailures++)));
            host.After(delay, () => {
                if (disposed || !deviceDataAllowed || IsInitialized || initializing || attempt != initializationAttempt) return;
                Initialize(personalized && NativeServices.TrackingAuthorizationStatus == 3, null);
            });
        }

        void CreateInventory()
        {
            if (disposed || !IsInitialized || banner != null) return;
            int inventory = generation;
            var options = new LevelPlayBannerAd.Config.Builder()
                .SetSize(LevelPlayAdSize.BANNER).SetPosition(LevelPlayBannerPosition.BottomCenter)
                .SetRespectSafeArea(true).SetDisplayOnLoad(false).Build();
            var newBanner = new LevelPlayBannerAd(config.IosBannerAdUnitId, options);
            banner = newBanner;
            newBanner.OnAdLoaded += _ => { if (Current(inventory) && banner == newBanner) { bannerLoaded = true; UpdateBanner(); } };
            newBanner.OnAdLoadFailed += _ => Retry(inventory, () => { if (banner == newBanner) newBanner.LoadAd(); });
            newBanner.LoadAd();
            CreateRewarded();
            CreateInterstitial();
        }

        bool Current(int inventory) => !disposed && IsInitialized && inventory == generation;
        void Retry(int inventory, Action load) => host.After(15, () => { if (Current(inventory)) load(); });

        void CreateRewarded()
        {
            if (disposed || !IsInitialized) return;
            int inventory = generation;
            var ad = new LevelPlayRewardedAd(config.IosRewardedAdUnitId);
            rewarded = ad;
            ad.OnAdLoadFailed += _ => Retry(inventory, () => { if (rewarded == ad) ad.LoadAd(); });
            ad.LoadAd();
        }

        void CreateInterstitial()
        {
            if (disposed || !IsInitialized) return;
            int inventory = generation;
            var ad = new LevelPlayInterstitialAd(config.IosInterstitialAdUnitId);
            interstitial = ad;
            ad.OnAdLoadFailed += _ => Retry(inventory, () => { if (interstitial == ad) ad.LoadAd(); });
            ad.OnAdDisplayed += _ => { if (Current(inventory)) interstitialDisplayed = true; };
            ad.OnAdClosed += _ => { if (Current(inventory)) FinishInterstitial(ad); };
            ad.OnAdDisplayFailed += (_, __) => { if (Current(inventory)) FinishInterstitial(ad); };
            ad.LoadAd();
        }

        public void SetBannerVisible(bool visible) { bannerWanted = visible; UpdateBanner(); }
        void UpdateBanner()
        {
            if (banner == null || disposed) return;
            bool visible = bannerWanted && bannerLoaded && !fullScreen;
            if (nativeBannerVisible == visible) return;
            nativeBannerVisible = visible;
            if (visible) { banner.ResumeAutoRefresh(); banner.ShowAd(); }
            else { banner.HideAd(); banner.PauseAutoRefresh(); }
        }

        public void ShowRewarded(Action<RewardedAdEvent> callback)
        {
            if (!IsRewardedReady) { callback?.Invoke(RewardedAdEvent.Failed); return; }
            retiredRewarded?.DestroyAd();
            retiredRewarded = null;
            var ad = rewarded;
            activeRewarded = ad;
            rewarded = null;
            int inventory = generation;
            bool earned = false, closed = false, failed = false;
            fullScreen = true;
            activeRewardCallback = callback;
            UpdateBanner();
            ad.OnAdRewarded += (_, __) =>
            {
                if (!Current(inventory) || earned || failed) return;
                earned = true;
                callback?.Invoke(RewardedAdEvent.Rewarded);
                if (closed) { ad.DestroyAd(); if (retiredRewarded == ad) retiredRewarded = null; }
            };
            ad.OnAdClosed += _ =>
            {
                if (!Current(inventory) || closed || failed) return;
                closed = true;
                fullScreen = false;
                activeRewardCallback = null;
                activeRewarded = null;
                // Keep this exact ad/callback alive until its late reward arrives or another show begins.
                if (earned) ad.DestroyAd(); else retiredRewarded = ad;
                CreateRewarded();
                UpdateBanner();
                callback?.Invoke(RewardedAdEvent.Closed);
            };
            Action fail = () =>
            {
                if (!Current(inventory) || closed || failed) return;
                failed = true;
                fullScreen = false;
                activeRewardCallback = null;
                activeRewarded = null;
                ad.DestroyAd();
                CreateRewarded();
                UpdateBanner();
                callback?.Invoke(RewardedAdEvent.Failed);
            };
            ad.OnAdDisplayFailed += (_, __) => fail();
            try { ad.ShowAd(); } catch (Exception) { fail(); }
        }

        public void ShowInterstitial(Action completed)
        {
            if (!IsInterstitialReady) { completed?.Invoke(); return; }
            fullScreen = true;
            interstitialDisplayed = false;
            interstitialCompleted = completed;
            UpdateBanner();
            var ad = interstitial;
            int inventory = generation;
            try { ad.ShowAd(); } catch (Exception) { FinishInterstitial(ad); return; }
            // Never strand the next-game action if the SDK cannot begin its presentation.
            host.After(15, () => { if (Current(inventory) && interstitial == ad && !interstitialDisplayed) FinishInterstitial(ad); });
        }

        void FinishInterstitial(LevelPlayInterstitialAd ad)
        {
            if (interstitial != ad) return;
            var completed = interstitialCompleted;
            interstitialCompleted = null;
            fullScreen = false;
            ad.DestroyAd();
            interstitial = null;
            CreateInterstitial();
            UpdateBanner();
            completed?.Invoke();
        }

        void DestroyInventory()
        {
            generation++;
            banner?.DestroyAd(); banner = null;
            rewarded?.DestroyAd(); rewarded = null;
            activeRewarded?.DestroyAd(); activeRewarded = null;
            retiredRewarded?.DestroyAd(); retiredRewarded = null;
            interstitial?.DestroyAd(); interstitial = null;
            bannerLoaded = false;
            nativeBannerVisible = null;
            fullScreen = false;
            var rewardCallback = activeRewardCallback; activeRewardCallback = null;
            var nextGame = interstitialCompleted; interstitialCompleted = null;
            rewardCallback?.Invoke(RewardedAdEvent.Failed);
            nextGame?.Invoke();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            sdkInitialized = false;
            LevelPlay.OnInitSuccess -= OnInitialized;
            LevelPlay.OnInitFailed -= OnInitializationFailed;
            DestroyInventory();
            initializedCallbacks = null;
            if (host) UnityEngine.Object.Destroy(host.gameObject);
        }
    }

    internal sealed class AdsRuntimeHost : MonoBehaviour
    {
        public void After(float seconds, Action callback) => StartCoroutine(Delay(seconds, callback));
        static IEnumerator Delay(float seconds, Action callback) { yield return new WaitForSecondsRealtime(seconds); callback(); }
    }
}
