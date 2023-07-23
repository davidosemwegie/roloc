import { View } from 'react-native';
import { MainLayout } from '@layouts';
import crashlytics from '@react-native-firebase/crashlytics';
import { useEffect } from 'react';



export default function App() {

  return (
    <View className="flex-1 items-center justify-center ">
      <MainLayout />
    </View>
  );
}

