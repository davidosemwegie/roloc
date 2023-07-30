import React, { useEffect, useState } from 'react';
import { Platform } from 'react-native';
import { BannerAd, BannerAdSize, TestIds } from 'react-native-google-mobile-ads'
import remoteConfig from '@react-native-firebase/remote-config';
import { Typography } from '@components';

const adUnitId = __DEV__ ? TestIds.BANNER : Platform.OS === 'ios' ? "ca-app-pub-6400654457067913/8646658773" : "ca-app-pub-6400654457067913/9859668093";

export const BannerAds = () => {

    const [showAds, setShowAds] = useState(false);

    useEffect(() => {
        const fetchConfig = async () => {
            await remoteConfig().fetchAndActivate() // fetch new values if available
            const shouldShowAds = remoteConfig().getValue('show_ads')
            console.log({ shouldShowAds });
            setShowAds(shouldShowAds.asBoolean());
        };

        fetchConfig();

        const intervalId = setInterval(fetchConfig, 60000); // Fetch every 60 seconds

        return () => {
            clearInterval(intervalId);
        };
    }, [])

    if (!showAds) {
        return null;
    }

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
