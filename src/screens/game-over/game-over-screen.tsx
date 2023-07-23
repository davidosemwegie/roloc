import { PulsingButton, Screen, Typography } from '@components'
import { useGameStateStore } from '@stores'
import React from 'react'

export const GameOverScreen = () => {

    const { score, startGame } = useGameStateStore()

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