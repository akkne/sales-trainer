import { create } from "zustand";
import { SESSION_TERMINATED_KEY } from "@/shared/api/api-client";

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

/// The administrator of one organization — the РОП. Mirrors `RequireOrgAdmin` on the backend,
/// minus its platform-staff branch: this predicate answers "does this person belong to an
/// organization panel", and platform staff reach `/org/*` through impersonation instead
/// (docs/TENANCY/ADMIN_UI_DESIGN.md §1.2).
export const isOrganizationStaff = (orgRole: OrgRole | null | undefined): boolean =>
    orgRole === "TenancyAdmin" || orgRole === "TenancySuperAdmin";

/// The single privilege that separates an organization admin from an organization superadmin:
/// inviting and deactivating that organization's people. Mirrors `RequireOrgSuperAdmin`, which
/// is the gate that actually enforces it — this only decides whether the affordance is shown.
export const canManageOrganizationPeople = (orgRole: OrgRole | null | undefined): boolean =>
    orgRole === "TenancySuperAdmin";

interface AuthenticatedUser {
    id: string;
    email: string;
    displayName: string;
    isOnboardingCompleted: boolean;
    role: UserRole;
    orgId?: string | null;
    orgName?: string | null;
    orgRole?: OrgRole | null;
}

interface AuthStoreState {
    authenticatedUser: AuthenticatedUser | null;
    accessToken: string | null;
    setAuthenticatedUser: (user: AuthenticatedUser) => void;
    setAccessToken: (token: string) => void;
    /**
     * R2-5: `terminated` must be true only when the session is deliberately ending (logout, or
     * an explicit auth rejection like a 401 from `/auth/me`) — never for a transient failure
     * (network error, timeout, 500), which must leave silent token refresh available for the
     * next request. Defaults to false so a careless call cannot accidentally re-introduce R-1.
     */
    clearAuthSession: (options?: { terminated?: boolean }) => void;
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
        // R-1: a fresh access token means a new session legitimately began (login/register/
        // Google sign-in) — un-terminate so the next 401 is allowed to refresh normally again.
        localStorage.removeItem(SESSION_TERMINATED_KEY);
        set({ accessToken: token });
    },

    clearAuthSession: (options) => {
        localStorage.removeItem("accessToken");
        if (options?.terminated) {
            // R-1: mark the session as deliberately ended so a leftover refresh-cookie (e.g.
            // because the server-side POST /auth/logout revoke failed) cannot silently mint a
            // new access token on the next 401 — see `attemptTokenRefresh` in
            // shared/api/api-client.ts. R2-5: callers must pass this only for a genuine logout
            // or an explicit auth rejection, never for a transient failure.
            localStorage.setItem(SESSION_TERMINATED_KEY, "1");
        }
        set({ authenticatedUser: null, accessToken: null });
    },
}));
