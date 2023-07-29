import { PulsingButton, Screen, Typography } from '@components';
import { addExtraLife, calculateAverageScore, calculateTotalScore, getExtraLives, getHighscore, getTotalGamesPlayed } from '@fb';
import { useGameStateStore } from '@stores';
import React, { useEffect, useState } from 'react';
import { Button, View } from 'react-native';
import { useAdContext } from '../../layouts';
import { get } from 'react-native/Libraries/TurboModule/TurboModuleRegistry';

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
                <View className="flex flex-row mb-10 mx-auto" >
                    <Typography className="text-6xl mx-1 font-bold text-blue-500">R</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-yellow-500">O</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-red-500">L</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-green-500">O</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-white-500">C</Typography>
                </View>
                <View className='max-w-[80%] w-full m-auto'>

                    <PulsingButton onPress={() => {
                        startGame(() => interstitialAd.load())
                    }}>
                        PLAY
                    </PulsingButton>
                </View>

                <View className='flex flex-col items-center '>
                    <Typography className='text-center mb-2'>
                        How to play:
                    </Typography>
                    <Typography className='text-center text-[16px]'>
                        Match the color of the ring with the color of the dot.
                    </Typography>
                    <Typography className='text-center text-[16px]'>
                        There are some twists though... so be careful! 😊
                    </Typography>
                </View>
                <View>
                    <Typography className='text-center text-xl'>
                        Extra Lives: {extraLives}
                    </Typography>
                    {rewardedInterstitialAd.isLoaded && <Button
                        title="Get Extra Lives"
                        onPress={() => {
                            rewardedInterstitialAd.show();
                        }}
                    />}

                </View>
                <View className=''>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl">High Score: </Typography>
                        <Typography className="text-xl "> {highScore}</Typography>
                    </View>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl ">Average Score: </Typography>
                        <Typography className="text-xl"> {averageScore}</Typography>
                    </View>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl ">Total Score: </Typography>
                        <Typography className="text-xl "> {totalScore}</Typography>
                    </View>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl ">Games Played: </Typography>
                        <Typography className="text-xl "> {gamesPlayed}</Typography>
                    </View>
                </View>
            </View>
        </Screen>
    );
};

export default StartScreen;
