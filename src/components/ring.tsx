
import { useDragContext } from '@screens/playing/drag-provider'
import { RingColors } from '@types'
import React, { FC, useEffect, useLayoutEffect, useRef } from 'react'
import { Alert, View } from 'react-native'
import { Typography } from './typography'

export interface RingProps {
    color: RingColors
}


import { BoundingBox, useSound, } from '@utils'
import { useDragStore, useGameStateStore } from '@stores'


export const Ring: FC<RingProps> = ({
    color
}) => {

    const [position, setPosition] = React.useState({ x: 0, y: 0 });

    const elementRef = useRef<View>(null);

    const [boundingBox, setBoundingBox] = React.useState<BoundingBox | null>(null);
    const { activeColor, addPoint, startGame, endGame } = useGameStateStore()

    const { playSound: playMatchSound } = useSound('match')



    const id = `ring-${color}`

    const COLOR = `${color}`

    const { draggableItem, dragging, setDraggableItem } = useDragContext();

    useLayoutEffect(() => {
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

            setBoundingBox(boundingBox);
        });

    }, [elementRef, position, dragging])

    async function checker() {
        if (draggableItem && !dragging && draggableItem.color === color) {

            console.log("Run checker", color)

            const dotPosition = draggableItem.position


            const bottomLimit = boundingBox?.bottomLeft.y;
            const topLimit = boundingBox?.topLeft.y;
            const leftLimit = boundingBox?.topLeft.x;
            const rightLimit = boundingBox?.topRight.x;



            const isWithinX = dotPosition?.x >= leftLimit && dotPosition?.x <= rightLimit;
            const isWithinY = dotPosition?.y >= topLimit && dotPosition?.y <= bottomLimit;

            const isColorMatch = draggableItem.color === activeColor;

            const isWithin = isWithinX && isWithinY && isColorMatch;


            if (isWithin) {
                console.log('Dot has been dropped over the ring.');
                addPoint()
                playMatchSound()
            } else {
                console.log('Dot has been dropped outside the ring.');
                await endGame()
                // Alert.alert('Game Over', 'You missed the ring', [
                //     {
                //         text: 'Try Again',
                //         onPress: () => {
                //             startGame()
                //         }
                //     }
                // ])
            }

            setDraggableItem(null);
        }
    }


    useEffect(() => {
        checker()
    }, [dragging]);

    return (
        <View
            ref={elementRef}
            className='bg-red-500 flex justify-center items-center'>
            <View
                ref={elementRef}
                id={id}
                className={`w-[140px] h-[140px] rounded-full  border-solid border-[20px] `}
                style={{
                    borderColor: `${COLOR}`
                }}
            >
                {/* <Typography className='text-sm'>
                    {JSON.stringify({ boundingBox })}
                </Typography> */}
            </View>

        </View>
    )
}