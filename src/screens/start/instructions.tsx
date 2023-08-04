import { PulsingButton, Screen, SoftButton, Typography } from "@components"
import { useState } from "react";
import { View, TouchableOpacity, Dimensions } from "react-native"
import Popover from 'react-native-popover-view';
import MaterialIcons from '@expo/vector-icons/MaterialIcons';



export const Instructions = () => {


    const [showPopover, setShowPopover] = useState(false);



    return (
        <>
            <SoftButton
                onPress={() => setShowPopover(true)}
            >
                <MaterialIcons name="info-outline" size={19} color="white" />
            </SoftButton>
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
                    <Typography className='text-center mb-4'>
                        How to play:
                    </Typography>
                    <Typography className='text-center text-[16px]'>
                        Drag the highlighted dot to the matching color ring before the time runs out to score points.
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