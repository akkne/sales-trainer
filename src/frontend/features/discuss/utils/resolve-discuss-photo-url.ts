import { EnvironmentConfiguration } from "@/config/environment";

export function resolveDiscussPhotoUrl(photoUrl: string): string {
    if (photoUrl.startsWith("http://") || photoUrl.startsWith("https://")) {
        return photoUrl;
    }
    const base = EnvironmentConfiguration.apiBaseUrl.replace(/\/$/, "");
    return `${base}${photoUrl}`;
}
