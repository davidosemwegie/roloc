import { PropsWithChildren, createContext, useContext, useEffect, useState } from 'react';
import { Audio } from 'expo-av';

export type Sounds = 'game-start' | 'game-over' | 'match';

export interface SoundPlayingOptions {
    looping: boolean
}

export interface SoundContext {
    playSound: (sound: Sounds, options?: SoundPlayingOptions) => void;
    stopSound: (sound: Sounds) => void;
}

const gameStart = require('../assets/playing.wav');
const gameOver = require('../assets/game-over.wav');
const match = require('../assets/match.wav');

const SoundContext = createContext<SoundContext>({
    playSound: () => { },
    stopSound: () => { },
});

export const useSoundContext = () => useContext(SoundContext);

const SoundProvider: React.FC<PropsWithChildren> = ({ children }) => {
    const [soundMap, setSoundMap] = useState<Record<Sounds, Audio.Sound>>({} as Record<Sounds, Audio.Sound>)

    useEffect(() => {
        const loadSounds = async () => {
            const sounds: Record<Sounds, any> = {
                'game-start': gameStart,
                'game-over': gameOver,
                'match': match,
            };

            const soundsToLoad: Record<Sounds, Audio.Sound> = {} as Record<Sounds, Audio.Sound>;

            for (const key in sounds) {
                const { sound } = await Audio.Sound.createAsync(sounds[key as Sounds], {
                    isLooping: key === 'game-start'
                });
                soundsToLoad[key as Sounds] = sound;
            }

            setSoundMap(soundsToLoad);
        };

        loadSounds();
    }, []);

    const playSound = (sound: Sounds) => {
        soundMap[sound]?.playAsync();
    };

    const stopSound = (sound: Sounds) => {
        soundMap[sound]?.stopAsync();
    };

    return (
        <SoundContext.Provider value={{ playSound, stopSound }}>
            {children}
        </SoundContext.Provider>
    );
};


export default SoundProvider