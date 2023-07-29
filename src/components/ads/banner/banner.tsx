import React from 'react'
import { Platform } from 'react-native';
import { BannerAd, BannerAdSize, TestIds } from 'react-native-google-mobile-ads'

const adUnitId = __DEV__ ? TestIds.BANNER : Platform.OS === 'ios' ? "ca-app-pub-6400654457067913/8646658773" : "ca-app-pub-6400654457067913/9859668093";


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