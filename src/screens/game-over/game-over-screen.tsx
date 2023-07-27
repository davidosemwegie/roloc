import { PulsingButton, Screen, Typography, useSoundContext } from '@components'
import { getHighscore } from '@fb'
import { useGameStateStore } from '@stores'
import { useSound } from '@utils'
import React, { useEffect } from 'react'
import { Button, View } from 'react-native'
import { TouchableOpacity } from 'react-native-gesture-handler'

export const GameOverScreen = () => {

    const [highscore, setHighscore] = React.useState(0)
    const [oldHighscore, setOldHighscore] = React.useState(0)

    const { playSound } = useSoundContext()

    const { score, startGame, oldHighscore: gameStoreOldHighscore, backToMenu } = useGameStateStore()

    useEffect(() => {
        playSound('game-over')
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
                {beatOldHighscore && <Typography className='text-lxl font-semibold text-yellow-500 mt-5'>
                    You beat your old highscore of {oldHighscore}!
                </Typography>}
            </View>
            <Typography className='mb-10'>
                High score: {String(Math.max(score, highscore))}
            </Typography>
            <PulsingButton onPress={startGame}>
                Play Again
            </PulsingButton>
            <View>
                <Button title="Back to main screen" color='grey' onPress={backToMenu} />
            </View>
        </Screen>
    )
}
