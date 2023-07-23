import AsyncStorage from '@react-native-async-storage/async-storage';

class LocalStorage {
    async setStorageValue(key: string, value: any): Promise<void> {
        try {
            const data = typeof value === 'object' ? JSON.stringify(value) : value

            await AsyncStorage.setItem(key, data);
        } catch (e) {
            // Handle saving error here
        }
    }

    async getStorageValue<T = string>(key: string): Promise<T> {
        try {
            const data = await AsyncStorage.getItem(key);
            return data ? JSON.parse(data) : null
        } catch (e) {
            // Handle reading error here
        }
    }

    async removeStorageValue(key: string): Promise<void> {
        try {
            await AsyncStorage.removeItem(key);
        }
        catch (e) {
            // Handle remove error here
        }
    }
}

export const { getStorageValue, removeStorageValue, setStorageValue } = new LocalStorage();