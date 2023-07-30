import { PulsingButton, Screen, Typography } from "@components"
import { useState } from "react";
import { View, TouchableOpacity, Dimensions } from "react-native"
import Popover from 'react-native-popover-view';
import MaterialIcons from '@expo/vector-icons/MaterialIcons';



export const Instructions = () => {


    const [showPopover, setShowPopover] = useState(false);



    return (
        <>
            <TouchableOpacity
                onPress={() => setShowPopover(true)}
                className="bg-gray-900 w-auto p-4 rounded-lg flex flex-row space-x-3 items-center justify-center m-auto"
                style={{
                    opacity: 0.8,
                }}
            >
                <Typography style={{
                    fontSize: 16,
                }}>How to play
                </Typography>
                <MaterialIcons name="info-outline" size={18} color="white" />
            </TouchableOpacity>
            <Popover
                isVisible={showPopover}
                onRequestClose={() => setShowPopover(false)}
                popoverStyle={{
                    backgroundColor: '#171717',
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