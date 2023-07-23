import { Typography } from '@components';
import React, { FC, PropsWithChildren, useEffect, useRef } from 'react'
import { Animated, TouchableOpacity } from 'react-native';

export interface PulsingButtonProps {
    onPress: () => void,
}

export const PulsingButton: FC<PropsWithChildren<PulsingButtonProps>> = ({ onPress, children }) => {

    const lettersRef = useRef(null);
    const scale = useRef(new Animated.Value(1)).current;  // Animation value

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
            ]),
        ).start();

    }, []);

    return (
        <Animated.View
            style={{ transform: [{ scale: scale }] }} // Bind scale to animated value
        >
            <TouchableOpacity
                onPress={onPress}
                className='flex flex-row justify-center items-center rounded-lg bg-green-600 p-4 w-auto'
            >
                <Typography className='text-4xl font-bold'>
                    {children}
                </Typography>
            </TouchableOpacity>

        </Animated.View>
    )
}