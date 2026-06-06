import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { VoiceApiRoutes } from "@/features/voice/constants/voice-api-routes";
import type { VoiceConfig } from "@/features/voice/types/voice-config";

export function useVoiceConfig() {
    return useQuery({
        queryKey: ["voice", "config"],
        queryFn: () => apiClient.get<VoiceConfig>(VoiceApiRoutes.config),
        staleTime: 5 * 60 * 1000,
    });
}
