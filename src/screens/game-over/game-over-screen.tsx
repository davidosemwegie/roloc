import { PulsingButton, Screen, Typography } from '@components'
import { useGameStateStore } from '@stores'
import { useSound } from '@utils'
import React, { useEffect } from 'react'
import { Button, View } from 'react-native'
import { TouchableOpacity } from 'react-native-gesture-handler'

export const GameOverScreen = () => {

    const [highscore, setHighscore] = React.useState(0)
    const [oldHighscore, setOldHighscore] = React.useState(0)

    const { playSound } = useSound('game-over')

    const { score, getHighscore, startGame, oldHighscore: gameStoreOldHighscore, showStartScreen } = useGameStateStore()

    useEffect(() => {
        playSound()
    }, [])

    useEffect(() => {
        const getHS = async () => {
            const hs = await getHighscore()
            setHighscore(hs)
            setOldHighscore(gameStoreOldHighscore);
        }
        getHS()
    }, [])

    const hasNewHighscore = score > highscore
    const matchedHighscore = score === highscore
    const beatOldHighscore = score > oldHighscore

    let copy = "You didn't beat your highscore"

    if (hasNewHighscore) {
        copy = "You beat your highscore! 🚀🚀🚀"
    } else if (matchedHighscore) {
        copy = "You matched your highscore!"
    }

    return (
        <Screen className='space-y-10'>
            <Typography className='text-3xl font-bold text-red-500'>
                Game Over
            </Typography>
            <View className='flex items-center'>
                <Typography >
                    Score: {score}
                </Typography>
                {beatOldHighscore && <Typography className='text-sm'>
                    You beat your old highscore of {oldHighscore}!
                </Typography>}
            </View>
            <Typography className='mb-10'>
                High score: {String(highscore)}
            </Typography>
            <PulsingButton onPress={startGame}>
                Play Again
            </PulsingButton>
            <View>
                <Button title="Back to main screen" color='grey' onPress={startGame} />
            </View>
        </Screen>
    )
}
