import { GameOverScreen } from '@screens/game-over/game-over-screen'
import PlayingScreen from '@screens/playing/playing-screen'
import StartScreen from '@screens/start/start-screen'
import { GameStates, useGameStateStore } from '@stores'
import React from 'react'
import { Text, View } from 'react-native'

export const MainLayout = () => {

    const { state } = useGameStateStore()


    let ACTIVE_SCREEN = null

    switch (state as GameStates) {
        case GameStates.IDLE:
            ACTIVE_SCREEN = <StartScreen />
            break
        case GameStates.PLAYING:
            ACTIVE_SCREEN = <PlayingScreen />
            break
        case GameStates.GAME_OVER:
            ACTIVE_SCREEN = <GameOverScreen />
            break
        default:
            ACTIVE_SCREEN = <StartScreen />
            break
    }

    return (
        <View className='bg-black flex-1 w-full text-white'>
            {ACTIVE_SCREEN}
        </View>
    )
}