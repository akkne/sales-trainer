import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { ApiError, apiClient } from "@/shared/api/api-client";
import { useAuthStore, type UserRole } from "@/shared/stores/auth-store";
import { clientLogger } from "@/shared/utils/client-logger";

const PENDING_VERIFICATION_EMAIL_KEY = "pendingVerificationEmail";

interface AuthTokenResponse {
    accessToken: string;
    userId: string;
    displayName: string;
    isOnboardingCompleted: boolean;
    role: UserRole;
}

interface RegistrationResult {
    email: string;
    requiresEmailVerification: boolean;
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
                isOnboardingCompleted: boolean;
            }>("/auth/me")
            .then((user) => setAuthenticatedUser(user))
            .catch(() => clearAuthSession());
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [accessToken]);
}

export function useRegister() {
    const router = useRouter();

    return useMutation({
        mutationFn: (credentials: {
            email: string;
            password: string;
            displayName: string;
        }) => apiClient.post<RegistrationResult>("/auth/register", credentials),
        onSuccess: (data, variables) => {
            clientLogger.info("Registration pending email verification", {
                email: variables.email,
            });
            storePendingVerificationEmail(data.email);
            router.push("/verify-email");
        },
        onError: (error, variables) => {
            clientLogger.warn("Registration failed", {
                email: variables.email,
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

export function useLogout() {
    const router = useRouter();
    const { clearAuthSession } = useAuthStore();

    return useMutation({
        mutationFn: () => apiClient.post<void>("/auth/logout", {}),
        onSuccess: () => {
            clientLogger.info("User logged out");
            clearAuthSession();
            router.push("/login");
        },
    });
}
