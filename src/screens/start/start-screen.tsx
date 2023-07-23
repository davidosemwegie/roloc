import { Animated, Button } from 'react-native';
import { PulsingButton, Screen, Typography } from '@components';
import { useGameStateStore } from '@stores';
import React, { useEffect, useRef, useState } from 'react';
import { View, TouchableOpacity } from 'react-native';

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
                <View className="flex flex-row mb-10" >
                    <Typography className="text-6xl mx-1 font-bold text-blue-500">R</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-yellow-500">O</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-red-500">L</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-green-500">O</Typography>
                    <Typography className="text-6xl mx-1 font-bold text-white-500">C</Typography>
                </View>
                <PulsingButton onPress={startGame}>
                    PLAY
                </PulsingButton>
                <Typography className="text-2xl m-auto">High Score: {highScore}</Typography>
            </View>
        </Screen>
    );
};

export default StartScreen;