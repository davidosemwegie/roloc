import { cn } from '@utils'
import React, { FC, PropsWithChildren } from 'react'
import { SafeAreaView, View } from 'react-native'

export interface ScreenProps {
    className?: string,
}

export const Screen: FC<PropsWithChildren<ScreenProps>> = ({ children, className }) => {
    return (
        <View className={cn('flex-1 bg-black flex flex-col justify-center', className)}>
            {children}
        </View>
    )
}