import { cn } from '@utils'
import React, { FC, PropsWithChildren } from 'react'
import { SafeAreaView, View } from 'react-native'

export interface ScreenProps {
    className?: string,
}

export const Screen: FC<PropsWithChildren<ScreenProps>> = ({ children, className }) => {
    return (
        <SafeAreaView className={cn('flex-1 bg-black flex flex-col justify-center items-center')}>
            <View className={className}>
                {children}
            </View>
        </SafeAreaView>
    )
}