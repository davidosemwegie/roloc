#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;

namespace Roloc.Services
{
    /// <summary>Explicitly injected test double; a device build never fabricates an ad completion.</summary>
    public sealed class FakeAdService : IAdService
    {
        public bool IsConfigured { get; set; } = true;
        public bool IsInitialized { get; private set; }
        public bool IsRewardedReady { get; set; } = true;
        public bool IsInterstitialReady { get; set; } = true;
        public int BannerHeightPixels { get; set; } = 50;
        public bool BannerVisible { get; private set; }
        public bool Personalized { get; private set; }
        public int RewardedShows { get; private set; }
        public int InterstitialShows { get; private set; }
        Action<RewardedAdEvent> rewardCallback;
        Action interstitialCallback;
        public void Initialize(bool personalized, Action<bool> completed) { Personalized = personalized; IsInitialized = IsConfigured; completed?.Invoke(IsInitialized); }
        public void SetBannerVisible(bool visible) => BannerVisible = visible;
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
