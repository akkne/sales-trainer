export const EnvironmentConfiguration = {
    apiBaseUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000",
    googleClientId: process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "",
} as const;
