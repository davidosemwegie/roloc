using System;
using UnityEngine;

namespace Roloc.Services
{
    [CreateAssetMenu(menuName = "Ring Rush/Ads configuration")]
    public sealed class AdsConfiguration : ScriptableObject
    {
        [Tooltip("Published privacy policy describing Unity advertising and privacy choices.")]
        public string PrivacyPolicyUrl = "";
        [Header("LevelPlay iOS dashboard identifiers")]
        public string IosAppKey = "";
        public string IosBannerAdUnitId = "";
        public string IosRewardedAdUnitId = "";
        public string IosInterstitialAdUnitId = "";

        public bool HasIosIdentifiers => !string.IsNullOrWhiteSpace(IosAppKey)
            && !string.IsNullOrWhiteSpace(IosBannerAdUnitId)
            && !string.IsNullOrWhiteSpace(IosRewardedAdUnitId)
            && !string.IsNullOrWhiteSpace(IosInterstitialAdUnitId);
        public bool HasPrivacyPolicy => Uri.TryCreate(PrivacyPolicyUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.Host);
    }
}
