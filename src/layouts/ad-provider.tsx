import { PropsWithChildren, createContext, useContext } from "react";
import { TestIds, useInterstitialAd, AdHookReturns } from 'react-native-google-mobile-ads';



export interface AdContext {
    interstitialAd: Omit<AdHookReturns, "reward" | "isEarnedReward">
}


export const AdContext = createContext<AdContext>(undefined);

export const AdProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {

    const interstitialAd = useInterstitialAd(TestIds.INTERSTITIAL, {
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