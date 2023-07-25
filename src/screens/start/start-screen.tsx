import { PulsingButton, Screen, Typography } from '@components';
import { useGameStateStore } from '@stores';
import React, { useEffect, useState } from 'react';
import { View } from 'react-native';

const StartScreen = () => {
    const { startGame, getHighscore } = useGameStateStore();

    const [highScore, setHighScore] = useState<number>(0);

    useEffect(() => {
        async function loadHighScore() {
            const fetchedHighScore = await getHighscore();
            setHighScore(fetchedHighScore);
        }

        loadHighScore();
    }, []);



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
                <Typography className="text-2xl m-auto">High Score: {highScore}</Typography>
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
            </View>
        </Screen>
    );
};

export default StartScreen;
