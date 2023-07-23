import React, { PropsWithChildren, createContext, useContext, useState } from 'react'
import { Animated } from 'react-native'

export interface DraggableItem {
    id: string;
    pan: Animated.ValueXY;
    position?: { x: number; y: number }; // represents the dot's position on the screen
}

export interface DragContextType {
    draggableItem: DraggableItem | null;
    setDraggableItem: (item: DraggableItem | null) => void;
}

export const DragContext = createContext<DragContextType>({
    draggableItem: null,
    setDraggableItem: () => { },
});

export const DragProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {
    const [draggableItem, setDraggableItem] = useState<DraggableItem | null>(null);

    const value = {
        draggableItem,
        setDraggableItem,
    }

    return (
        <DragContext.Provider value={value}>
            {children}
        </DragContext.Provider>
    );
}

export const useDragContext = () => useContext(DragContext);
