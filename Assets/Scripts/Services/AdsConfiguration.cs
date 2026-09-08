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
        [Header("Google UMP consent messages (no Google ad network)")]
        [Tooltip("Real iOS AdMob app ID with published regional privacy messages and Unity/ironSource partners.")]
        public string IosConsentAppId = "";
        [Tooltip("Enable only after regional consent messages and their vendor list are published and verified in AdMob.")]
        public bool ConsentMessagesPublished;

        public bool HasConsentAppId => System.Text.RegularExpressions.Regex.IsMatch(
            IosConsentAppId ?? "", @"^ca-app-pub-[0-9]{16}~[0-9]{10}$");

        public bool HasIosIdentifiers => !string.IsNullOrWhiteSpace(IosAppKey)
            && !string.IsNullOrWhiteSpace(IosBannerAdUnitId)
            && !string.IsNullOrWhiteSpace(IosRewardedAdUnitId)
            && !string.IsNullOrWhiteSpace(IosInterstitialAdUnitId);
        public bool HasPrivacyPolicy => Uri.TryCreate(PrivacyPolicyUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.Host);
    }
}
