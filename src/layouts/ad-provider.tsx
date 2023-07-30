import { PropsWithChildren, createContext, useContext, useEffect, useState } from "react";
import { Platform } from "react-native";
import { TestIds, useInterstitialAd, AdHookReturns, useRewardedInterstitialAd } from 'react-native-google-mobile-ads';
import remoteConfig from '@react-native-firebase/remote-config';

export interface AdContext {
    interstitialAd: Omit<AdHookReturns, "reward" | "isEarnedReward">
    rewardedInterstitialAd: AdHookReturns
    shouldShowAds: boolean
}

export const AdContext = createContext<AdContext>(undefined);

export const AdProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {
    const [shouldShowAds, setShouldShowAds] = useState(true);

    const fetchShouldShowAds = async () => {
        await remoteConfig().fetchAndActivate();
        const shouldShow = await remoteConfig().getBoolean('show_ads');
        setShouldShowAds(shouldShow);
    };

    useEffect(() => {
        fetchShouldShowAds();

        const intervalId = setInterval(fetchShouldShowAds, 60000); // Fetch every 60 seconds

        return () => {
            clearInterval(intervalId);
        };
    }, []);

    const interstitialAdId = __DEV__ ? TestIds.INTERSTITIAL : Platform.OS === 'ios' ? 'ca-app-pub-6400654457067913/4850883993' : "ca-app-pub-6400654457067913/2711751913";

    const rewardedAdId = __DEV__ ? TestIds.REWARDED_INTERSTITIAL : Platform.OS === 'ios' ? 'ca-app-pub-6400654457067913/6403584915' : "ca-app-pub-6400654457067913/9029748255";

    const interstitialAd = useInterstitialAd(interstitialAdId, {
        requestNonPersonalizedAdsOnly: true,
    });

    const rewardedInterstitialAd = useRewardedInterstitialAd(rewardedAdId, {
        requestNonPersonalizedAdsOnly: true,
    });

    const value = {
        interstitialAd: shouldShowAds ? interstitialAd : {
            load: () => { },
            isLoaded: false,
            show: () => { },
        },
        rewardedInterstitialAd: shouldShowAds ? rewardedInterstitialAd : {
            load: () => { },
            isLoaded: false,
            show: () => { },
            isEarnedReward: false,
        },
        shouldShowAds
    };

    return (
        <AdContext.Provider value={value as AdContext}>
            {children}
        </AdContext.Provider>
    );
}

export const useAdContext = () => useContext(AdContext);
