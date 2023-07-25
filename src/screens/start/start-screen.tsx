import { PulsingButton, Screen, Typography } from '@components';
import { calculateAverageScore, calculateTotalScore, getHighscore, getTotalGamesPlayed } from '@fb';
import { useGameStateStore } from '@stores';
import React, { useEffect, useState } from 'react';
import { View } from 'react-native';

const StartScreen = () => {

    const [averageScore, setAverageScore] = useState<number>(0);
    const [totalScore, setTotalScore] = useState<number>(0);
    const [gamesPlayed, setGamesPlayed] = useState<number>(0);
    const [highScore, setHighScore] = useState<number>(0);

    const { startGame } = useGameStateStore();


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

                    <PulsingButton onPress={startGame}>
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
                <View className=''>
                    <Typography className='mb-6'>
                        Stats
                    </Typography>
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
