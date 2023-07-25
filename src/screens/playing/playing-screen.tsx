import { Dot, Ring, Screen, Typography } from '@components'
import { GameStates, useDragStore, useGameStateStore } from '@stores'
import { RingColors } from '@types'
import { cn, useSound } from '@utils'
import React, { useEffect, useState } from 'react'
import { View, Text, SafeAreaView } from 'react-native'
import { DragProvider, useDragContext } from './drag-provider'

const PlayingScreen = () => {
    const { score, state, activeColor, endGame, changeColor, startGame, } = useGameStateStore()



    const { playSound } = useSound('game-start', {
        looping: true
    })

    useEffect(() => {
        playSound()
    }, [])

    const ringColumn = 'flex-1 space-y-[250px] flex-1 flex flex-col'
    const ringGridCell = ''
    const dotColumn = 'flex-1 space-y-10 justify-between flex-1 flex-col h-[200px]'

    // Function to calculate interval time based on the score
    const calculateInterval = (score: number) => {
        if (score >= 50) return 1000;    // 1 second
        if (score >= 25) return 1500;    // 1.5 seconds
        if (score >= 10) return 2000;    // 2 seconds
        return 3000;                    // Default 3 seconds
    };

    useEffect(() => {
        startGame()
    }, [])

    const [column1Rings, setColumn1Rings] = useState([RingColors.BLUE, RingColors.PURPLE]);
    const [column2Rings, setColumn2Rings] = useState([RingColors.GREEN, RingColors.RED]);
    const [column1Dots, setColumn1Dots] = useState([RingColors.GREEN, RingColors.BLUE,]);
    const [column2Dots, setColumn2Dots] = useState([RingColors.PURPLE, RingColors.RED]);


    const shuffleArray = (array) => {
        for (let i = array.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [array[i], array[j]] = [array[j], array[i]];
        }
    };

    useEffect(() => {
        startGame();

        // Randomize the order of the rings and dots for each column
        const shuffledColumn1Rings = [...column1Rings];
        const shuffledColumn2Rings = [...column2Rings];
        const shuffledColumn1Dots = [...column1Dots];
        const shuffledColumn2Dots = [...column2Dots];
        shuffleArray(shuffledColumn1Rings);
        shuffleArray(shuffledColumn2Rings);
        shuffleArray(shuffledColumn1Dots);
        shuffleArray(shuffledColumn2Dots);
        setColumn1Rings(shuffledColumn1Rings);
        setColumn2Rings(shuffledColumn2Rings);
        setColumn1Dots(shuffledColumn1Dots);
        setColumn2Dots(shuffledColumn2Dots);
    }, []);



    // useEffect(() => {
    //     if (state !== GameStates.PLAYING) {
    //         return
    //     }
    //     const interval = setInterval(() => {
    //         endGame()
    //     }, calculateInterval(score))
    //     return () => clearInterval(interval)
    // }, [activeColor])


    return (
        <DragProvider>
            <View className='flex-1 flex flex-col relative'>
                <View className='m-auto left-0 right-0  absolute top-[5%]'>
                    <Typography className='text-3xl m-auto'>
                        {score}
                    </Typography>
                </View>
                <View className='flex-1 items-center justify-center '>
                    <View className='flex flex-row space-x-[80px]'>
                        <View className={cn(ringColumn)}>
                            <View className={cn(ringGridCell, 'justify-end items-end h-auto')}>
                                <Ring color={column1Rings[0]} />
                            </View>
                            <View className={cn(ringGridCell, 'justify-start items-end')}>
                                <Ring color={column1Rings[1]} />
                            </View>
                        </View>
                        <View className={cn(ringColumn)}>
                            <View className={cn(ringColumn)}>
                                <View className={cn(ringGridCell, 'justify-end items-start')}>
                                    <Ring color={column2Rings[0]} />
                                </View>
                                <View className={cn(ringGridCell, 'justify-start items-start')}>
                                    <Ring color={column2Rings[1]} />
                                </View>
                            </View>
                        </View>
                    </View>

                    <View className='absolute flex flex-row space-x-10  '>
                        <View className={cn(dotColumn, 'items-end')}>
                            <Dot color={column1Dots[0]} />
                            <Dot color={column1Dots[1]} />
                        </View>
                        <View className={cn(dotColumn)}>
                            <Dot color={column2Dots[0]} />
                            <Dot color={column2Dots[1]} />
                        </View>
                    </View>
                </View>
            </View>
        </DragProvider>

    )
}

export default PlayingScreen