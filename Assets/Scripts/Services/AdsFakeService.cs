#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;

namespace Roloc.Services
{
    /// <summary>Explicitly injected test double; a device build never fabricates an ad completion.</summary>
    public sealed class FakeAdService : IAdService
    {
        public bool IsConfigured { get; set; } = true;
        public bool IsInitialized { get; private set; }
        bool rewardedAvailable = true, interstitialAvailable = true;
        public bool IsRewardedReady { get => IsInitialized && rewardedAvailable; set => rewardedAvailable = value; }
        public bool IsInterstitialReady { get => IsInitialized && interstitialAvailable; set => interstitialAvailable = value; }
        public int BannerHeightPixels { get; set; } = 50;
        public bool BannerVisible { get; private set; }
        public bool Personalized { get; private set; }
        public bool DeviceDataAllowed { get; private set; }
        public int InitializeCalls { get; private set; }
        public int RewardedShows { get; private set; }
        public int InterstitialShows { get; private set; }
        Action<RewardedAdEvent> rewardCallback;
        Action interstitialCallback;
        public void SetDeviceDataConsent(bool allowed)
        {
            DeviceDataAllowed = allowed;
            if (allowed) return;
            Personalized = false; IsInitialized = false; BannerVisible = false;
            rewardCallback = null;
            var nextGame = interstitialCallback; interstitialCallback = null;
            nextGame?.Invoke();
        }
        public void Initialize(bool personalized, Action<bool> completed)
        {
            InitializeCalls++;
            Personalized = DeviceDataAllowed && personalized;
            IsInitialized = IsConfigured && DeviceDataAllowed;
            completed?.Invoke(IsInitialized);
        }
        public void SetBannerVisible(bool visible) => BannerVisible = visible && IsInitialized;
        public void ShowRewarded(Action<RewardedAdEvent> callback)
        {
            if (!IsInitialized || !IsRewardedReady) { callback?.Invoke(RewardedAdEvent.Failed); return; }
            RewardedShows++; rewardCallback = callback;
        }
        public void EmitRewarded(RewardedAdEvent result) => rewardCallback?.Invoke(result);
        public void ShowInterstitial(Action completed)
        {
            if (!IsInitialized || !IsInterstitialReady) { completed?.Invoke(); return; }
            InterstitialShows++; interstitialCallback = completed;
        }
        public void CloseInterstitial() { var callback = interstitialCallback; interstitialCallback = null; callback?.Invoke(); }
        public void Dispose() { rewardCallback = null; interstitialCallback = null; IsInitialized = false; }
    }
}
#endif
