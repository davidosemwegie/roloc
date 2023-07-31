import { View } from 'react-native';
import { MainLayout } from '@layouts';
import auth from '@react-native-firebase/auth';
import { useEffect, useState } from 'react';
import crashlytics from '@react-native-firebase/crashlytics';
import { requestTrackingPermissionsAsync } from 'expo-tracking-transparency';
import mobileAds from 'react-native-google-mobile-ads';
import remoteConfig from '@react-native-firebase/remote-config';
import { mixpanel } from '@fb';


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

