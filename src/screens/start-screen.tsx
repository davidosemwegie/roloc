import React from 'react'
import { Button, StatusBar, View } from 'react-native'

export const StartScreen = () => {
    return (
        <View className="flex-1 items-center justify-center bg-white">
            <StatusBar />
            <Button title='Click me' onPress={() => console.log("Hello world")} />
        </View>
    )
}