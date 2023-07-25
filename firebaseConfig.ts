
import database from '@react-native-firebase/database';
import auth from '@react-native-firebase/auth';
import crashlytics from '@react-native-firebase/crashlytics'

export function updateUserScore(score: number) {
    const user = auth().currentUser;

    console.log({ user });

    try {
        database()
            .ref(`/users/${user.uid}`)
            .set({
                high_score: score,
            })
            .then(() => console.log('New Highscore added to database'));
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }
}