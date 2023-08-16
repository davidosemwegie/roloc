import { View } from 'react-native';
import { MainLayout } from '@layouts';
import auth from '@react-native-firebase/auth';
import { useEffect, useState } from 'react';
import crashlytics from '@react-native-firebase/crashlytics';
import mobileAds from 'react-native-google-mobile-ads';
import { mixpanel } from '@fb';
import { useNetInfo } from "@react-native-community/netinfo";
import { Typography } from '@components';
import * as Network from 'expo-network';




mobileAds()
  .initialize()

mixpanel.init()

export default function App() {

  const [isConnected, setIsConnected] = useState(false);


  // Function to check the network state
  const checkConnection = async () => {
    const networkState = await Network.getNetworkStateAsync();
    setIsConnected(networkState.isConnected);
  };

  useEffect(() => {
    // Check the connection initially
    checkConnection();

    // Set up the interval to check the connection every minute
    const intervalId = setInterval(() => {
      checkConnection();
    }, 30 * 1000); // 60 * 1000 milliseconds = 1 minute

    // Return a cleanup function to clear the interval when the component is unmounted
    return () => {
      clearInterval(intervalId);
    };
  }, []);

  useEffect(() => {

    (async () => {
      const networkState = await Network.getNetworkStateAsync();
      setIsConnected(networkState.isConnected);
    })();


    const env = process.env.NODE_ENV;

    console.log('env', env);
    console.log(__DEV__ ? 'Running in dev mode' : 'Running in production mode');

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

  if (!isConnected) {
    return (
      <View className="flex-1 items-center justify-center bg-black px-6 space-y-4">
        <View className="flex flex-row mx-auto" >
          <Typography className="text-6xl mx-1 font-bold text-blue-500">R</Typography>
          <Typography className="text-6xl mx-1 font-bold text-yellow-500">O</Typography>
          <Typography className="text-6xl mx-1 font-bold text-red-500">L</Typography>
          <Typography className="text-6xl mx-1 font-bold text-green-500">O</Typography>
          <Typography className="text-6xl mx-1 font-bold text-white-500">C</Typography>
        </View>
        <Typography className='text-center'>
          Please check your internet connection and reload the app 🙏🏾
        </Typography>
      </View>
    )
  }

  return (
    <View className="flex-1 items-center justify-center ">
      <MainLayout />
    </View>
  );
}

