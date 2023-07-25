import { View } from 'react-native';
import { MainLayout } from '@layouts';
import auth from '@react-native-firebase/auth';
import { useEffect, useState } from 'react';



export default function App() {

  function SignInAnonymously() {
    auth()
      .signInAnonymously()
      .then(() => {
        console.log('User signed in anonymously');
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

