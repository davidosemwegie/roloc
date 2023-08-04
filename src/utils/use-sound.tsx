import * as React from 'react';
import { Audio } from 'expo-av';
import crashlytics from '@react-native-firebase/crashlytics'


const gameStart = require('../assets/playing.wav')
const gameOver = require('../assets/game-over.wav')
const match = require('../assets/match.wav')

export type Sounds = "game-start" | "game-over" | "match"

export interface SoundOptions {
    isLooping?: boolean,
    isMuted?: boolean
}

export const useSound = (soundToPlay: Sounds, options?: SoundOptions) => {
    const [sound, setSound] = React.useState<Audio.Sound>();


    const muted = options.isMuted

    const soundMap: Record<Sounds, any> = {
        "game-start": gameStart,
        "game-over": gameOver,
        "match": match
    }

    const pathToSoundToPlay = soundMap[soundToPlay]

    async function playSound() {

        try {

            const { sound } = await Audio.Sound.createAsync(pathToSoundToPlay, {
                ...options,
            });

            setSound(sound);

            sound.playAsync();
        } catch (error) {
            crashlytics().recordError(error);
            console.log(error);
        }
    }

    async function stopSound() {
        sound?.stopAsync();
    }

    React.useEffect(() => {
        return sound
            ? () => {
                sound.unloadAsync();
            }
            : undefined;
    }, [sound]);

    // Handle changes in the muted state
    React.useEffect(() => {
        if (sound) {
            sound.setIsMutedAsync(muted); // Set the new mute state for the currently loaded sound
            if (!muted) {
                sound.playAsync(); // If not muted, play the sound again
            }
        }
    }, [muted]);

    return {
        playSound,
        stopSound,
    };
}
