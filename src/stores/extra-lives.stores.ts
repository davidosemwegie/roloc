import { create } from "zustand";
import { addExtraLife, getExtraLives, trackEvent, useExtraLife } from "@fb";

interface ExtraLifeStore {
    extraLives: number;
    setExtraLives: () => void;
    addExtraLife: () => void;
    useExtraLife: () => void;
}

export const useExtraLifeStore = create<ExtraLifeStore>((set, get) => ({
    extraLives: 0,
    setExtraLives: async () => {
        const lives = await getExtraLives(); // Assume this function gets the extra lives from Firestore
        set({ extraLives: lives });
    },
    addExtraLife: async () => {
        trackEvent("extra_life_added")
        await addExtraLife();
        const lives = await getExtraLives(); // Refresh the state with the current value from Firestore
        set({ extraLives: lives });
    },
    useExtraLife: async () => {
        trackEvent("extra_life_used")
        const lives = await getExtraLives(); // Assume this function gets the extra lives from Firestore
        if (lives > 0) {
            await useExtraLife();
            const lives = await getExtraLives(); // Refresh the state with the current value from Firestore
            set({ extraLives: lives });
        }
    }
}));
