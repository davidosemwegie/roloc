import { PulsingButton, Screen, Typography } from '@components'
import { useGameStateStore } from '@stores'
import { useSound } from '@utils'
import React, { useEffect } from 'react'

export const GameOverScreen = () => {

    const { playSound } = useSound('game-over')

    const { score, startGame } = useGameStateStore()

    useEffect(() => {
        playSound()
    }, [])

    return (
        <Screen className='space-y-10'>
            <Typography>
                Game Over 😔
            </Typography>
            <Typography className='mb-10'>
                Score: {score}
            </Typography>
            <PulsingButton onPress={startGame}>
                Play Again
            </PulsingButton>
            <Typography className='text-underline'>
                Back to main screen
            </Typography>

        </Screen>
    )
}