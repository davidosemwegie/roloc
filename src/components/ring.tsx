
import { useDragContext } from '@screens/playing/drag-provider'
import { RingColors } from '@types'
import React, { FC, useEffect, useRef } from 'react'
import { View } from 'react-native'
import { Typography } from './typography'

export interface RingProps {
    color: RingColors
}


import { BoundingBox, } from '@utils'


export const Ring: FC<RingProps> = ({
    color
}) => {

    const [position, setPosition] = React.useState({ x: 0, y: 0 });

    const elementRef = useRef<View>(null);


    const id = `ring-${color}`

    const COLOR = `${color}`

    const { draggableItem } = useDragContext();

    useEffect(() => {
        elementRef.current.measure((x, y, width, height, pageX, pageY) => {
            const topLeft = { x: pageX, y: pageY };
            const topRight = { x: pageX + width, y: pageY };
            const bottomLeft = { x: pageX, y: pageY + height };
            const bottomRight = { x: pageX + width, y: pageY + height };
            const center = { x: pageX + width / 2, y: pageY + height / 2 };
            const boundingBox: BoundingBox = {
                topLeft,
                topRight,
                bottomLeft,
                bottomRight,
                center,
            };
            setPosition({ x: center.x, y: center.y });
        });

    }, [elementRef, position])


    useEffect(() => {
        if (draggableItem) {
            const dotPosition = draggableItem.position;

            console.log('dotPosition', dotPosition)

            // const ringRadius = 140 / 2;

            // // Calculate the distance between the centers of the dot and the ring
            // const dist = Math.hypot(dotPosition.x - position.x, dotPosition.y - position.y);

            // // If the distance is less than or equal to the ring's radius minus the dot's radius, the dot is over the ring
            // if (dist <= ringRadius) {
            //     console.log('Dot has been dropped over the ring.');
            // }

            // If center point of dot is within the ring, then it's a match
        }
    }, [draggableItem]);

    return (
        <>
            <View
                ref={elementRef}
                id={id}
                className={`w-[140px] h-[140px] rounded-full  border-solid border-[20px] `}
                style={{
                    borderColor: `${COLOR}`
                }}
            >
                <Typography className='text-sm'>
                    {JSON.stringify({ position })}
                </Typography>
            </View>

        </>
    )
}