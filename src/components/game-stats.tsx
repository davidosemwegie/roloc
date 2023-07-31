import { calculateAverageScore, calculateTotalScore, getTotalGamesPlayed, getHighscore } from "@fb";
import { useEffect, useState } from "react";
import { View } from 'react-native';
import { Typography } from "./typography";
import { useExtraLifeStore } from "@stores";

const LoadingBar = () => (
    <Typography className="text-sm">
        Loading...
    </Typography>
);

export const GameStats = () => {
    const [averageScore, setAverageScore] = useState<number>(0);
    const [totalScore, setTotalScore] = useState<number>(0);
    const [gamesPlayed, setGamesPlayed] = useState<number>(0);
    const [highScore, setHighScore] = useState<number>(0);

    const [loadingAverageScore, setLoadingAverageScore] = useState<boolean>(true);
    const [loadingTotalScore, setLoadingTotalScore] = useState<boolean>(true);
    const [loadingGamesPlayed, setLoadingGamesPlayed] = useState<boolean>(true);
    const [loadingHighScore, setLoadingHighScore] = useState<boolean>(true);

    const { setExtraLives, extraLives } = useExtraLifeStore();

    useEffect(() => {
        async function getStats() {
            try {
                setLoadingAverageScore(true);
                const averageScore = await calculateAverageScore();
                setAverageScore(averageScore);
                setLoadingAverageScore(false);

                setLoadingTotalScore(true);
                const totalScore = await calculateTotalScore();
                setTotalScore(totalScore);
                setLoadingTotalScore(false);

                setLoadingGamesPlayed(true);
                const gamesPlayed = await getTotalGamesPlayed();
                setGamesPlayed(gamesPlayed);
                setLoadingGamesPlayed(false);

                setLoadingHighScore(true);
                const fetchedHighScore = await getHighscore();
                setHighScore(fetchedHighScore);
                setLoadingHighScore(false);

            } catch (error) {
                console.error("Error fetching game statistics:", error);
            }
        }

        getStats();
    }, []);

    const showStats = gamesPlayed >= 10;

    return (
        <View>
            {showStats && (
                <>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl">High Score: </Typography>
                        {loadingHighScore ? <LoadingBar /> : <Typography className="text-xl "> {highScore}</Typography>}
                    </View>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl ">Average Score: </Typography>
                        {loadingAverageScore ? <LoadingBar /> : <Typography className="text-xl"> {averageScore}</Typography>}
                    </View>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl ">Total Score: </Typography>
                        {loadingTotalScore ? <LoadingBar /> : <Typography className="text-xl "> {totalScore}</Typography>}
                    </View>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl ">Games Played: </Typography>
                        {loadingGamesPlayed ? <LoadingBar /> : <Typography className="text-xl "> {gamesPlayed}</Typography>}
                    </View>
                </>
            )}

            {!showStats && !loadingHighScore && !loadingAverageScore && !loadingTotalScore && !loadingGamesPlayed && (
                <View className="space-y-3">
                    <Typography className="text-xl">Play 10 games to unlock more stats 🔓</Typography>
                    <View className='flex flex-row justify-between'>
                        <Typography className="text-xl ">Games Played: </Typography>
                        {loadingGamesPlayed ? <LoadingBar /> : <Typography className="text-xl "> {gamesPlayed}</Typography>}
                    </View>
                </View>
            )}
        </View>
    );
};
