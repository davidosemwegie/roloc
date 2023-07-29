import firestore, { FirebaseFirestoreTypes } from '@react-native-firebase/firestore';
import auth from '@react-native-firebase/auth';
import crashlytics from '@react-native-firebase/crashlytics'
import analytics from '@react-native-firebase/analytics';


interface Game {
    score: number;
    endTime: FirebaseFirestoreTypes.Timestamp
}

const isProduction = process.env.NODE_ENV === 'production';

export async function trackEvent(eventName: string, eventParams?: any) {
    try {
        if (isProduction) {
            await analytics().logEvent(eventName, eventParams);
        }
    } catch (error) {
        if (isProduction) {
            crashlytics().recordError(error);
            crashlytics().recordError(error);
            console.log(error);
        }
    }
}


export async function updateGamesArrayWithScore(score: number) {
    const user = auth().currentUser;

    console.log({ user });

    try {
        if (user) {
            const userRef = firestore().collection('scores').doc(user.uid);
            const userDoc = await userRef.get();

            if (userDoc.exists) {
                const gamesArray: Game[] = (userDoc.get('games') as unknown as Game[]) || [];

                const newGame: Game = {
                    score: score,
                    endTime: firestore.Timestamp.now(),
                };

                gamesArray.push(newGame);

                await userRef.update({
                    games: gamesArray,
                });

                console.log("Games array updated");
            } else {
                // User document does not exist in the scores collection, create a new one
                const newGame: Game = {
                    score: score,
                    endTime: firestore.Timestamp.now(),
                };

                const newUserDoc = {
                    games: [newGame],
                };

                await userRef.set(newUserDoc);

                console.log('User added to the scores collection');
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }
}


export async function updateUserHighscore(score: number) {
    const user = auth().currentUser;


    try {
        if (user) {
            const scoresRef = firestore().collection('scores');
            const userScoreRef = scoresRef.doc(user.uid);
            const userScoreDoc = await userScoreRef.get();

            if (userScoreDoc.exists) {
                // User score record already exists, update the highscore
                const currentHighscore = userScoreDoc.get('high-score') as number ?? 0;
                if (score > currentHighscore) {
                    await userScoreRef.update({
                        'high-score': score,
                    });
                    console.log('Highscore updated in the database');
                } else {
                    console.log('Score is not higher than the current highscore. No update needed.');
                }
            } else {
                // User score record doesn't exist, create a new one
                await userScoreRef.set({
                    'high-score': score,
                });
                console.log('New Highscore added to database');
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }
}

export async function calculateAverageScore(): Promise<number> {
    const user = auth().currentUser;

    try {
        if (user) {
            const userRef = firestore().collection('scores').doc(user.uid);
            const userDoc = await userRef.get();

            if (userDoc.exists) {
                const gamesArray: Game[] = (userDoc.get('games') as unknown as Game[]) || [];

                if (gamesArray.length === 0) {
                    // No games available, return 0 as average score
                    return 0;
                }

                // Calculate the sum of scores
                const totalScore = gamesArray.reduce((acc, game) => acc + game.score, 0);

                // Calculate the average score
                const averageScore = totalScore / gamesArray.length;

                // Return the average score to 2 decimal places

                return Math.round(averageScore * 100) / 100;
            } else {
                // User document does not exist in the scores collection, return 0 as average score
                return 0;
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    // In case of an error or no user, return 0 as average score
    return 0;
}

export async function calculateTotalScore(): Promise<number> {
    const user = auth().currentUser;

    try {
        if (user) {
            const userRef = firestore().collection('scores').doc(user.uid);
            const userDoc = await userRef.get();

            if (userDoc.exists) {
                const gamesArray: Game[] = (userDoc.get('games') as unknown as Game[]) || [];

                // Calculate the sum of all scores
                const totalScore = gamesArray.reduce((acc, game) => acc + game.score, 0);

                return totalScore;
            } else {
                // User document does not exist in the scores collection, return 0 as total score
                return 0;
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    // In case of an error or no user, return 0 as total score
    return 0;
}

export async function getTotalGamesPlayed(): Promise<number> {
    const user = auth().currentUser;

    try {
        if (user) {
            const userRef = firestore().collection('scores').doc(user.uid);
            const userDoc = await userRef.get();

            if (userDoc.exists) {
                const gamesArray: Game[] = (userDoc.get('games') as unknown as Game[]) || [];

                // Get the total number of games played (length of the games array)
                const totalGamesPlayed = gamesArray.length;

                return totalGamesPlayed;
            } else {
                // User document does not exist in the scores collection, return 0 as total games played
                return 0;
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    // In case of an error or no user, return 0 as total games played
    return 0;
}


export async function getHighscore(): Promise<number> {
    const user = auth().currentUser;

    try {
        if (user) {
            const userRef = firestore().collection('scores').doc(user.uid);
            const userDoc = await userRef.get();

            if (userDoc.exists) {
                // Get the high-score field from the user's document
                const highscore = userDoc.get('high-score') as number;

                return highscore;
            } else {
                // User document does not exist in the scores collection, return 0 as highscore
                return 0;
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    // In case of an error or no user, return 0 as highscore
    return 0;
}