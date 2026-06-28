import { create } from "zustand";

const STORAGE_KEY_PRACTICE_REMINDERS = "notif.practiceReminders";
const STORAGE_KEY_PRODUCT_UPDATES = "notif.productUpdates";

interface NotificationPreferencesState {
    isPracticeRemindersEnabled: boolean;
    isProductUpdatesEnabled: boolean;
    setPracticeRemindersEnabled: (value: boolean) => void;
    setProductUpdatesEnabled: (value: boolean) => void;
}

function readBooleanFromStorage(key: string, defaultValue: boolean): boolean {
    if (typeof window === "undefined") return defaultValue;
    const stored = localStorage.getItem(key);
    if (stored === null) return defaultValue;
    return stored === "true";
}

export const useNotificationPreferencesStore = create<NotificationPreferencesState>((set) => ({
    isPracticeRemindersEnabled: readBooleanFromStorage(STORAGE_KEY_PRACTICE_REMINDERS, true),
    isProductUpdatesEnabled: readBooleanFromStorage(STORAGE_KEY_PRODUCT_UPDATES, false),

    setPracticeRemindersEnabled: (value) => {
        localStorage.setItem(STORAGE_KEY_PRACTICE_REMINDERS, String(value));
        set({ isPracticeRemindersEnabled: value });
    },

    setProductUpdatesEnabled: (value) => {
        localStorage.setItem(STORAGE_KEY_PRODUCT_UPDATES, String(value));
        set({ isProductUpdatesEnabled: value });
    },
}));
