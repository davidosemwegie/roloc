import React, { FC, PropsWithChildren, useEffect, useRef } from 'react';
import { Animated, TouchableOpacity, View } from 'react-native';
import { Typography } from './typography';

export interface PulsingButtonProps {
    onPress: () => void;
    color?: string; // New color prop
    size?: number; // New size prop
}

export const PulsingButton: FC<PropsWithChildren<PulsingButtonProps>> = ({
    onPress,
    children,
    color = 'green', // Default color is green
    size = 48, // Default size is 48 (assuming it's in pixels)
}) => {
    const lettersRef = useRef(null);
    const scale = useRef(new Animated.Value(1)).current; // Animation value

    // Assign a random delay to each letter
    useEffect(() => {
        const letters = lettersRef.current?.children;
        letters?.forEach((letter) => {
            const randomDelay = Math.random() * 1; // generates a random number between 0 and 1
            letter.setNativeProps({
                style: { animationDelay: `${randomDelay}s` },
            });
        });

        // Define pulse animation
        Animated.loop(
            Animated.sequence([
                Animated.timing(scale, {
                    toValue: 1.1,
                    duration: 1000,
                    useNativeDriver: true, // Add This line
                }),
                Animated.timing(scale, {
                    toValue: 1,
                    duration: 1000,
                    useNativeDriver: true, // Add This line
                }),
            ])
        ).start();
    }, []);

    return (
        <View>
            <Animated.View style={{ transform: [{ scale: scale }] }}>
                <TouchableOpacity
                    onPress={onPress}
                    style={{
                        backgroundColor: color, // Use the color prop for background color
                    }}
                    className='flex flex-row justify-center items-center rounded-lg bg-green-600 p-4 max-w-fit'
                >
                    <Typography className={`text-${size / 8}xl font-bold`}>{children}</Typography>
                </TouchableOpacity>
            </Animated.View>
        </View>
    );
};
