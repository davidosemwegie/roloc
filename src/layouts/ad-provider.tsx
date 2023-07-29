import { addExtraLife } from "@fb";
import { PropsWithChildren, createContext, useContext, useEffect } from "react";
import { Platform } from "react-native";
import { TestIds, useInterstitialAd, AdHookReturns, useRewardedInterstitialAd } from 'react-native-google-mobile-ads';

export interface AdContext {
    interstitialAd: Omit<AdHookReturns, "reward" | "isEarnedReward">
    rewardedInterstitialAd: AdHookReturns
}


export const AdContext = createContext<AdContext>(undefined);

export const AdProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {

    const interstitialAdId = __DEV__ ? TestIds.INTERSTITIAL : Platform.OS === 'ios' ? 'ca-app-pub-6400654457067913/4850883993' : "ca-app-pub-6400654457067913/2711751913";

    const rewardedAdId = __DEV__ ? TestIds.REWARDED_INTERSTITIAL : Platform.OS === 'ios' ? 'ca-app-pub-6400654457067913/6403584915' : "ca-app-pub-6400654457067913/9029748255";

    const interstitialAd = useInterstitialAd(interstitialAdId, {
        requestNonPersonalizedAdsOnly: true,
    });

    const rewardedInterstitialAd = useRewardedInterstitialAd(rewardedAdId, {
        requestNonPersonalizedAdsOnly: true,
    });

    const { isClosed, isEarnedReward } = rewardedInterstitialAd


    useEffect(() => {
        rewardedInterstitialAd.load();
    }, [rewardedInterstitialAd.load])

    useEffect(() => {
        if (isClosed) {
            // Log the reward when the rewarded ad is closed
            console.log("Reward:", isEarnedReward ? "Earned" : "Not Earned");
            console.log("Reward amount:", rewardedInterstitialAd.reward);
            addExtraLife();

            // Load another ad
            rewardedInterstitialAd.load();
        }
    }, [isClosed, isEarnedReward, rewardedInterstitialAd]);



    const value = {
        interstitialAd,
        rewardedInterstitialAd
    }

    return (
        <AdContext.Provider value={value}>
            {children}
        </AdContext.Provider>
    );
}

export const useAdContext = () => useContext(AdContext);