import { PulsingButton, Screen, SoftButton, Typography } from "@components"
import { useState } from "react";
import { View, TouchableOpacity, Dimensions, Switch } from "react-native"
import Popover from 'react-native-popover-view';
import MaterialIcons from '@expo/vector-icons/MaterialIcons';
import { useGameStateStore } from "@stores";
import Feather from "@expo/vector-icons/Feather";



export const SettingsPopup = () => {


    const [showPopover, setShowPopover] = useState(false);


    const {
        isBackgroundMuted,
        toggleBackgroundMute,
        isGameOverSoundMuted,
        toggleGameOverSoundMute,
        isMatchSoundMuted,
        toggleMatchSoundMute,
    } = useGameStateStore()


    return (
        <>
            <SoftButton
                onPress={() => setShowPopover(true)}
            >
                ⚙️
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
                    width: Dimensions.get('window').width * 0.8,
                }}>
                <TouchableOpacity
                    onPress={() => setShowPopover(false)}
                    style={{ position: 'absolute', top: 16, right: 16 }}>
                    <Feather name="x" size={24} color='white' />
                </TouchableOpacity>
                <View className='flex flex-col items-center px-4 space-y-4'>
                    <Typography className='text-center mb-2'>
                        Settings
                    </Typography>
                    <View className="flex flex-row justify-between items-center w-full">
                        <Typography className='text-[16px] flex-1'>
                            Background music
                        </Typography>

                        <Switch value={!isBackgroundMuted} onValueChange={() => toggleBackgroundMute()} />
                    </View>
                    <View className="flex flex-row justify-between items-center w-full">
                        <Typography className='text-[16px] flex-1'>
                            Match sound
                        </Typography>

                        <Switch value={!isMatchSoundMuted} onValueChange={() => toggleMatchSoundMute()} />
                    </View>
                    <View className="flex flex-row justify-between items-center w-full">
                        <Typography className='text-[16px] flex-1'>
                            Gameover sound
                        </Typography>

                        <Switch value={!isGameOverSoundMuted} onValueChange={() => toggleGameOverSoundMute()} />
                    </View>
                </View>
            </Popover>
        </>
    )
}