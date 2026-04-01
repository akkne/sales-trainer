import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { apiClient } from "@/lib/api/apiClient";
import { useAuthStore, type UserRole } from "@/lib/store/authStore";

interface AuthTokenResponse {
    accessToken: string;
    userId: string;
    displayName: string;
    isOnboardingCompleted: boolean;
    role: UserRole;
}

function useHandleSuccessfulAuth() {
    const router = useRouter();
    const { setAccessToken, setAuthenticatedUser } = useAuthStore();

    return (authResponse: AuthTokenResponse) => {
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

export function useRegister() {
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (credentials: {
            email: string;
            password: string;
            displayName: string;
        }) => apiClient.post<AuthTokenResponse>("/auth/register", credentials),
        onSuccess: handleSuccessfulAuth,
    });
}

export function useLogin() {
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (credentials: { email: string; password: string }) =>
            apiClient.post<AuthTokenResponse>("/auth/login", credentials),
        onSuccess: handleSuccessfulAuth,
    });
}

export function useGoogleLogin() {
    const handleSuccessfulAuth = useHandleSuccessfulAuth();

    return useMutation({
        mutationFn: (idToken: string) =>
            apiClient.post<AuthTokenResponse>("/auth/google", { idToken }),
        onSuccess: handleSuccessfulAuth,
    });
}

export function useLogout() {
    const router = useRouter();
    const { clearAuthSession } = useAuthStore();

    return useMutation({
        mutationFn: () => apiClient.post<void>("/auth/logout", {}),
        onSuccess: () => {
            clearAuthSession();
            router.push("/login");
        },
    });
}
