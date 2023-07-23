import { create } from "zustand";

export interface DragStore {
    ringPositions: { [key: string]: { x: number; y: number } }
    setRingPosition: (id: string, x: number, y: number) => void
}

export const useDragStore = create<DragStore>((set, get) => ({
    ringPositions: {},
    setRingPosition: (id: string, x: number, y: number) => set((state) => ({
        ringPositions: { ...state.ringPositions, [id]: { x, y } }
    }))
}))