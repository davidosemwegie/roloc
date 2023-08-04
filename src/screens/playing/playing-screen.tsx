import { Dot, Ring, Typography } from '@components'
import { GameStates, useExtraLifeStore, useGameStateStore } from '@stores'
import { cn, useSound, } from '@utils'
import React, { useEffect } from 'react'
import { View } from 'react-native'
import { DragProvider } from './drag-provider'
import { useAdContext } from '@providers'

const PlayingScreen = () => {

    const { score, state, ringOrder, dotOrder, isBackgroundMuted, extraLifeUsed, endGame, activeColor } = useGameStateStore()

    const { extraLives, setExtraLives } = useExtraLifeStore();

    const { playSound, stopSound } = useSound('game-start', {
        isLooping: true,
        isMuted: isBackgroundMuted
    })

    useEffect(() => {
        playSound()
        setExtraLives()
    }, [])

    useEffect(() => {
        if (state === GameStates.GAME_OVER) {
            stopSound()
        }
    }, [state])


    const ringColumn = 'flex-1 space-y-[250px] flex-1 flex flex-col'
    const ringGridCell = ''
    const dotColumn = 'flex-1 space-y-10 justify-between flex-1 flex-col h-[200px]'

    // Function to calculate interval time based on the score
    const calculateInterval = (score: number) => {
        // if (score >= 30) return 1000;    // 1 second
        // if (score >= 20) return 1500;    // 1.5 seconds
        // if (score >= 5) return 3000;    // 3 seconds
        return 2500;                    // Default 3 seconds
    };

    useEffect(() => {
        if (state !== GameStates.PLAYING) {
            return;
        }
        const interval = setInterval(() => {
            endGame()
        }, calculateInterval(score));
        return () => clearInterval(interval);
    }, [activeColor]); // Replaced activeColor with state and score


    const shouldShowExtraLifeIcon = !extraLifeUsed && extraLives > 0 && state === GameStates.PLAYING

    return (
        <DragProvider>
            <View className='flex-1 flex flex-col relative'>
                {shouldShowExtraLifeIcon && (
                    <View
                        style={{ position: 'absolute', top: 16, right: 16 }}
                    >
                        <Typography>
                            ❣️
                        </Typography>
                    </View>
                )}

                <View className='m-auto left-0 right-0  absolute top-[5%]'>
                    <Typography className='text-4xl m-auto'>
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
                            <Dot color={dotOrder[0]} />
                            <Dot color={dotOrder[1]} />
                        </View>
                        <View className={cn(dotColumn)}>
                            <Dot color={dotOrder[2]} />
                            <Dot color={dotOrder[3]} />
                        </View>
                    </View>
                </View>
            </View>
        </DragProvider>

    )
}

export default PlayingScreen