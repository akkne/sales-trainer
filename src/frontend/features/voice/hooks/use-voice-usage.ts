import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { VoiceApiRoutes } from "@/features/voice/constants/voice-api-routes";
import type { VoiceUsage } from "@/features/voice/types/voice-usage";

export function useVoiceUsage(enabled = true) {
    return useQuery({
        queryKey: ["voice", "usage"],
        queryFn: () => apiClient.get<VoiceUsage>(VoiceApiRoutes.usage),
        staleTime: 30 * 1000,
        enabled,
    });
}
