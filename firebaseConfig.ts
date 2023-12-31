import firestore, { FirebaseFirestoreTypes } from '@react-native-firebase/firestore';
import auth from '@react-native-firebase/auth';
import crashlytics from '@react-native-firebase/crashlytics'
import analytics from '@react-native-firebase/analytics';

import { Mixpanel } from 'mixpanel-react-native'
import { requestTrackingPermissionsAsync, getTrackingPermissionsAsync } from 'expo-tracking-transparency';



const mixPanelToken = __DEV__ ? 'd28e4c5d2cd75b5de542b6fc9fa052a4' : 'c45110ff303aad488299c3067a3ef433'

export const mixpanel = new Mixpanel(mixPanelToken, true);



interface Game {
    score: number;
    endTime: FirebaseFirestoreTypes.Timestamp
}


export async function trackEvent(eventName: string, eventParams?: any) {
    try {
        if (!__DEV__) {
            await analytics().logEvent(eventName, eventParams);
        }
        mixpanel.track(eventName, eventParams);
    } catch (error) {
        if (!__DEV__) {
            crashlytics().recordError(error);
            crashlytics().recordError(error);
            console.log(error);
        }
    }
}


export async function updateGamesArrayWithScore(score: number) {
    const user = auth().currentUser;

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
                    trackEvent('highscore_updated', {
                        oldHighscore: currentHighscore,
                        newHighscore: score,
                    })
                }
            } else {
                // User score record doesn't exist, create a new one
                await userScoreRef.set({
                    'high-score': score,
                });
                trackEvent('highscore_updated', {
                    oldHighscore: 0,
                    newHighscore: score,
                })
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

export async function addExtraLife(): Promise<void> {
    const user = auth().currentUser;

    try {
        if (user) {
            const playerRef = firestore().collection('players').doc(user.uid);
            const playerDoc = await playerRef.get();

            if (playerDoc.exists) {
                // Player document exists, increment the extra_lives field by 1
                await playerRef.update({
                    extra_lives: firestore.FieldValue.increment(1),
                });

                console.log('Extra life added');
            } else {
                // Player document does not exist, create a new one with extra_lives field set to 1
                await playerRef.set({
                    extra_lives: 1,
                });

                console.log('New player added and extra life given');
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }
}


export async function getExtraLives(): Promise<number> {
    const user = auth().currentUser;

    try {
        if (user) {
            const playerRef = firestore().collection('players').doc(user.uid);
            const playerDoc = await playerRef.get();

            if (playerDoc.exists) {
                // Player document exists, get the extra_lives field
                const extraLives = playerDoc.get('extra_lives') as number;

                return extraLives;
            } else {
                // Player document does not exist in the players collection, return 0 as extra lives
                return 0;
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    // In case of an error or no user, return 0 as extra lives
    return 0;
}


export async function useExtraLife(): Promise<void> {
    const user = auth().currentUser;

    try {
        if (user) {
            const playerRef = firestore().collection('players').doc(user.uid);
            const playerDoc = await playerRef.get();

            if (playerDoc.exists) {
                // Player document exists, decrement the extra_lives field by 1
                const currentExtraLives = playerDoc.get('extra_lives') as number;

                if (currentExtraLives > 0) {
                    await playerRef.update({
                        extra_lives: firestore.FieldValue.increment(-1),
                    });

                    console.log('Extra life used');
                } else {
                    console.log('No extra lives left to use');
                }
            } else {
                // Player document does not exist in the players collection, log error
                console.log('No player document found for current user');
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }
}

export async function setUserEmail(email: string): Promise<void> {
    const user = auth().currentUser;

    try {
        if (user) {
            const userRef = firestore().collection('players').doc(user.uid);

            await userRef.update({
                email: email,
            });

            console.log('User email updated in the database');
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }
}

export async function getUserEmail(): Promise<string | null> {
    const user = auth().currentUser;

    try {
        if (user) {
            const userRef = firestore().collection('players').doc(user.uid);
            const userDoc = await userRef.get();

            if (userDoc.exists) {
                // Get the email field from the user's document
                const email = userDoc.get('email') as string;

                return email;
            } else {
                // User document does not exist in the users collection, return null for email
                return null;
            }
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    // In case of an error or no user, return null for email
    return null;
}


export async function getShouldShowAds(): Promise<boolean> {
    try {
        const configRef = firestore().collection('config').doc('ads');
        const configDoc = await configRef.get();

        if (configDoc.exists) {
            // Get the show_ads field from the config's document
            const showAds = configDoc.get('show_ads') as boolean;

            // Return the value of show_ads
            return showAds;
        } else {
            // Config document does not exist, return false as default
            console.log('Config document does not exist in the collection');
            return false;
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    // In case of an error, don't show ads
    return false;
}

export async function getShouldGetFaster(): Promise<boolean> {
    try {
        const configRef = firestore().collection('config').doc('game');
        const configDoc = await configRef.get();

        if (configDoc.exists) {
            const shouldGetFaster = configDoc.get('should_get_faster') as boolean;

            console.log('shouldGetFaster', shouldGetFaster);

            return shouldGetFaster;
        } else {
            console.log('Config document does not exist in the collection');
            return false;
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    return false;
}


export async function getAdRatio(): Promise<number> {
    try {
        const configRef = firestore().collection('config').doc('ads');
        const configDoc = await configRef.get();

        if (configDoc.exists) {
            // Get the ad_ratio field from the config's document
            const adRatio = configDoc.get('ad_ratio') as number;

            // Return the value of ad_ratio
            return adRatio;
        } else {
            // Config document does not exist, return 0 as default
            console.log('Config document does not exist in the collection');
            return 3;
        }
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }

    // In case of an error, return 0 as default
    return 3;
}

// Get user permissions
// Function to get array of user's permissions
export async function getUserPermissions(): Promise<string[]> {

    const user = auth().currentUser;

    if (!user) {
        return [];
    }

    const userId = user.uid;

    try {
        const userRef = firestore().collection('users').doc(userId);
        const userDoc = await userRef.get();

        if (userDoc.exists) {
            const permissions = userDoc.get('permissions') as string[];
            return permissions;
        } else {
            // create a new user document and return empty array
            await userRef.set({
                permissions: [],
            });
            return [];
        }
    } catch (error) {
        console.log(error);
        return [];
    }
}

// Function to add to the user's permission array
export async function addUserPermission(permission: Permission): Promise<void> {

    try {

        const user = auth().currentUser;

        if (!user) {
            return;
        }

        const userId = user.uid;

        const userRef = firestore().collection('users').doc(userId);
        const userDoc = await userRef.get();

        if (userDoc.exists) {
            const permissions = userDoc.get('permissions') as string[];
            permissions.push(permission);
            await userRef.update({ permissions });
        } else {
            console.log(`User with id ${userId} does not exist in the collection`);
        }
    } catch (error) {
        console.log(error);
    }
}

// Function to check for a specific permission
export async function hasPermission(permission: Permission): Promise<boolean> {
    try {
        const permissions = await getUserPermissions();
        return permissions.includes(permission);
    } catch (error) {
        console.log(error);
        return false;
    }
}

export enum Permission {
    AD_TRACKING = 'ad_tracking',
}

export async function setAdTrackingPermission(): Promise<boolean> {

    console.log('Setting ad tracking permission');

    try {
        const permissions = await getUserPermissions();
        const hasPermission = permissions.includes(Permission.AD_TRACKING);

        if (!hasPermission) {
            const { granted, canAskAgain } = await getTrackingPermissionsAsync()
            if (!granted || canAskAgain) {
                const { granted, canAskAgain, expires } = await requestTrackingPermissionsAsync();
                console.log({
                    canAskAgain,
                    granted
                })
                if (!granted) {
                    console.log('User denied ad tracking permission', {
                        expires
                    });

                } else {
                    await addUserPermission(Permission.AD_TRACKING)
                }

                if (!canAskAgain) {
                    console.log('User denied ad tracking permission and cannot ask again');
                } else {
                    requestTrackingPermissionsAsync();
                }


            }
        } else {
            console.log('User already has ad tracking permission');
        }
    } catch (error) {
        console.log(error);
        return;
    }
}

