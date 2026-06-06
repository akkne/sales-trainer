"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { useState, useEffect } from "react";
import { useInitAuth } from "@/features/auth/hooks/use-auth";
import { useThemeStore } from "@/shared/stores/theme-store";

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "";

function AuthInitializer() {
    useInitAuth();
    return null;
}

function ThemeInitializer() {
    const { theme } = useThemeStore();

    useEffect(() => {
        const root = document.documentElement;
        const systemDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
        const isDark = theme === "dark" || (theme === "system" && systemDark);

        if (isDark) {
            root.setAttribute("data-theme", "dark");
        } else {
            root.removeAttribute("data-theme");
        }
    }, [theme]);

    return null;
}

export function AppProviders({ children }: { children: React.ReactNode }) {
    const [queryClient] = useState(
        () =>
            new QueryClient({
                defaultOptions: {
                    queries: { staleTime: 60_000, retry: 1 },
                },
            })
    );

    return (
        <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
            <QueryClientProvider client={queryClient}>
                <AuthInitializer />
                <ThemeInitializer />
                {children}
            </QueryClientProvider>
        </GoogleOAuthProvider>
    );
}
