"use client";

import { GoogleLogin } from "@react-oauth/google";
import { useGoogleLogin } from "@/lib/hooks/useAuth";

export function GoogleLoginButton() {
    const googleLoginMutation = useGoogleLogin();

    return (
        <div className="w-full flex justify-center">
            <GoogleLogin
                onSuccess={(credentialResponse) => {
                    if (credentialResponse.credential) {
                        googleLoginMutation.mutate(credentialResponse.credential);
                    }
                }}
                onError={() => {
                    // error state handled inside useGoogleLogin mutation
                }}
                text="signin_with"
                shape="rectangular"
                theme="outline"
                size="large"
                width="360"
            />
        </div>
    );
}
