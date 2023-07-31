import React, { useState, useEffect } from "react";
import { View, TouchableOpacity } from "react-native";
import Popover from 'react-native-popover-view';
import { PulsingButton } from "../pulsing-button";
import { Typography } from "../typography";
import { useAdContext } from "@layouts";
import { useExtraLifeStore } from "@stores";
import { TextInput } from "react-native-gesture-handler";
import { getUserEmail, mixpanel, setUserEmail, trackEvent } from "@fb";
import { A } from '@expo/html-elements';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Feather from '@expo/vector-icons/Feather';
import AntDesign from '@expo/vector-icons/Feather';
import { SoftButton } from "../soft-button";


export const GetExtraLivesModal = () => {
    const [showPopover, setShowPopover] = useState(false);
    const [isLoadingAd, setIsLoadingAd] = useState(false);
    const [email, setEmail] = useState('');
    const [dbEmail, setDbEmail] = useState('');

    const [lastInstagramClickTime, setLastInstagramClickTime] = useState(0);
    const [lastTwitterClickTime, setLastTwitterClickTime] = useState(0);

    const { extraLives, setExtraLives, addExtraLife } = useExtraLifeStore();
    const { rewardedInterstitialAd, shouldShowAds } = useAdContext();
    const [isLoadingEmail, setIsLoadingEmail] = useState(true);

    useEffect(() => {
        async function loadLastClickTimes() {
            const instagramLastClickTime = await AsyncStorage.getItem('instagram_link_clicked');
            const twitterLastClickTime = await AsyncStorage.getItem('twitter_link_clicked');
            if (instagramLastClickTime) {
                setLastInstagramClickTime(parseInt(instagramLastClickTime, 10));
            }
            if (twitterLastClickTime) {
                setLastTwitterClickTime(parseInt(twitterLastClickTime, 10));
            }
        }

        loadLastClickTimes();
    }, []);

    useEffect(() => {
        setExtraLives();
        getUserEmail().then((email) => {
            setDbEmail(email);
            setIsLoadingEmail(false);
        });
    }, []);

    useEffect(() => {
        const { isEarnedReward } = rewardedInterstitialAd;

        if (isEarnedReward) {
            addExtraLife();
        }

    }, [rewardedInterstitialAd.isEarnedReward]);

    useEffect(() => {
        if (rewardedInterstitialAd.isLoaded) {
            setIsLoadingAd(false);
        }
    }, [rewardedInterstitialAd.isLoaded]);

    const onWatchAdButtonClicked = () => {
        if (shouldShowAds) {
            rewardedInterstitialAd.show();
        }
    };

    const onGetExtraLivesButtonClicked = async () => {
        setShowPopover(true);
        setIsLoadingAd(true);
        try {
            await rewardedInterstitialAd.load();
        } catch (error) {
            console.error("Error loading ad:", error);
            setIsLoadingAd(false);
        }
    };

    const handleSocialLinkClick = async (social: 'instagram' | 'twitter') => {
        trackEvent('social_link_clicked', {
            social
        });
        const currentTime = Date.now();
        const lastClickTime = social === 'instagram' ? lastInstagramClickTime : lastTwitterClickTime;
        const setLastClickTime = social === 'instagram' ? setLastInstagramClickTime : setLastTwitterClickTime;

        if (currentTime - lastClickTime >= 3600000) {
            setLastClickTime(currentTime);
            await AsyncStorage.setItem(`${social}_link_clicked`, currentTime.toString());
            addExtraLife();
        } else {
            alert(`Come back later to view my ${social} page and get more free extra lives!`);
        }
    };

    const isSocialLinkClickable = (lastClickTime: number) => {
        return Date.now() - lastClickTime >= 1 * 60 * 60 * 1000;
    };

    const getRemainingTime = (lastClickTime: number) => {
        const currentTime = Date.now();
        const timeDifference = currentTime - lastClickTime;
        const remainingTime = 3600000 - timeDifference;
        return remainingTime;
    };

    const formatTime = (timeInMillis: number) => {
        const minutes = Math.floor(timeInMillis / (1000 * 60));
        return minutes === 0 ? 'less than a minute' : `${minutes} minutes`;
    };


    return (
        <>
            <PulsingButton
                onPress={onGetExtraLivesButtonClicked}
                size={16}
                color="#1d4ed8"
            >
                Get extra lives ❣️
            </PulsingButton>
            <Typography className="text-[18px] text-center mt-4">
                You have {extraLives} extra {extraLives === 1 ? 'life' : 'lives'}
            </Typography>
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
                }}
            >
                <TouchableOpacity
                    onPress={() => setShowPopover(false)}
                    style={{ position: 'absolute', top: 16, right: 16 }}>
                    <Feather name="x" size={24} color='white' />
                </TouchableOpacity>
                <View>
                    <Typography className="">
                        Get extra more lives
                    </Typography>
                </View>
                <View className='flex flex-row justify-between'>
                    <Typography className="text-xl">Extra Lives: </Typography>
                    <Typography className="text-xl "> {extraLives}</Typography>
                </View>
                {shouldShowAds && (

                    isLoadingAd ? (
                        <Typography className="text-center text-[16px] mt-4" >
                            Loading...
                        </Typography>
                    ) : rewardedInterstitialAd.isLoaded ? (
                        <View className="mt-6">
                            <PulsingButton
                                onPress={onWatchAdButtonClicked}
                                color="#1d4ed8"
                            >
                                <Typography style={{
                                    fontSize: 16,
                                }}>Watch ad to get an extra life
                                </Typography>
                            </PulsingButton>
                        </View>
                    ) : (
                        <View className="mt-4">
                            <Typography className="text-center text-[16px]">
                                Come back later to get more free extra lives
                            </Typography>
                        </View>
                    )
                )
                }


                <View className="mb-10 mt-10 w-full">
                    {/* Show the user's email if it's available */}
                    {isLoadingEmail ? (
                        <Typography className="text-center text-[16px] mt-4">Loading Email...</Typography>
                    ) : (
                        dbEmail ? (
                            <>
                                <View>
                                    <Typography className="text-center text-[18px] mt-4">Email: {dbEmail}</Typography>
                                </View>
                            </>
                        ) : (
                            <>
                                <Typography className="text-center mb-4 text-[18px]">
                                    Enter your email to get an extra life
                                </Typography>
                                <TextInput
                                    placeholder="Email"
                                    value={email} // Use the 'email' state as the value of the input
                                    onChangeText={(text) => setEmail(text)} // Update the 'email' state when the input changes
                                    className="bg-white w-full p-4 mb-6"
                                />
                                <SoftButton
                                    disabled={!email}
                                    onPress={() => {
                                        setUserEmail(email)
                                            .then(() => {
                                                addExtraLife();
                                                alert('Email saved! and you got an extra life!');
                                                setDbEmail(email);
                                                setEmail('');
                                                mixpanel.getPeople().set({
                                                    $email: email,
                                                })
                                            })
                                        trackEvent('email_saved')
                                    }}
                                    style={{
                                        opacity: !email ? 0.5 : 1,
                                    }}
                                >
                                    Submit
                                </SoftButton>
                            </>
                        )
                    )}
                </View>

                <View className="space-y-6 flex items-center">
                    <View className="space-y-2">

                        <Typography className="text-[18px] text-center">
                            Hi I'm David, the creator of this game 👋🏽
                        </Typography>
                        <Typography className="text-[18px] text-center">
                            View my social media pages to get more free extra lives!
                        </Typography>
                    </View>
                    <View className="flex space-y-4">
                        {isSocialLinkClickable(lastInstagramClickTime) ? (
                            <SoftButton >
                                <A
                                    href="https://instagram.com/osazi"
                                    onPress={() => handleSocialLinkClick('instagram')}
                                >
                                    <AntDesign name="instagram" size={24} color="white" />
                                </A>
                            </SoftButton>
                        ) : (
                            <Typography className="text-sm text-center">
                                Come back in {' '}
                                <Typography className="text-green-700 text-[18px]">
                                    {formatTime(getRemainingTime(lastInstagramClickTime))}
                                </Typography> {' '}
                                to view my Instagram page and get more free extra lives!
                            </Typography>
                        )}

                        {isSocialLinkClickable(lastTwitterClickTime) ? (
                            <SoftButton >
                                <A
                                    href="https://twitter.com/@davidosemwegie"
                                    onPress={() => handleSocialLinkClick('twitter')}
                                >
                                    <AntDesign name="twitter" size={24} color="white" />
                                </A>
                            </SoftButton>
                        ) : (
                            <Typography className="text-sm text-center" >
                                Come back in {' '}
                                <Typography className="text-green-700 text-[18px]">
                                    {formatTime(getRemainingTime(lastTwitterClickTime))}
                                </Typography>  {' '}
                                to view my Twitter page and get more free extra lives!
                            </Typography>
                        )}
                    </View>
                </View>

            </Popover>
        </>
    );
};
