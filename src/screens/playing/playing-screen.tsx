import { Dot, Ring, Typography } from '@components'
import { GameStates, useGameStateStore } from '@stores'
import { RingColors } from '@types'
import { cn, useSound } from '@utils'
import React, { useEffect, useState } from 'react'
import { View } from 'react-native'
import { DragProvider } from './drag-provider'

const PlayingScreen = () => {
    const { score, state, endGame, ringOrder, dotOrder } = useGameStateStore()



    const { playSound } = useSound('game-start', {
        looping: true
    })

    useEffect(() => {
        playSound()
        console.log('dotOrder', dotOrder.map(dot => dot.color))
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
        if (state !== GameStates.PLAYING) {
            return;
        }
        const interval = setInterval(() => {
            endGame()
        }, calculateInterval(score));
        return () => clearInterval(interval);
    }, [state, score]); // Replaced activeColor with state and score



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
                                <Ring color={ringOrder[0]} />
                            </View>
                            <View className={cn(ringGridCell, 'justify-start items-end')}>
                                <Ring color={ringOrder[1]} />
                            </View>
                        </View>
                        <View className={cn(ringColumn)}>
                            <View className={cn(ringColumn)}>
                                <View className={cn(ringGridCell, 'justify-end items-start')}>
                                    <Ring color={ringOrder[2]} />
                                </View>
                                <View className={cn(ringGridCell, 'justify-start items-start')}>
                                    <Ring color={ringOrder[3]} />
                                </View>
                            </View>
                        </View>
                    </View>

                    <View className='absolute flex flex-row space-x-10  '>
                        <View className={cn(dotColumn, 'items-end ')}>
                            <Dot color={dotOrder[0].color} id={dotOrder[0].id} />
                            <Dot color={dotOrder[1].color} id={dotOrder[1].id} />
                        </View>
                        <View className={cn(dotColumn)}>
                            <Dot color={dotOrder[2].color} id={dotOrder[2].id} />
                            <Dot color={dotOrder[3].color} id={dotOrder[3].id} />
                        </View>
                    </View>
                </View>
            </View>
        </DragProvider>

    )
}

export default PlayingScreen