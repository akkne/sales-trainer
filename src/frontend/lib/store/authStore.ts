import { create } from "zustand";

interface AuthenticatedUser {
    id: string;
    email: string;
    displayName: string;
    isOnboardingCompleted: boolean;
}

interface AuthStoreState {
    authenticatedUser: AuthenticatedUser | null;
    accessToken: string | null;
    setAuthenticatedUser: (user: AuthenticatedUser) => void;
    setAccessToken: (token: string) => void;
    clearAuthSession: () => void;
}

export const useAuthStore = create<AuthStoreState>((set) => ({
    authenticatedUser: null,
    accessToken:
        typeof window !== "undefined"
            ? localStorage.getItem("accessToken")
            : null,

    setAuthenticatedUser: (user) => set({ authenticatedUser: user }),

    setAccessToken: (token) => {
        localStorage.setItem("accessToken", token);
        set({ accessToken: token });
    },

    clearAuthSession: () => {
        localStorage.removeItem("accessToken");
        set({ authenticatedUser: null, accessToken: null });
    },
}));
