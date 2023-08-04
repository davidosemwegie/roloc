import { PulsingButton, Screen, Typography } from '@components'
import { getHighscore, trackEvent } from '@fb'
import { useAdContext } from '@providers'
import { useGameStateStore } from '@stores'
import { useSound } from '@utils'
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


    const { score, startGame, oldHighscore: gameStoreOldHighscore, backToMenu, isGameOverSoundMuted } = useGameStateStore()
    const { playSound, stopSound } = useSound('game-over', {
        isMuted: isGameOverSoundMuted
    })

    const [showAd] = React.useState(() => shouldShowAd())
    const [highscore, setHighscore] = React.useState(0)
    const [oldHighscore, setOldHighscore] = React.useState(0)

    const getHS = async () => {
        const hs = await getHighscore()
        setHighscore(hs ?? 0)
        setOldHighscore(gameStoreOldHighscore);
    }

    const {
        interstitialAd: {
            isLoaded,
            show,
            load
        },
        shouldShowAds
    } = useAdContext()

    useEffect(() => {
        if (showAd && shouldShowAds && isLoaded) {
            stopSound()
            show()
            trackEvent('ad_shown', {
                type: 'interstitial'
            })
        }
    }, [isLoaded])


    useEffect(() => {
        load()
        playSound()
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
                    startGame(() => {
                        stopSound()
                    })
                }}
                size={40}
            >
                Play Again
            </PulsingButton>
            {/* {extraLives > 0 && !extraLifeUsed && (
                <View>
                    <Typography className='text-[20px] mb-4'>
                        You have {extraLives} extra lives
                    </Typography>

                    <TouchableOpacity
                        onPress={() => {
                            resumeWithExtraLife(async () => {
                                load()
                            })
                        }}
                        className="bg-gray-900 w-auto p-4 rounded-lg flex flex-row space-x-3 items-center justify-center m-auto"
                        style={{
                            opacity: 1,
                        }}
                    >
                        <Typography style={{
                            fontSize: 16,
                        }}>Use an extra life 🔄
                        </Typography>
                    </TouchableOpacity>
                </View>
            )} */}

            <View>
                <Button title="Back to main screen" color='grey' onPress={backToMenu} />
            </View>
        </Screen>
    )
}
