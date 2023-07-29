import React, { useState, useEffect } from "react";
import { Text, View, Dimensions, Button } from "react-native";
import Popover from 'react-native-popover-view';
import { PulsingButton } from "../pulsing-button";
import { Typography } from "../typography";
import { getExtraLives } from "@fb";
import { useAdContext } from "@layouts";


export const GetExtraLivesModal = () => {
    const [showPopover, setShowPopover] = useState(false);
    const [extraLives, setExtraLives] = useState<number>(0);

    const { rewardedInterstitialAd } = useAdContext();



    // const windowWidth = Dimensions.get('window').width;
    // const windowHeight = Dimensions.get('window').height;

    useEffect(() => {
        async function init() {
            const extraLives = await getExtraLives();
            setExtraLives(extraLives);
        }

        init()
    })

    const onWatchAdButtonClicked = () => {
        rewardedInterstitialAd.show();
    }

    return (
        <>
            <PulsingButton
                onPress={() => setShowPopover(true)}
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
                    // width: windowWidth * 0.9,
                    // height: windowHeight * 0.9,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 30,
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
                <View>
                    <Button
                        onPress={onWatchAdButtonClicked}
                        title="Watch an ad"
                    />
                </View>
                <View className="mt-20">
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
}