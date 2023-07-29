import { PulsingButton, Screen, Typography } from "@components"
import { useState } from "react";
import { View, TouchableOpacity, Dimensions } from "react-native"
import Popover from 'react-native-popover-view';


export const Instructions = () => {


    const [showPopover, setShowPopover] = useState(false);


    // const windowWidth = Dimensions.get('window').width;
    // const windowHeight = Dimensions.get('window').height;

    return (
        <>
            <TouchableOpacity
                onPress={() => setShowPopover(true)}
                className="bg-gray-900 p-3 rounded-lg flex items-center justify-center"
                style={{
                    opacity: 0.5,
                }}
            >
                <Typography className="text-md">Instructions</Typography>
            </TouchableOpacity>
            <Popover
                isVisible={showPopover}
                onRequestClose={() => setShowPopover(false)}
                popoverStyle={{
                    backgroundColor: '#171717',
                    // width: windowWidth * 0.9,
                    // height: windowHeight * 0.9,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 30,
                    padding: 40,
                    borderRadius: 20,
                }}>
                <View className='flex flex-col items-center '>
                    <Typography className='text-center mb-2'>
                        How to play:
                    </Typography>
                    <Typography className='text-center text-[16px]'>
                        Match the color of the ring with the color of the dot.
                    </Typography>
                    <Typography className='text-center text-[16px]'>
                        There are some twists though... so be careful! 😊
                    </Typography>
                </View>
                <PulsingButton
                    onPress={() => setShowPopover(false)}
                    size={12}
                    color="#1d4ed8"
                >
                    Got it
                </PulsingButton>
            </Popover>
        </>
    )
}