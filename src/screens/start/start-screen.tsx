import { GameStats, GetExtraLivesModal, PulsingButton, Screen, Typography } from '@components';
import { useGameStateStore } from '@stores';
import React, { useEffect } from 'react';
import { View } from 'react-native';
import { useAdContext } from '../../layouts';
import { Instructions } from './instructions';

const StartScreen = () => {

    const { interstitialAd, rewardedInterstitialAd } = useAdContext();

    const { startGame } = useGameStateStore();

    useEffect(() => {

        rewardedInterstitialAd.load();

    }, [])

    return (
        <Screen className='justify-between'>
            <View className='space-y-10'>
                <View className="flex flex-row mx-auto" >
                    <Typography className="text-6xl mx-1 font-bold text-blue-500">R</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-yellow-500">O</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-red-500">L</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-green-500">O</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-white-500">C</Typography>
                </View>
                <View>
                    <GameStats />
                </View>
                <View className='space-y-10'>

                    <View className='flex justify-between items-center mb-6'>
                        <GetExtraLivesModal />
                    </View>

                    <Instructions />
                </View>
                <View className='m-auto'>
                    <PulsingButton
                        onPress={() => {
                            startGame(() => interstitialAd.load())
                        }}
                    >
                        PLAY
                    </PulsingButton>
                </View>
            </View>

        </Screen>
    );
};

export default StartScreen;
