"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { useInitAuth } from "@/lib/hooks/useAuth";

function AuthInitializer() {
    useInitAuth();
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
        <QueryClientProvider client={queryClient}>
            <AuthInitializer />
            {children}
        </QueryClientProvider>
    );
}
