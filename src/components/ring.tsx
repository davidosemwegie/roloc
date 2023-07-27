
import { useDragContext } from '@screens/playing/drag-provider'
import { RingColors } from '@types'
import React, { FC, useEffect, useLayoutEffect, useRef } from 'react'
import { Alert, View } from 'react-native'
import { Typography } from './typography'

export interface RingProps {
    color: RingColors
}


import { BoundingBox, useSound, } from '@utils'
import { useGameStateStore } from '@stores'
import { useSoundContext } from './sound-provider'


export const Ring: FC<RingProps> = ({
    color
}) => {

    const [isRing, setIsInRing] = React.useState(false)


    const elementRef = useRef<View>(null);

    const [boundingBox, setBoundingBox] = React.useState<BoundingBox | null>(null);
    const { activeColor, addPoint, endGame } = useGameStateStore()

    const { playSound } = useSoundContext()



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

    }, [elementRef, dragging])

    async function checker() {
        if (draggableItem && !dragging && activeColor === color) {


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
                setIsInRing(true)
                addPoint()
                playSound('match')
            } else {
                setIsInRing(false)
                endGame()
            }

            setDraggableItem(null);
        }
    }


    useEffect(() => {
        checker()
        setIsInRing(false)

    }, [dragging]);

    return (
        <View
            ref={elementRef}
            className=' flex justify-center items-center'
            id={id}
        >
            <View
                ref={elementRef}
                id={id}
                className={`w-[140px] h-[140px] rounded-full  border-solid border-[20px] `}
                style={{
                    borderColor: `${COLOR}`
                }}
            >
            </View>

        </View>
    )
}