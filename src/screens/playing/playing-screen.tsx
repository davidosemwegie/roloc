import { Dot, Ring, Screen, Typography } from '@components'
import { useGameStateStore } from '@stores'
import { RingColors } from '@types'
import { cn, useSound } from '@utils'
import React, { useEffect } from 'react'
import { View, Text, SafeAreaView } from 'react-native'
import { DragProvider } from './drag-provider'

const PlayingScreen = () => {
    const { score } = useGameStateStore()

    const { playSound } = useSound('game-start', {
        looping: true
    })

    // useEffect(() => {
    //     playSound()
    // }, [])

    const ringColumn = 'flex-1 space-y-[250px] flex-1 flex flex-col'
    const ringGridCell = ''
    const dotColumn = 'flex-1 space-y-10 justify-between flex-1 flex-col h-[200px]'

    return (
        <DragProvider>
            <View className='flex-1 flex flex-col relative'>
                <View className='m-auto left-0 right-0  absolute top-[48%]'>
                    <Typography className='text-3xl m-auto'>
                        {score}
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