
import { useGameStateStore } from '@stores'
import { RingColors } from '@types'
import React, { FC } from 'react'
import { View } from 'react-native'

export interface DotProps {
    color: RingColors
}

export const Dot: FC<DotProps> = ({
    color
}) => {

    const { activeColor } = useGameStateStore()

    const disabled = color !== activeColor

    const id = `dot-${color}`

    const style = {
        backgroundColor: color,
        opacity: disabled ? 0.2 : 1
    };


    return (
        <View
            id={id}
            className={`w-20 h-20 rounded-full`}
            style={style}
        >
        </View>
    )
}