import React, { useState, useEffect } from "react";
import { View, Button } from "react-native";
import Popover from 'react-native-popover-view';
import { PulsingButton } from "../pulsing-button";
import { Typography } from "../typography";
import { useAdContext } from "@layouts";
import { useExtraLifeStore } from "@stores";
import { TextInput } from "react-native-gesture-handler";
import { getUserEmail, setUserEmail, trackEvent } from "@fb";
import { A } from '@expo/html-elements';
import AsyncStorage from '@react-native-async-storage/async-storage';


export const GetExtraLivesModal = () => {
    const [showPopover, setShowPopover] = useState(false);
    const [isLoadingAd, setIsLoadingAd] = useState(false); // New state for ad loading
    const [email, setEmail] = useState(''); // New state for ad loading
    const [dbEmail, setDbEmail] = useState(''); // New state for ad loa0ding

    const [lastInstagramClickTime, setLastInstagramClickTime] = useState(0);

    useEffect(() => {
        async function loadLastInstagramClickTime() {
            // Load the last clicked time from local storage when the component mounts
            const lastClickTime = await AsyncStorage.getItem('instagram_link_clicked');
            if (lastClickTime) {
                setLastInstagramClickTime(parseInt(lastClickTime, 10));
            }
        }

        loadLastInstagramClickTime();
    }, []);


    const { extraLives, setExtraLives, addExtraLife } = useExtraLifeStore();

    const { rewardedInterstitialAd } = useAdContext();

    const [isLoadingEmail, setIsLoadingEmail] = useState(true);


    useEffect(() => {
        setExtraLives()
        getUserEmail().then((email) => {
            setDbEmail(email);
            setIsLoadingEmail(false); // Mark email loading as complete
        });
    }, [])

    useEffect(() => {

        const { isEarnedReward } = rewardedInterstitialAd;

        if (isEarnedReward) {
            addExtraLife();
        }

    }, [
        rewardedInterstitialAd.isEarnedReward,
    ])


    useEffect(() => {
        if (rewardedInterstitialAd.isLoaded) {
            setIsLoadingAd(false); // Set isLoadingAd to false when the ad is loaded
        }
    }, [rewardedInterstitialAd.isLoaded])

    const onWatchAdButtonClicked = () => {
        rewardedInterstitialAd.show();
    };

    const onGetExtraLivesButtonClicked = async () => {
        setShowPopover(true);
        setIsLoadingAd(true); // Set isLoadingAd to true before loading the ad
        try {
            await rewardedInterstitialAd.load()
        } catch (error) {
            console.error("Error loading ad:", error);
            setIsLoadingAd(false); // Set isLoadingAd to false in case of an error
        }
    };

    const handleInstagramLinkClick = async () => {
        trackEvent('social_link_clicked', {
            social: 'instagram'
        });
        // Check if the current time is greater than the last clicked time plus 12 hours
        const currentTime = Date.now();
        if (currentTime - lastInstagramClickTime >= 12 * 60 * 60 * 1000) {
            // User is allowed to click the link
            setLastInstagramClickTime(currentTime);

            // Store the new last clicked time in local storage
            await AsyncStorage.setItem('instagram_link_clicked', currentTime.toString());

            // Perform other actions for the Instagram link click here (e.g., addExtraLife())
            addExtraLife();

            alert("You got an extra life!");

        } else {
            // User cannot click the link yet, display a message or take other actions
            alert("Get extra lives by clicking the link once every 12 hours");
        }
    };

    const [lastTwitterClickTime, setLastTwitterClickTime] = useState(0);

    useEffect(() => {
        async function loadLastTwitterClickTime() {
            // Load the last clicked time from local storage when the component mounts
            const lastClickTime = await AsyncStorage.getItem('twitter_link_clicked');
            if (lastClickTime) {
                setLastTwitterClickTime(parseInt(lastClickTime, 10));
            }
        }

        loadLastTwitterClickTime();
    }, []);

    const handleTwitterLinkClick = async () => {
        trackEvent('social_link_clicked', {
            social: 'twitter'
        });        // Check if the current time is greater than the last clicked time plus 12 hours
        const currentTime = Date.now();
        if (currentTime - lastTwitterClickTime >= 12 * 60 * 60 * 1000) {
            // User is allowed to click the link
            setLastTwitterClickTime(currentTime);

            // Store the new last clicked time in local storage
            await AsyncStorage.setItem('twitter_link_clicked', currentTime.toString());

            // Perform other actions for the Twitter link click here (e.g., addExtraLife())
            addExtraLife();

            alert("You got an extra life!");
        } else {
            // User cannot click the link yet, display a message or take other actions
            alert("Get extra lives by clicking the Twitter link once every 12 hours");
        }
    };

    const isInstagramLinkClickable = Date.now() - lastInstagramClickTime >= 12 * 60 * 60 * 1000;
    const isTwitterLinkClickable = Date.now() - lastTwitterClickTime >= 12 * 60 * 60 * 1000;


    return (
        <>
            <PulsingButton
                onPress={onGetExtraLivesButtonClicked}
                size={8}
                color="#1d4ed8"
            >
                Get extra lives
            </PulsingButton>
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
                <View>
                    <Typography className="">
                        Get extra more lives
                    </Typography>
                </View>
                <View className='flex flex-row justify-between'>
                    <Typography className="text-xl">Extra Lives: </Typography>
                    <Typography className="text-xl "> {extraLives}</Typography>
                </View>
                {isLoadingAd ? (
                    <Typography className="text-center text-[16px] mt-4">
                        Loading...
                    </Typography>
                ) : rewardedInterstitialAd.isLoaded ? (
                    <View className="mt-6">
                        <Button
                            onPress={onWatchAdButtonClicked}
                            title="Watch ad to get an extra life"
                        />
                    </View>
                ) : (
                    <View className="mt-4">
                        <Typography className="text-center text-[16px]">
                            Come back later to get more free extra lives
                        </Typography>
                    </View>
                )}
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
                                <TextInput
                                    placeholder="Email"
                                    value={email} // Use the 'email' state as the value of the input
                                    onChangeText={(text) => setEmail(text)} // Update the 'email' state when the input changes
                                    className="bg-white w-full p-4"
                                />
                                <Button
                                    title="Join our newsletter to get an extra life"
                                    disabled={!email}
                                    onPress={() => {
                                        setUserEmail(email).then(() => {
                                            alert('Email saved! and you got an extra life!');
                                            setDbEmail(email);
                                            setEmail('');
                                        })
                                        trackEvent('email_saved')
                                    }}
                                />
                            </>
                        )
                    )}
                </View>

                {(isInstagramLinkClickable || isTwitterLinkClickable) && (
                    <View className="space-y-6 flex items-center">
                        <Typography className="text-[18px] text-center">
                            Hi I'm David, the creator of this game 👋🏽
                        </Typography>
                        {isInstagramLinkClickable && (
                            <A
                                href="https://instagram.com/osazi"
                                style={{
                                    color: '#1d4ed8',
                                    fontSize: 18,
                                }}
                                onPress={handleInstagramLinkClick} // Use the custom handler for the Instagram link
                            >Follow me on instagram</A>
                        )}

                        {isTwitterLinkClickable && (
                            <A href="https://twitter.com/@davidosemwegie"
                                style={{
                                    color: '#1d4ed8',
                                    fontSize: 18,
                                }}
                                onPress={handleTwitterLinkClick} // Use the custom handler for the Twitter link

                            >
                                Follow me on Twitter
                            </A>
                        )}

                    </View>
                )}

                <View className="mt-10">
                    <PulsingButton
                        onPress={() => setShowPopover(false)}
                        size={8}
                        color="#1d4ed8"
                    >
                        Close
                    </PulsingButton>
                </View>
            </Popover>
        </>
    );
};
