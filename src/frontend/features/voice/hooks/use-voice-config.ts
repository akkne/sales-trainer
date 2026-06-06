import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { VoiceApiRoutes } from "@/features/voice/constants/voice-api-routes";
import type { VoiceConfig } from "@/features/voice/types/voice-config";
import { TimingConstants } from "@/shared/constants/timing-constants";

export function useVoiceConfig() {
    return useQuery({
        queryKey: ["voice", "config"],
        queryFn: () => apiClient.get<VoiceConfig>(VoiceApiRoutes.config),
        staleTime: TimingConstants.fiveMinutesMs,
    });
}
