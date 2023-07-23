import React, { useEffect, useState, useRef } from "react";
import { View } from "react-native";

export interface BoundingBox {
    topLeft: { x: number; y: number };
    topRight: { x: number; y: number };
    bottomLeft: { x: number; y: number };
    bottomRight: { x: number; y: number };
    center: { x: number; y: number };
}

export function getBoundingBox(elementRef: React.RefObject<View>): Promise<BoundingBox | null> {
    if (!elementRef.current) return Promise.resolve(null);

    return new Promise<BoundingBox | null>((resolve) => {
        elementRef.current.measure((x, y, width, height, pageX, pageY) => {
            const topLeft = { x: pageX, y: pageY };
            const topRight = { x: pageX + width, y: pageY };
            const bottomLeft = { x: pageX, y: pageY + height };
            const bottomRight = { x: pageX + width, y: pageY + height };
            const center = { x: pageX + width / 2, y: pageY + height / 2 };
            const boundingBox: BoundingBox = {
                topLeft,
                topRight,
                bottomLeft,
                bottomRight,
                center,
            };
            resolve(boundingBox);
        });
    });
}

export const useGetBoundingBox = (ref: React.RefObject<View>) => {
    const [boundingBox, setBoundingBox] = useState<BoundingBox | null>({
        topLeft: { x: 0, y: 0 },
        topRight: { x: 0, y: 0 },
        bottomLeft: { x: 0, y: 0 },
        bottomRight: { x: 0, y: 0 },
        center: { x: 0, y: 0 },
    });

    useEffect(() => {
        const fetchBoundingBox = async () => {
            const boundingBox = await getBoundingBox(ref);
            setBoundingBox(boundingBox);
        };

        fetchBoundingBox();
    }, [ref]);

    return boundingBox;
}
