import { create } from "zustand";

// Two independent axes, per the owner's 2026-08-16 role split (docs/DECISIONS.md).
//
// `UserRole` is the platform role from the JWT `role` claim: Sellevate's own staff roles,
// deliberately not bounded by tenancy. `OrgRole` is the organization role from `org_role`
// — a РОП is the admin of one organization, never of the platform. A user can hold one,
// the other, or both; platform staff usually hold no membership at all and therefore no
// `orgRole`.
//
// At either level the only difference between the admin and the superadmin is that only
// the superadmin may add or remove users.
export type UserRole = "User" | "Admin" | "SuperAdmin";
export type OrgRole = "Manager" | "TenancyAdmin" | "TenancySuperAdmin";

/// Sellevate staff. Everything in the platform admin panel is open to these two.
export const isPlatformStaff = (role: UserRole | null | undefined): boolean =>
    role === "Admin" || role === "SuperAdmin";

/// The single privilege that separates a platform admin from a platform superadmin:
/// creating, inviting, deactivating or re-roling a user. Mirrors `RequireSuperAdmin`
/// on the backend, which is the gate that actually enforces it — this only decides
/// whether the affordance is worth showing.
export const canManagePlatformUsers = (role: UserRole | null | undefined): boolean =>
    role === "SuperAdmin";

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
