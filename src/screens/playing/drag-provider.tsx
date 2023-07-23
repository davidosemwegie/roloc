import { RingColors } from '@types';
import React, { PropsWithChildren, createContext, useContext, useState } from 'react'
import { Animated } from 'react-native'

export interface DraggableItem {
    color: RingColors;
    id: string;
    pan: Animated.ValueXY;
    position?: { x: number; y: number }; // represents the dot's position on the screen
}

export interface DragContextType {
    draggableItem: DraggableItem | null;
    setDraggableItem: (item: DraggableItem | null) => void;
    dragging: boolean;
    setDragging?: (dragging: boolean) => void;
}

export const DragContext = createContext<DragContextType>({
    draggableItem: null,
    setDraggableItem: () => { },
    dragging: false,
    setDragging: () => { },
});

export const DragProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {
    const [draggableItem, setDraggableItem] = useState<DraggableItem | null>(null);
    const [dragging, setDragging] = useState(false);

    const value = {
        draggableItem,
        setDraggableItem,
        dragging,
        setDragging,
    }

    return (
        <DragContext.Provider value={value}>
            {children}
        </DragContext.Provider>
    );
}

export const useDragContext = () => useContext(DragContext);
