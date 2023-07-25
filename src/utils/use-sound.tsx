import * as React from 'react';
import { Audio } from 'expo-av';

const gameStart = require('../assets/game-start.mp3')
const gameOver = require('../assets/game-over.wav')
const match = require('../assets/match.wav')

export type Sounds = "game-start" | "game-over" | "match"

interface SoundOptions {
    looping?: boolean
}

export const useSound = (soundToPlay: Sounds, options?: SoundOptions) => {
    const [sound, setSound] = React.useState<Audio.Sound>();
    const [isMuted, setIsMuted] = React.useState(false);

    const soundMap: Record<Sounds, any> = {
        "game-start": gameStart,
        "game-over": gameOver,
        "match": match
    }

    const pathToSoundToPlay = soundMap[soundToPlay]

    const toggleMute = () => {
        setIsMuted(!isMuted);
    }

    async function playSound() {
        if (isMuted) {
            return;
        }

        const { sound } = await Audio.Sound.createAsync(pathToSoundToPlay, {
            isLooping: options?.looping ?? false
        })

        setSound(sound);

        await sound.playAsync();
    }

    React.useEffect(() => {
        return sound
            ? () => {
                sound.unloadAsync();
            }
            : undefined;
    }, [sound]);


    return {
        playSound,
        toggleMute,
        isMuted
    }
}
