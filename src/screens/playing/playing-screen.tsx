import { Screen } from '@components'
import { useSound } from '@utils'
import React, { useEffect } from 'react'
import { View, Text } from 'react-native'

const PlayingScreen = () => {

    const { playSound } = useSound('game-start', {
        looping: true
    })


    useEffect(() => {
        playSound()
    }, [])


    return (
        <Screen>
            <Text className='text-white'>Playing screen</Text>
        </Screen>
    )
}

export default PlayingScreen