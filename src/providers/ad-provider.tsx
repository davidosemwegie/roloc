import { PropsWithChildren, createContext, useContext, useEffect, useState } from "react";
import { Platform } from "react-native";
import { TestIds, useInterstitialAd, AdHookReturns, useRewardedInterstitialAd } from 'react-native-google-mobile-ads';
import remoteConfig from '@react-native-firebase/remote-config';
import { getShouldShowAds } from "@fb";

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
        await remoteConfig().setDefaults({
            show_ads: false,
        });
        const shouldShow = await getShouldShowAds()
        setShouldShowAds(shouldShow);
        console.log("shouldShowAds: ", shouldShowAds);
    };

    useEffect(() => {
        fetchShouldShowAds();

        const intervalId = setInterval(fetchShouldShowAds, 60 * 1000); // Fetch every 60 seconds

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

    // useEffect(() => {
    //     if (shouldShowAds) {
    //         interstitialAd.load();
    //         rewardedInterstitialAd.load();
    //     }
    // }, [shouldShowAds])

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

export const useAdContext = () => {
    const context = useContext(AdContext);

    // useEffect(() => {
    //     if (context.shouldShowAds) {
    //         context.interstitialAd.load();
    //         context.rewardedInterstitialAd.load();
    //     }
    // }, [context.shouldShowAds])

    if (context === undefined) {
        throw new Error('useAdContext must be used within a AdProvider');
    }


    return context;
}
