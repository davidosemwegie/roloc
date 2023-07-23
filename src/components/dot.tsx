import { Typography } from '@components'
import { useDragContext } from '@screens/playing/drag-provider'
import { useGameStateStore } from '@stores'
import { RingColors } from '@types'
import { BoundingBox, useGetBoundingBox } from '@utils'
import React, { FC, useEffect, useRef } from 'react'
import { View, PanResponder, Animated } from 'react-native'

export interface DotProps {
    color: RingColors
}

export const Dot: FC<DotProps> = ({
    color
}) => {

    // const [position, setPosition] = React.useState({ x: 0, y: 0 });
    const [initialPosition, setInitialPosition] = React.useState({ x: 0, y: 0 });

    const ref = useRef<View>(null)


    const pan = useRef(new Animated.ValueXY()).current;

    const { setDraggableItem, setDragging, dragging } = useDragContext();

    const panResponder = useRef(
        PanResponder.create({
            onMoveShouldSetPanResponder: () => true,
            onPanResponderMove: Animated.event([null, { dx: pan.x, dy: pan.y }], { useNativeDriver: false }),
            onPanResponderGrant: () => {
                setDragging(true);
                setDraggableItem({
                    id, pan,
                    position: { x: initialPosition.x, y: initialPosition.y },
                    color
                });
            },
            onPanResponderRelease: (e) => {

                const finalPosition = {
                    x: e.nativeEvent.pageX,
                    y: e.nativeEvent.pageY
                };
                setDraggableItem({
                    id, pan,
                    position: finalPosition,
                    color
                })
                setDragging(false);
                Animated.spring(pan, {
                    toValue: { x: 0, y: 0 },
                    useNativeDriver: false
                }).start();
            },
        }),
    ).current;

    const { activeColor } = useGameStateStore()

    const disabled = color !== activeColor

    const id = `dot-${color}`

    const style = {
        backgroundColor: color,
        opacity: disabled ? 0.2 : 1
    };

    useEffect(() => {
        ref.current.measure((x, y, width, height, pageX, pageY) => {
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
            setInitialPosition({ x: center.x, y: center.y });
        });
    }, [dragging])


    // useEffect(() => {

    //     pan.addListener((value) => {
    //         setDraggableItem({
    //             color,
    //             id,
    //             pan,
    //             position: {
    //                 x: initialPosition.x + value.x,
    //                 y: initialPosition.y + value.y,
    //             }
    //         });
    //     })


    //     return () => {
    //         // Clean up the pan listener when component unmounts
    //         pan.removeAllListeners();
    //     };
    // }, [ref])

    // useEffect(() => {
    //     console.log({ initialPosition })
    // }, [initialPosition])

    return (
        <Animated.View
            style={{
                transform: [{ translateX: pan.x }, { translateY: pan.y }],
            }}
            {...panResponder.panHandlers}
        >
            <View
                ref={ref}
                id={id}
                className={`w-20 h-20 rounded-full`}
                style={style}
            >

            </View>
            {/* <Typography>
                {JSON.stringify({ initialPosition })}
            </Typography> */}
        </Animated.View>
    )
}
