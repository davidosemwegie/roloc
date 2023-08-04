import crashlytics from '@react-native-firebase/crashlytics'
import * as Sentry from 'sentry-expo';

export const captureError = (error: Error) => {
    crashlytics().recordError(error);
    Sentry.Native.captureException(error);
    console.log(error);
}