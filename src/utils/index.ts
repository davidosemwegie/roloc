export * from './local-storage'
export * from './cn'

export function getRandomEnumValue<T>(enumeration: T): T[keyof T] {
    const values = Object.values(enumeration as any) as unknown as T[keyof T][];
    const randomIndex = Math.floor(Math.random() * values.length);
    return values[randomIndex];
}