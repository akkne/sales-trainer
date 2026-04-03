import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { apiClient } from "@/lib/api/apiClient";
import { useAuthStore, type UserRole } from "@/lib/store/authStore";
import { clientLogger } from "@/lib/clientLogger";

interface AuthTokenResponse {
    accessToken: string;
    userId: string;
    displayName: string;
    isOnboardingCompleted: boolean;
    role: UserRole;
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
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (credentials: {
            email: string;
            password: string;
            displayName: string;
        }) => apiClient.post<AuthTokenResponse>("/auth/register", credentials),
        onSuccess: (data, variables) => {
            clientLogger.info("Registration successful", {
                userId: data.userId,
                email: variables.email,
                role: data.role,
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

export function useLogin() {
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
