import React from 'react'
import { BannerAd, BannerAdSize, TestIds } from 'react-native-google-mobile-ads'

const adUnitId = __DEV__ ? TestIds.BANNER : 'ca-app-pub-xxxxxxxxxxxxx/yyyyyyyyyyyyyy';


export const BannerAds = () => {
    return (
        <BannerAd
            unitId={adUnitId}
            size={BannerAdSize.ANCHORED_ADAPTIVE_BANNER}
            requestOptions={{
                requestNonPersonalizedAdsOnly: true,
            }}
        />
    )
}