import { Permission, getShouldGetFaster, hasPermission, setAdTrackingPermission } from "@fb";
import React from "react";
import { PropsWithChildren, createContext, useEffect, useState } from "react";

export interface RemoteConfigProviderContext {
    shouldGetFaster: boolean
}

export const RemoteConfigProviderContext = createContext<RemoteConfigProviderContext>(undefined);

export const RemoteConfigProvider: React.FC<PropsWithChildren<{}>> = ({ children }) => {
    const [shouldGetFaster, setShouldGetFaster] = useState(false);

    const fetchShouldGetFaster = async () => {
        // Should get faster
        const _shouldGetFaster = await getShouldGetFaster()
        setShouldGetFaster(_shouldGetFaster);

    };

    useEffect(() => {
        fetchShouldGetFaster();

        (async () => {
            await setAdTrackingPermission();
        })();

        const intervalId = setInterval(fetchShouldGetFaster, 60 * 1000); // Fetch every 60 seconds

        return () => {
            clearInterval(intervalId);
        };
    }, []);

    const value = {
        shouldGetFaster,
    }

    return (
        <RemoteConfigProviderContext.Provider value={value}>
            {children}
        </RemoteConfigProviderContext.Provider>
    )
}

export const useRemoteConfig = () => {
    const context = React.useContext(RemoteConfigProviderContext)
    if (context === undefined) {
        throw new Error(
            'useRemoteConfigProvider must be used within a RemoteConfigProvider'
        )
    }
    return context
}