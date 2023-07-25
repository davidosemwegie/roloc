import { useDragContext } from '@screens/playing/drag-provider'
import { useGameStateStore } from '@stores'
import { RingColors } from '@types'
import { FC, useEffect, useRef, useState } from 'react'
import { View, PanResponder, Animated } from 'react-native'
import { Typography } from './typography'

interface DotProps {
    color: RingColors,
}

interface DraggableItem {
    id: string
    pan: Animated.ValueXY
    position: { x: number, y: number }
    color: RingColors
}

export const Dot: FC<DotProps> = ({ color, }) => {
    const [initialPosition, setInitialPosition] = useState<{ x: number, y: number }>({ x: 0, y: 0 });

    const ref = useRef<View>(null)
    const pan = useRef(new Animated.ValueXY()).current;
    const { setDraggableItem, setDragging, dragging } = useDragContext();

    const panResponder = useRef(
        PanResponder.create({
            onMoveShouldSetPanResponder: () => true,
            onPanResponderMove: Animated.event([null, { dx: pan.x, dy: pan.y }], { useNativeDriver: false }),
            onPanResponderGrant: () => {
                setDragging(true);
                setDraggableItem(createDraggableItem());
            },
            onPanResponderRelease: (e) => {
                setDraggableItem(createDraggableItem(e.nativeEvent.pageX, e.nativeEvent.pageY));
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
    const _id = `dot-${color}`
    const style = {
        backgroundColor: color,
        opacity: disabled ? 0.2 : 1
    }

    useEffect(() => {
        ref.current.measure((x, y, width, height, pageX, pageY) => {
            const center = { x: pageX + width / 2, y: pageY + height / 2 };
            setInitialPosition({ x: center.x, y: center.y });
        });
    }, [dragging])

    const createDraggableItem = (x: number = initialPosition.x, y: number = initialPosition.y): DraggableItem => {
        return {
            id: _id,
            pan,
            position: { x, y },
            color
        }
    };

    return (
        <Animated.View
            style={{
                transform: [{ translateX: pan.x }, { translateY: pan.y }],
            }}
            {...panResponder.panHandlers}
        >
            <View
                ref={ref}
                id={_id}
                className={`w-20 h-20 rounded-full`}
                style={style}
            />
        </Animated.View>
    )
}
