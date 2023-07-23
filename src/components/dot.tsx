
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

    // const { attributes, listeners, setNodeRef, transform } = useDraggable({
    //     id,
    //     data: {
    //         color
    //     },
    //     disabled
    // });

    const style = {
        backgroundColor: color,
        opacity: disabled ? 0.2 : 1
    };


    // Disbale dragging if the color is not the active color

    return (
        <View
            id={id}
            // ref={setNodeRef}
            className={`w-20 h-20 rounded-full`}
            style={style}
        // {...listeners}
        // {...attributes}
        >
        </View>
    )
}