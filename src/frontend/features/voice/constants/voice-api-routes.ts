export const VoiceApiRoutes = {
    config: "/dialog/voice/config",
    usage: "/dialog/voice/usage",
    stream: (sessionId: string) => `/dialog/sessions/${sessionId}/voice/stream`,
} as const;
