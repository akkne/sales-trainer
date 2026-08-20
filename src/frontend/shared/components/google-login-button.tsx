"use client";

import { GoogleLogin } from "@react-oauth/google";
import { useGoogleLogin } from "@/features/auth/hooks/use-auth";
import { toast } from "@/features/notifications/store/toast-store";

export function GoogleLoginButton() {
    const googleLoginMutation = useGoogleLogin();

    return (
        <div className="w-full flex flex-col items-center gap-2">
            <GoogleLogin
                onSuccess={(credentialResponse) => {
                    if (credentialResponse.credential) {
                        googleLoginMutation.mutate(credentialResponse.credential);
                    }
                }}
                onError={() => {
                    // The Google popup itself failed (closed, blocked, no credential) — nothing
                    // reached our server, so there is no mutation error to read. Tell the user
                    // directly instead of staying silent.
                    toast.error("Не удалось войти через Google. Попробуйте ещё раз.");
                }}
                text="signin_with"
                shape="rectangular"
                theme="outline"
                size="large"
                width="360"
            />
            {googleLoginMutation.isError && (
                <p className="auth-error">
                    {googleLoginMutation.error?.message ?? "Не удалось войти через Google"}
                </p>
            )}
        </div>
    );
}
