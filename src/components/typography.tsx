import { cn } from '@utils'
import React, { FC, PropsWithChildren } from 'react'
import { StyleProp, Text, TextStyle } from 'react-native'

export interface TypographyProps {
    className?: string,
    style?: StyleProp<TextStyle>;
}
export const Typography: FC<PropsWithChildren<TypographyProps>> = ({ children, className, style }) => {
    return (
        <Text style={style} className={cn('text-white font-semibold text-[18px]', className)}>
            {children}
        </Text>
    )
}