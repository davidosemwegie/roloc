import { GameStats, GetExtraLivesModal, PulsingButton, Screen, SoftButton, Typography } from '@components';
import { useGameStateStore } from '@stores';
import React, { useEffect } from 'react';
import { SafeAreaView, Settings, View } from 'react-native';
import { useAdContext } from '../../layouts';
import { Instructions } from './instructions';
import Entypo from '@expo/vector-icons/Entypo';
import { useSound } from '@utils';
import { SettingsPopup } from './settings';


const StartScreen = () => {
    const { startGame, isBackgroundMuted } = useGameStateStore();

    const { playSound } = useSound('game-start', {
        isLooping: true,
        isMuted: isBackgroundMuted
    })

    const { interstitialAd, rewardedInterstitialAd } = useAdContext();


    useEffect(() => {
        rewardedInterstitialAd.load();
        playSound()
    }, [])

    return (
        <SafeAreaView className='h-full bg-black mx-10'>
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
                            onPress={() => {
                                startGame(() => interstitialAd.load())
                            }}
                            color='white'
                        >
                            <Entypo name="controller-play" size={60} color='green' />
                        </PulsingButton>
                    </View>
                    <View className='mt-10'>
                        <GetExtraLivesModal />
                    </View>

                </View>
            </View>
        </SafeAreaView >
    );
};

export default StartScreen;
