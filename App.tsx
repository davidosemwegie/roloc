import { View } from 'react-native';
import { MainLayout } from '@layouts';
import auth from '@react-native-firebase/auth';
import { useEffect, useState } from 'react';
import crashlytics from '@react-native-firebase/crashlytics';
import { requestTrackingPermissionsAsync } from 'expo-tracking-transparency';
import mobileAds from 'react-native-google-mobile-ads';
import { mixpanel } from '@fb';

import * as Sentry from 'sentry-expo';

Sentry.init({
  dsn: 'https://3ad26c15d4f859f3e8fd859727599107@o4505616022241280.ingest.sentry.io/4505616032727040',
  enableInExpoDevelopment: true,
  debug: __DEV__, // If `true`, Sentry will try to print out useful debugging information if something goes wrong with sending the event. Set it to `false` in production
  environment: __DEV__ ? 'development' : 'production',
});


mobileAds()
  .initialize()

mixpanel.init()

export default function App() {

  useEffect(() => {
    (async () => {

      const { status } = await requestTrackingPermissionsAsync();
      if (status === 'granted') {
        console.log('Yay! I have user permission to track data');
      }
    })();

    const env = process.env.NODE_ENV;

    console.log('env', env);
    console.log(__DEV__ ? 'Running in dev mode' : 'Running in production mode')

  }, []);


  function SignInAnonymously() {
    auth()
      .signInAnonymously()
      .then(async (user) => {
        await Promise.all([
          crashlytics().setUserId(user.user.uid),
          crashlytics().log('User signed in anonymously'),
          mixpanel.identify(user.user.uid),
        ])
      })
      .catch(error => {
        if (error.code === 'auth/operation-not-allowed') {
          console.log('Enable anonymous in your firebase console.');
        }

        console.error(error);
      });
  }

  // Set an initializing state whilst Firebase connects
  const [initializing, setInitializing] = useState(true);
  const [user, setUser] = useState();

  // Handle user state changes
  function onAuthStateChanged(user) {
    setUser(user);
    if (initializing) setInitializing(false);
  }

  useEffect(() => {
    const subscriber = auth().onAuthStateChanged(onAuthStateChanged);
    return subscriber; // unsubscribe on unmount
  }, []);


  useEffect(() => {
    SignInAnonymously()
  }, []);


  return (
    <View className="flex-1 items-center justify-center ">
      <MainLayout />
    </View>
  );
}

