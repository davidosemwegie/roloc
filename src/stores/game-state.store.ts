import { RingColors } from '@types'
import { getRandomEnumValue } from '@utils'
import { getExtraLives, getHighscore, trackEvent, updateGamesArrayWithScore, updateUserHighscore, useExtraLife } from '@fb'
import { setStorageValue } from 'local-storage'
import { create } from 'zustand'



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
    startGame: (callback?: () => void) => void,
    resumeWithExtraLife: (callback?: () => void) => void,
    pauseGame: () => void,
    resumeGame: () => void,
    endGame: (callback?: () => void) => void,
    changeColor: () => void,
    backToMenu: () => void,
    oldHighscore: number;
    showStartScreen: () => void,
    dotOrder: RingColors[],
    ringOrder: RingColors[],
    extraLives: number,
    extraLifeUsed: boolean,

}

function setHighScore(score: number) {
    setStorageValue('highScore', String(score))
}

async function setOldHighScore(score: number) {
    setStorageValue('oldHighScore', String(score))
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


function getCondition(score: number) {

    // When the user score is above 150 we will change the dot order
    if (score > 150) {
        return true
    }

    // When the user score is above 100 and a multiple of 2 we will change the ring order
    if (score > 100 && score % 2 === 0) {
        return true
    }

    // When the user score is above 70 and a multiple of 5 we will change the dot order
    if (score > 70 && score % 5 === 0) {
        return true
    }

    // when user score is above 30 and a multiple of 10 we will change the ring order
    if (score > 30 && score % 10 === 0) {
        return true;
    }

    return false
}



export const useGameStateStore = create<GameStateStore>((set, get) => ({
    score: 0,
    oldHighscore: 0,
    ringOrder: [],
    dotOrder: [],
    extraLives: 0,
    extraLifeUsed: false,

    // After initializing dotOrder...
    startGame: async (callback?: () => void) => {

        if (typeof callback === 'function') callback()


        trackEvent('start_game')

        return set(() => ({
            state: GameStates.PLAYING,
            score: 0,
            activeColor: getRandomEnumValue(RingColors),
            ringOrder: generateRandomRingOrder(),
            dotOrder: generateRandomRingOrder(), // New function to generate dot order with unique ids
            extraLifeUsed: false,
        }))
    },
    activeColor: undefined,
    addPoint: () => {

        const condition = getCondition(get().score)

        return set((state) => ({
            score: state.score + 1,
            activeColor: getRandomEnumValue(RingColors),
            ...(condition && {
                ringOrder: generateRandomRingOrder(),
            }),
        }))
    }
    ,
    state: GameStates.IDLE,
    pauseGame: () => set(() => ({ state: GameStates.PAUSED })),
    resumeGame: () => set(() => ({ state: GameStates.PLAYING, })),
    backToMenu: () => set(() => ({ state: GameStates.IDLE, score: 0, activeColor: undefined })),
    endGame: async (callback?: () => void) => {
        if (typeof callback === 'function') callback()

        const state = get()
        updateGamesArrayWithScore(state.score)

        const parsedHighScore = await getHighscore() || 0



        let newHighScore = parsedHighScore;
        // If current score is higher than high score, update it
        if (state.score > parsedHighScore) {
            newHighScore = state.score;
            await setOldHighScore(parsedHighScore);
            setHighScore(newHighScore)
            updateUserHighscore(newHighScore)
            console.log('new high score')
        }



        await trackEvent('game_over', {
            score: state.score,
        })


        set(() => ({ state: GameStates.GAME_OVER, highScore: newHighScore, oldHighscore: parsedHighScore }));


    },
    resumeWithExtraLife: async (callback?: () => void) => {

        const extraLives = await getExtraLives();
        const { state } = get()

        if (state === GameStates.GAME_OVER && extraLives > 0) {


            await useExtraLife()
                .then(() => {
                    // Deduct one extra life and resume the game
                    if (typeof callback === 'function') callback()

                    set({
                        state: GameStates.PLAYING,
                        extraLifeUsed: true,
                    });
                })
        } else {
            console.error("Cannot resume game: game is not over or no extra lives left.");
        }
    },
    showStartScreen: () => set(() => ({ state: GameStates.IDLE })),

    changeColor: () => set(() => ({ activeColor: getRandomEnumValue(RingColors) })),
}))
