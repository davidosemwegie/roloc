
import firestore from '@react-native-firebase/firestore';
import auth from '@react-native-firebase/auth';
import crashlytics from '@react-native-firebase/crashlytics'

export function updateUserScore(score: number) {
    const user = auth().currentUser;

    console.log({ user });

    try {
        firestore()
            .collection('users')
            .doc(user?.uid)
            .update({
                highscore: score,
            })
            .then(() => console.log('New Highscore added to database'));
    } catch (error) {
        crashlytics().recordError(error);
        console.log(error);
    }
}