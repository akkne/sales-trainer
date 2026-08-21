import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiClient, SessionExpiredError } from "@/shared/api/api-client";
import { useAuthStore, type OrgRole, type UserRole } from "@/shared/stores/auth-store";
import { clientLogger } from "@/shared/utils/client-logger";
import { toast } from "@/features/notifications/store/toast-store";

const PENDING_VERIFICATION_EMAIL_KEY = "pendingVerificationEmail";

interface AuthTokenResponse {
    accessToken: string;
    userId: string;
    displayName: string;
    isOnboardingCompleted: boolean;
    role: UserRole;
    orgId?: string | null;
    orgRole?: OrgRole | null;
}

export function readPendingVerificationEmail(): string {
    if (typeof window === "undefined") return "";
    return window.sessionStorage.getItem(PENDING_VERIFICATION_EMAIL_KEY) ?? "";
}

function storePendingVerificationEmail(email: string) {
    if (typeof window !== "undefined") {
        window.sessionStorage.setItem(PENDING_VERIFICATION_EMAIL_KEY, email);
    }
}

function useHandleSuccessfulAuth() {
    const router = useRouter();
    const queryClient = useQueryClient();
    const { setAccessToken, setAuthenticatedUser } = useAuthStore();

    return (authResponse: AuthTokenResponse) => {
        queryClient.clear();
        setAccessToken(authResponse.accessToken);
        setAuthenticatedUser({
            id: authResponse.userId,
            email: "",
            displayName: authResponse.displayName,
            isOnboardingCompleted: authResponse.isOnboardingCompleted,
            role: authResponse.role ?? "User",
            orgId: authResponse.orgId ?? null,
            orgRole: authResponse.orgRole ?? null,
        });

        if (authResponse.isOnboardingCompleted) {
            router.push("/tree");
        } else {
            router.push("/onboarding");
        }
    };
}

export function useInitAuth() {
    const { accessToken, authenticatedUser, setAuthenticatedUser, clearAuthSession } =
        useAuthStore();

    useEffect(() => {
        if (!accessToken || authenticatedUser) return;

        apiClient
            .get<{
                id: string;
                email: string;
                displayName: string;
                role: UserRole;
                orgId?: string | null;
                orgName?: string | null;
                orgRole?: OrgRole | null;
                isOnboardingCompleted: boolean;
            }>("/auth/me")
            .then((user) => setAuthenticatedUser(user))
            .catch(() => clearAuthSession());
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [accessToken]);
}

/**
 * Sign-up answers one of two ways depending on the server's `EmailVerification:Enabled`: a session
 * (200) when the address needs no proving, or just the address (202) when a code has been mailed.
 * Told apart by the presence of `accessToken` rather than by status code, which the api-client does
 * not surface.
 */
type RegistrationResponse =
    | AuthTokenResponse
    | { email: string; requiresEmailVerification: true };

/**
 * Public registration, reopened in 40.37 after 40.7 had deleted it.
 *
 * What comes back is an account with no membership, so `handleSuccessfulAuth` routes it exactly
 * like any other sign-in — onboarding first, then `/tree`, where the gate in `app/(main)` shows
 * the "waiting for an invitation" screen instead of the lesson tree. Registration deliberately
 * knows nothing about that: the user's organization, or lack of one, is the server's answer, not
 * a branch this hook takes.
 */
export function useRegister() {
    const router = useRouter();
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (credentials: { email: string; password: string; displayName: string }) =>
            apiClient.post<RegistrationResponse>("/auth/register", credentials),
        onSuccess: (data, variables) => {
            if (!("accessToken" in data)) {
                clientLogger.info("Registration awaiting email verification", {
                    email: variables.email,
                });
                storePendingVerificationEmail(data.email);
                router.push("/verify-email");
                return;
            }

            clientLogger.info("Registration successful", {
                userId: data.userId,
                email: variables.email,
            });
            handleSuccessfulAuth(data);
        },
        onError: (error, variables) => {
            clientLogger.warn("Registration failed", {
                email: variables.email,
                error: (error as Error).message,
            });
        },
    });
}

// An invite remains the way an account is attached to an organization, and the invite token
// itself already proves control of the email address — so accepting one still skips the
// verification code, whether it lands on a brand-new account or on one made at /register.
export function useAcceptInvite(token: string) {
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (credentials: { displayName?: string; password?: string }) =>
            apiClient.post<AuthTokenResponse>(
                `/auth/invites/${encodeURIComponent(token)}/accept`,
                credentials,
            ),
        onSuccess: (data) => {
            clientLogger.info("Invite accepted", { userId: data.userId });
            handleSuccessfulAuth(data);
        },
        onError: (error) => {
            clientLogger.warn("Invite acceptance failed", {
                error: (error as Error).message,
            });
        },
    });
}

