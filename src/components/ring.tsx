
import { RingColors } from '@types'
import React, { FC } from 'react'
import { View } from 'react-native'

export interface RingProps {
    color: RingColors
}

export const Ring: FC<RingProps> = ({
    color
}) => {

    const id = `ring-${color}`

    // const { setNodeRef } = useDroppable({
    //     id,
    //     data: {
    //         color
    //     },
    // });


    const COLOR = `${color}`



    return (
        <View
            // ref={setNodeRef}
            id={id}
            className={`w-[140px] h-[140px] rounded-full  border-solid border-[20px] `}
            style={{
                borderColor: `${COLOR}`
            }}
        >

        </View>
    )
}