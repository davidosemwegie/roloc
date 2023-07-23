import { PulsingButton, Screen, Typography } from '@components'
import { useGameStateStore } from '@stores'
import { useSound } from '@utils'
import React, { useEffect } from 'react'

export const GameOverScreen = () => {

    const [highscore, setHighscore] = React.useState(0)

    const { playSound } = useSound('game-over')

    const { score, getHighscore, startGame } = useGameStateStore()

    useEffect(() => {
        playSound()
    }, [])

    useEffect(() => {
        const getHS = async () => {
            const hs = await getHighscore()
            setHighscore(hs)
        }
        getHS()
    }, [])


    return (
        <Screen className='space-y-10'>
            <Typography>
                Game Over 😔
            </Typography>
            <Typography className='mb-10'>
                Score: {score}
            </Typography>
            <Typography className='mb-10'>
                High score: {String(highscore)}
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