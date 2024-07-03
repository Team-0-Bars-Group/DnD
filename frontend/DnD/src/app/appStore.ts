import { configureStore } from "@reduxjs/toolkit";
import { rootReducer } from "./appReducer";
import { authApi } from "@/features/auth/api/api";
import { inventoryApi } from "@/features/inventory/api/api";
import { partyApi } from "@/features/party/api/api";
import { characterApi } from "@/features/character/api/api";

const store = configureStore({
    reducer: rootReducer,
    middleware: (getDefaultMiddleware) =>
        getDefaultMiddleware()
        .concat(authApi.middleware)
        .concat(inventoryApi.middleware)
        .concat(partyApi.middleware)
        .concat(characterApi.middleware),
});

export default store;

export type RootState = ReturnType<typeof store.getState>

export type AppDispatch = typeof store.dispatch;