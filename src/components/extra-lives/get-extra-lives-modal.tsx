import React, { useState, useEffect } from "react";
import { Text, View, Dimensions, Button } from "react-native";
import Popover from 'react-native-popover-view';
import { PulsingButton } from "../pulsing-button";
import { Typography } from "../typography";
import { getExtraLives, useExtraLife } from "@fb";
import { useAdContext } from "@layouts";
import { useExtraLifeStore } from "@stores";

export const GetExtraLivesModal = () => {
    const [showPopover, setShowPopover] = useState(false);
    const [isLoadingAd, setIsLoadingAd] = useState(false); // New state for ad loading

    const { extraLives, setExtraLives, addExtraLife } = useExtraLifeStore();

    const { rewardedInterstitialAd } = useAdContext();

    useEffect(() => {
        setExtraLives()
    }, [])

    useEffect(() => {

        const { isEarnedReward } = rewardedInterstitialAd;

        if (isEarnedReward) {
            addExtraLife();
        }

    }, [
        rewardedInterstitialAd.isEarnedReward,
    ])


    useEffect(() => {
        if (rewardedInterstitialAd.isLoaded) {
            setIsLoadingAd(false); // Set isLoadingAd to false when the ad is loaded
        }
    }, [rewardedInterstitialAd.isLoaded])

    const onWatchAdButtonClicked = () => {
        rewardedInterstitialAd.show();
    };

    const onGetExtraLivesButtonClicked = async () => {
        setShowPopover(true);
        setIsLoadingAd(true); // Set isLoadingAd to true before loading the ad
        try {
            await rewardedInterstitialAd.load()
        } catch (error) {
            console.error("Error loading ad:", error);
            setIsLoadingAd(false); // Set isLoadingAd to false in case of an error
        }
    };

    return (
        <>
            <PulsingButton
                onPress={onGetExtraLivesButtonClicked}
                size={8}
                color="#1d4ed8"
            >
                Get extra lives
            </PulsingButton>
            <Popover
                isVisible={showPopover}
                onRequestClose={() => setShowPopover(false)}
                popoverStyle={{
                    backgroundColor: '#171717',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    padding: 40,
                    borderRadius: 20,
                }}
            >
                <View>
                    <Typography className="">
                        Get extra more lives
                    </Typography>
                </View>
                <View className='flex flex-row justify-between'>
                    <Typography className="text-xl">Extra Lives: </Typography>
                    <Typography className="text-xl "> {extraLives}</Typography>
                </View>
                {/* <Typography>
                    {JSON.stringify({
                        loaded: rewardedInterstitialAd.isLoaded,
                    })}
                </Typography> */}
                {isLoadingAd ? (
                    <Typography className="text-center text-[16px] mt-4">
                        Loading...
                    </Typography>
                ) : rewardedInterstitialAd.isLoaded ? (
                    <View>
                        <Button
                            onPress={onWatchAdButtonClicked}
                            title="Watch an ad"
                        />
                    </View>
                ) : (
                    <View className="mt-4">
                        <Typography className="text-center text-[16px]">
                            Come back later to get more free extra lives
                        </Typography>
                    </View>
                )}
                <View className="mt-10">
                    <PulsingButton
                        onPress={() => setShowPopover(false)}
                        size={8}
                        color="#1d4ed8"
                    >
                        Close
                    </PulsingButton>
                </View>
            </Popover>
        </>
    );
};
