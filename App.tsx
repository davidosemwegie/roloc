import { StatusBar } from 'expo-status-bar';
import { Button, Text, View } from 'react-native';

export default function App() {
  return (
    <View className="flex-1 items-center justify-center bg-red-500">
      <Text>Open up App.js to start working on your app wagwan</Text>
      <StatusBar style="auto" />
      <Button title='Click me' onPress={() => console.log("Hello world")} />
    </View>
  );
}

