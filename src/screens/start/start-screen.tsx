import { GameStats, GetExtraLivesModal, PulsingButton, Typography } from '@components';
import { useGameStateStore } from '@stores';
import React, { useEffect } from 'react';
import { SafeAreaView, View } from 'react-native';
import { Instructions } from './instructions';
import Entypo from '@expo/vector-icons/Entypo';
import { useSound } from '@utils';
import { SettingsPopup } from './settings';
import { useAdContext } from '@providers';


const StartScreen = () => {


    const { startGame, isBackgroundMuted } = useGameStateStore();

    const { playSound, stopSound } = useSound('game-start', {
        isLooping: true,
        isMuted: isBackgroundMuted
    })

    const { rewardedInterstitialAd } = useAdContext();


    useEffect(() => {
        rewardedInterstitialAd.load();
    }, [])

    return (
        <SafeAreaView className='flex-1 bg-black flex flex-col mx-10'>
            <View className='flex flex-row justify-between items-center mt-6 w-full'>
                <View>
                    <Instructions />
                </View>
                <View>
                    <SettingsPopup />
                </View>
            </View>
            <View className='space-y-10 flex justify-center items-center flex-[0.9]'>
                <View className=' space-y-10'>
                    <View className="flex flex-row mx-auto" >
                        <Typography className="text-6xl mx-1 font-bold text-blue-500">R</Typography>
                        <Typography className="text-6xl mx-1 font-bold text-yellow-500">O</Typography>
                        <Typography className="text-6xl mx-1 font-bold text-red-500">L</Typography>
                        <Typography className="text-6xl mx-1 font-bold text-green-500">O</Typography>
                        <Typography className="text-6xl mx-1 font-bold text-white-500">C</Typography>
                    </View>
                    <View className='m-auto'>
                        <GameStats />
                    </View>
                    <View className='m-auto mb-14'>
                        <PulsingButton
                            onPress={startGame}
                            color='white'
                        >
                            <Entypo name="controller-play" size={60} color='green' />
                        </PulsingButton>
                    </View>
                    <View className='m-auto'>
                        <GetExtraLivesModal stopSound={stopSound} />
                    </View>

                </View>
            </View>
        </SafeAreaView >
    );
};

export default StartScreen;
