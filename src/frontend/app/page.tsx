"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuthStore } from "@/shared/stores/auth-store";

/**
 * The default path is a fork, not a page: someone already signed in goes straight to the app,
 * everyone else gets the public landing. It has to run on the client because the access token
 * lives in localStorage (see `shared/stores/auth-store.ts`), so the server cannot tell the two
 * visitors apart. `/landing` itself is deliberately kept free of this check — it stays reachable
 * for a signed-in user who asked for it by name.
 */
export default function RootPage() {
    const router = useRouter();
    const { accessToken } = useAuthStore();

    useEffect(() => {
        router.replace(accessToken ? "/tree" : "/landing");
    }, [accessToken, router]);

    return null;
}
