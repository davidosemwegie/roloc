import { GameStats, GetExtraLivesModal, PulsingButton, Screen, Typography } from '@components';
import { addExtraLife, calculateAverageScore, calculateTotalScore, getExtraLives, getHighscore, getTotalGamesPlayed } from '@fb';
import { useGameStateStore } from '@stores';
import React, { useEffect, useState } from 'react';
import { Button, View } from 'react-native';
import { useAdContext } from '../../layouts';
import { get } from 'react-native/Libraries/TurboModule/TurboModuleRegistry';
import { Instructions } from './instructions';

const StartScreen = () => {

    const [averageScore, setAverageScore] = useState<number>(0);
    const [totalScore, setTotalScore] = useState<number>(0);
    const [gamesPlayed, setGamesPlayed] = useState<number>(0);
    const [highScore, setHighScore] = useState<number>(0);
    const [extraLives, setExtraLives] = useState<number>(0);

    const { interstitialAd, rewardedInterstitialAd } = useAdContext();

    const { startGame } = useGameStateStore();

    useEffect(() => {
        interstitialAd.load();
        rewardedInterstitialAd.load();
    }, [])

    // Function to update extra lives by fetching from the server
    const updateExtraLives = async () => {
        await addExtraLife().then((res) => {
            setExtraLives(extraLives + 1)
        });

    };

    // when the reward is closed refetch the extra lives
    useEffect(() => {
        // Check if the rewarded ad is closed and a reward is earned
        if (rewardedInterstitialAd.isClosed && rewardedInterstitialAd.isEarnedReward) {
            // Call the function to update extra lives
            updateExtraLives();

        }

    }, [rewardedInterstitialAd.isClosed, rewardedInterstitialAd.isEarnedReward])


    useEffect(() => {
        async function getStats() {
            const averageScore = await calculateAverageScore();
            setAverageScore(averageScore);

            const totalScore = await calculateTotalScore();
            setTotalScore(totalScore);

            const gamesPlayed = await getTotalGamesPlayed();
            setGamesPlayed(gamesPlayed);

            const fetchedHighScore = await getHighscore();
            setHighScore(fetchedHighScore);

            const extraLives = await getExtraLives();
            setExtraLives(extraLives);
        }

        getStats();
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