export function useVerifyEmail() {
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (credentials: { email: string; code: string }) =>
            apiClient.post<AuthTokenResponse>("/auth/verify-email", credentials),
        onSuccess: (data, variables) => {
            clientLogger.info("Email verification successful", {
                userId: data.userId,
                email: variables.email,
            });
            handleSuccessfulAuth(data);
        },
        onError: (error, variables) => {
            clientLogger.warn("Email verification failed", {
                email: variables.email,
                error: (error as Error).message,
            });
        },
    });
}

export function useResendVerificationCode() {
    return useMutation({
        mutationFn: (email: string) =>
            apiClient.post<void>("/auth/resend-code", { email }),
        onError: (error) => {
            clientLogger.warn("Resend verification code failed", {
                error: (error as Error).message,
            });
        },
    });
}

/**
 * Phase 40.8, step 1 of the three-step login flow: the server answers which credential to ask
 * for, because the login method is per-organization configuration (docs/TENANCY/TENANCY.md §4.5).
 *
 * The response deliberately carries nothing but the method — no organization, no "this address
 * exists" flag — so the screen cannot be used to probe which addresses belong to a customer.
 */
export type LoginMethod = "password" | "oidc" | "saml";

interface LoginStartResponse {
    method: LoginMethod;
}

export function useLoginStart() {
    return useMutation({
        mutationFn: (email: string) =>
            apiClient.post<LoginStartResponse>("/auth/login/start", { email }),
        onError: (error) => {
            clientLogger.warn("Login method lookup failed", {
                error: (error as Error).message,
            });
        },
    });
}

export function useLogin() {
    const router = useRouter();
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (credentials: { email: string; password: string }) =>
            apiClient.post<AuthTokenResponse>("/auth/login", credentials),
        onSuccess: (data, variables) => {
            clientLogger.info("Login successful", {
                userId: data.userId,
                email: variables.email,
                role: data.role,
            });
            handleSuccessfulAuth(data);
        },
        onError: (error, variables) => {
            if (
                error instanceof ApiError &&
                error.payload.requiresEmailVerification === true
            ) {
                clientLogger.info("Login requires email verification", {
                    email: variables.email,
                });
                storePendingVerificationEmail(variables.email);
                router.push("/verify-email");
                return;
            }
            clientLogger.warn("Login failed", {
                email: variables.email,
                error: (error as Error).message,
            });
        },
    });
}

export function useGoogleLogin() {
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (idToken: string) =>
            apiClient.post<AuthTokenResponse>("/auth/google", { idToken }),
        onSuccess: (data) => {
            clientLogger.info("Google login successful", {
                userId: data.userId,
                role: data.role,
            });
            handleSuccessfulAuth(data);
        },
        onError: (error) => {
            clientLogger.warn("Google login failed", { error: (error as Error).message });
        },
    });
}

/**
 * A failed server-side revoke must never leave a signed-in browser: the local session is cleared
 * and the user is sent to `/login` in `onSettled`, regardless of whether the request succeeded.
 * `onError` only adds the notice that the server-side session may still be alive.
 */
export function useLogout() {
    const router = useRouter();
    const queryClient = useQueryClient();
    const { clearAuthSession } = useAuthStore();

    return useMutation({
        mutationFn: () => apiClient.post<void>("/auth/logout", {}),
        onSuccess: () => {
            clientLogger.info("User logged out");
        },
        onError: (error) => {
            // R-2: `SessionExpiredError` means the session was already expired before logout even
            // ran — `fetchWithAuthToken` already cleared the token and started a hard navigation
            // to /login itself. That's a routine expiry, not a possibly-stolen-session event, so
            // it gets a quiet log line instead of the "change your password" warning below.
            if (error instanceof SessionExpiredError) {
                clientLogger.info("Logout ran on an already-expired session");
                return;
            }
            clientLogger.warn("Logout request failed; clearing local session anyway", {
                error: (error as Error).message,
            });
            toast.error(
                `Не удалось завершить сессию на сервере: ${(error as Error).message}. ` +
                    "Вы вышли на этом устройстве, но советуем сменить пароль, если устройство не ваше."
            );
        },
        onSettled: (_data, error) => {
            clearAuthSession();
            // R-3: match login's `queryClient.clear()` — otherwise the next person to sign in on
            // this browser (or a token silently restored, see R-1) can briefly render this user's
            // cached data.
            queryClient.clear();
            // R-2: a SessionExpiredError already triggered `window.location.href = "/login"` (a
            // hard navigation) inside the failed request itself — an extra `router.push` here
            // would just race a second, SPA-level navigation on top of it.
            if (error instanceof SessionExpiredError) return;
            router.push("/login");
        },
    });
}
