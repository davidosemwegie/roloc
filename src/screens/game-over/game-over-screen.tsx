import { PulsingButton, Screen, Typography, useSoundContext } from '@components'
import { getHighscore, trackEvent } from '@fb'
import { useAdContext } from '@layouts'
import { useExtraLifeStore, useGameStateStore } from '@stores'
import React, { useEffect } from 'react'
import { Button, View } from 'react-native'


function shouldShowAd() {
    const num = Math.floor(Math.random() * 3) + 1;

    if (num === 1) {
        return true;
    } else {
        return false;
    }
}


export const GameOverScreen = () => {


    const [showAd] = React.useState(() => shouldShowAd())
    const [highscore, setHighscore] = React.useState(0)
    const [oldHighscore, setOldHighscore] = React.useState(0)

    const { setExtraLives, extraLives } = useExtraLifeStore()


    const { playSound } = useSoundContext()
    const { interstitialAd: {
        isLoaded,
        show,
        load
    } } = useAdContext()

    const { score, startGame, oldHighscore: gameStoreOldHighscore, backToMenu, resumeWithExtraLife, extraLifeUsed } = useGameStateStore()

    useEffect(() => {
        if (showAd) {
            show()
            trackEvent('ad_shown', {
                type: 'interstitial'
            })
        }
        playSound('game-over')
    }, [])

    useEffect(() => {
        setExtraLives()
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
                Game Over {isLoaded && '🚀'}
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
            <PulsingButton
                onPress={() => {
                    startGame(async () => {
                        load()
                    })
                }}
                size={40}
            >
                Play Again
            </PulsingButton>
            {extraLives > 0 && !extraLifeUsed && (
                <View>
                    <Typography className='text-[20px]'>
                        You have {extraLives} extra lives
                    </Typography>
                    <Button
                        title="Use an extra life"
                        onPress={() => {
                            resumeWithExtraLife(async () => {
                                load()
                            })
                        }}
                    />
                </View>
            )}

            <View>
                <Button title="Back to main screen" color='grey' onPress={backToMenu} />
            </View>
        </Screen>
    )
}
