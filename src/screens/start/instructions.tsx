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
                className="bg-gray-900 w-auto p-4 rounded-lg flex items-center justify-center m-auto"
                style={{
                    opacity: 0.8,
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
                    padding: 40,
                    borderRadius: 20,
                }}>
                <View className='flex flex-col items-center mb-10'>
                    <Typography className='text-center mb-2'>
                        How to play:
                    </Typography>
                    <Typography className='text-center text-[16px]'>
                        Drag the highlighted dot to the matching color ring.
                    </Typography>
                    <Typography className='text-center text-[16px]'>
                        There are some twists though along the way... so be careful! 😊
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