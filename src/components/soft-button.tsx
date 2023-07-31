import { FC } from "react";
import { TouchableOpacity, TouchableOpacityProps } from "react-native";
import { Typography } from "./typography";

export interface SoftButtonProps {
    onPress: () => void;
    style: TouchableOpacityProps;
}

export const SoftButton: FC<TouchableOpacityProps> = ({ children, onPress, style, ...rest }) => {
    return (
        <TouchableOpacity
            onPress={onPress}
            className="bg-gray-800 w-auto p-4 rounded-lg flex flex-row space-x-3 items-center justify-center m-auto"
            style={{
                opacity: 0.8,
            }}
            {...rest}
        >
            <Typography
                style={{
                    fontSize: 16,
                }}
            >
                {children}
            </Typography>
        </TouchableOpacity>
    )
}