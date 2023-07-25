import { RingColors } from '@types'
import { getRandomEnumValue } from '@utils'
import { getStorageValue, setStorageValue } from 'local-storage'
import { create } from 'zustand'

interface DotState {
    id: string;
    color: RingColors;
}

export enum GameStates {
    IDLE = 'idle',
    PLAYING = 'playing',
    PAUSED = 'paused',
    GAME_OVER = 'game-over'
}


interface GameStateStore {
    score: number
    addPoint: () => void,
    activeColor?: RingColors,
    state: GameStates,
    startGame: () => void,
    pauseGame: () => void,
    resumeGame: () => void,
    endGame: (callback?: () => void) => void,
    changeColor: () => void,
    backToMenu: () => void,
    getHighscore: () => Promise<number>,
    oldHighscore: number;
    showStartScreen: () => void,
    dotOrder: DotState[],
    ringOrder: RingColors[],

}

function setHighScore(score: number) {
    setStorageValue('highScore', String(score))
}

async function setOldHighScore(score: number) {
    setStorageValue('oldHighScore', String(score))
}

async function getHighScore(): Promise<number> {
    const value = await getStorageValue<string>('highScore')
    return parseInt(value)
}

function generateRandomRingOrder(): RingColors[] {
    const colorsArray: RingColors[] = Object.values(RingColors);
    const shuffledColors: RingColors[] = [];

    while (colorsArray.length > 0) {
        const randomIndex = Math.floor(Math.random() * colorsArray.length);
        const randomColor = colorsArray.splice(randomIndex, 1)[0];
        shuffledColors.push(randomColor);
    }

    return shuffledColors;
}

function generateRandomDotOrder(): DotState[] {
    const colorsArray: RingColors[] = Object.values(RingColors);
    const shuffledDots: DotState[] = [];

    while (colorsArray.length > 0) {
        const randomIndex = Math.floor(Math.random() * colorsArray.length);
        const randomColor = colorsArray.splice(randomIndex, 1)[0];
        shuffledDots.push({ id: `dot-${randomColor}`, color: randomColor });
    }
    console.log("shuffled: ", shuffledDots.map((dot) => dot.color))

    return shuffledDots;
}



export const useGameStateStore = create<GameStateStore>((set, get) => ({
    score: 0,
    oldHighscore: 0,
    ringOrder: [],
    dotOrder: [],

    // After initializing dotOrder...
    startGame: () => set(() => ({
        state: GameStates.PLAYING,
        score: 0,
        activeColor: getRandomEnumValue(RingColors),
        ringOrder: generateRandomRingOrder(),
        dotOrder: generateRandomDotOrder(), // New function to generate dot order with unique ids
    })),
    getHighscore: async () => {
        const value = await getStorageValue<string>('highScore') || '0'
        return parseInt(value)
    },
    activeColor: undefined,
    addPoint: () => set((state) => ({
        score: state.score + 1,
        activeColor: getRandomEnumValue(RingColors),
        ...(state.score > 20 && {
            ringOrder: generateRandomRingOrder()
        }),
    }))
    ,
    state: GameStates.IDLE,
    pauseGame: () => set(() => ({ state: GameStates.PAUSED })),
    resumeGame: () => set(() => ({ state: GameStates.PLAYING, })),
    backToMenu: () => set(() => ({ state: GameStates.IDLE, score: 0, activeColor: undefined })),
    endGame: async (callback?: () => void) => {
        if (typeof callback === 'function') callback()

        const state = get()

        const parsedHighScore = await getHighScore() || 0

        let newHighScore = parsedHighScore;
        // If current score is higher than high score, update it
        if (state.score > parsedHighScore) {
            newHighScore = state.score;
            await setOldHighScore(parsedHighScore);
            setHighScore(newHighScore)
        }

        set(() => ({ state: GameStates.GAME_OVER, highScore: newHighScore, oldHighscore: parsedHighScore }));
    },
    showStartScreen: () => set(() => ({ state: GameStates.IDLE })),

    changeColor: () => set(() => ({ activeColor: getRandomEnumValue(RingColors) })),
}))
