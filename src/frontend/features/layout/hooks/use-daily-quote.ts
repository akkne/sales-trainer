"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface DailyQuote {
    text: string;
    author: string;
    date: string;
}

function formatLocalDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

export function useDailyQuote() {
    const today = formatLocalDate(new Date());
    return useQuery({
        queryKey: ["daily-quote", today],
        // 204 (no quotes at all) resolves to undefined — normalize to null for React Query.
        queryFn: async () =>
            (await apiClient.get<DailyQuote | undefined>(`/daily-quote?date=${today}`)) ?? null,
        staleTime: 5 * 60 * 1000,
    });
}
