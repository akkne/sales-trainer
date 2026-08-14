import { create } from "zustand";

// Phase 40.6: the global "Admin" role no longer exists on the backend — a РОП is the
// admin of one organization (org_role "OrgAdmin" on the JWT/`/auth/me`), never of the
// platform. "SuperAdmin" is the only remaining platform-wide role.
export type UserRole = "User" | "SuperAdmin";
export type OrgRole = "Manager" | "OrgAdmin";

interface AuthenticatedUser {
    id: string;
    email: string;
    displayName: string;
    isOnboardingCompleted: boolean;
    role: UserRole;
    orgId?: string | null;
    orgRole?: OrgRole | null;
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
