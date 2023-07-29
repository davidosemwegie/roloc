import { PropsWithChildren, createContext, useContext } from "react";
import { Platform } from "react-native";
import { TestIds, useInterstitialAd, AdHookReturns } from 'react-native-google-mobile-ads';

export interface AdContext {
    interstitialAd: Omit<AdHookReturns, "reward" | "isEarnedReward">
}


export const AdContext = createContext<AdContext>(undefined);

export const AdProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {

    const interstitialAdId = __DEV__ ? TestIds.INTERSTITIAL : Platform.OS === 'ios' ? 'ca-app-pub-6400654457067913/4850883993' : "ca-app-pub-6400654457067913/2711751913";

    const rewardedAdId = __DEV__ ? TestIds.REWARDED : Platform.OS === 'ios' ? 'ca-app-pub-6400654457067913/6403584915' : "ca-app-pub-6400654457067913/9029748255";

    const interstitialAd = useInterstitialAd(interstitialAdId, {
        requestNonPersonalizedAdsOnly: true,
    });


    const value = {
        interstitialAd
    }

    return (
        <AdContext.Provider value={value}>
            {children}
        </AdContext.Provider>
    );
}

export const useAdContext = () => useContext(AdContext);