import { PulsingButton, Screen, SoftButton, Typography } from "@components"
import { FC, useEffect, useState } from "react";
import { View, TouchableOpacity, Dimensions, Image } from "react-native"
import Popover from 'react-native-popover-view';
import MaterialIcons from '@expo/vector-icons/MaterialIcons';
import { useGameStateStore } from "@stores";
import Feather from "@expo/vector-icons/Feather";

export interface InstructionsProps {
    open?: boolean;
}

export const Instructions: FC<InstructionsProps> = ({ open = false }) => {

    const [showPopover, setShowPopover] = useState(open);
    const { startGame } = useGameStateStore();

    useEffect(() => {
        if (open) {
            setShowPopover(true);
        }
    }, [open])


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
                <View className="mb-6">
                    <Image
                        source={require('../../assets/howto.gif')}
                        style={{
                            aspectRatio: 3 / 4,
                            height: Dimensions.get('window').width * 0.9,
                            resizeMode: 'contain',
                        }}
                    />
                </View>
                <PulsingButton
                    onPress={startGame}
                    size={12}
                    color="#1d4ed8"
                >
                    Play
                </PulsingButton>
                <TouchableOpacity
                    onPress={() => setShowPopover(false)}
                    style={{ position: 'absolute', top: 16, right: 16 }}>
                    <Feather name="x" size={24} color='white' />
                </TouchableOpacity>
            </Popover>
        </>
    )
}