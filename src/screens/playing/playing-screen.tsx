import { Dot, Ring, Screen, Typography } from '@components'
import { GameStates, useDragStore, useGameStateStore } from '@stores'
import { RingColors } from '@types'
import { cn, useSound } from '@utils'
import React, { useEffect } from 'react'
import { View, Text, SafeAreaView } from 'react-native'
import { DragProvider, useDragContext } from './drag-provider'

const PlayingScreen = () => {
    const { score, state, activeColor, endGame, changeColor, startGame } = useGameStateStore()

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


    useEffect(() => {
        if (state !== GameStates.PLAYING) {
            return
        }
        const interval = setInterval(() => {
            endGame()
        }, calculateInterval(score))
        return () => clearInterval(interval)
    }, [activeColor])


    return (
        <DragProvider>
            <View className='flex-1 flex flex-col relative'>
                <View className='m-auto left-0 right-0  absolute top-[48%]'>
                    <Typography className='text-3xl m-auto'>
                        {score} {state}
                    </Typography>
                </View>
                <View className='flex-1 items-center justify-center '>
                    <View className='flex flex-row space-x-[80px]'>
                        <View className={cn(ringColumn)}>
                            <View className={cn(ringGridCell, 'justify-end items-end h-auto')}>
                                <Ring color={RingColors.BLUE} />
                            </View>
                            <View className={cn(ringGridCell, 'justify-start items-end')}>
                                <Ring color={RingColors.PURPLE} />
                            </View>
                        </View>
                        <View className={cn(ringColumn)}>
                            <View className={cn(ringColumn)}>
                                <View className={cn(ringGridCell, 'justify-end items-start')}>
                                    <Ring color={RingColors.GREEN} />
                                </View>
                                <View className={cn(ringGridCell, 'justify-start items-start')}>
                                    <Ring color={RingColors.RED} />
                                </View>
                            </View>
                        </View>
                    </View>

                    <View className='absolute flex flex-row space-x-10  '>
                        <View className={cn(dotColumn, 'items-end')}>
                            <Dot color={RingColors.BLUE} />
                            <Dot color={RingColors.GREEN} />
                        </View>
                        <View className={cn(dotColumn)}>
                            <Dot color={RingColors.PURPLE} />
                            <Dot color={RingColors.RED} />
                        </View>
                    </View>
                </View>
            </View>
        </DragProvider>

    )
}

export default PlayingScreen