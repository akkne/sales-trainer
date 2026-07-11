import { EnvironmentConfiguration } from "@/config/environment";

/**
 * Resolve a (possibly relative) avatar URL against the backend API origin.
 *
 * Avatar endpoints are served by the identity service (`/avatars/{id}`), which
 * in production lives on a different origin than the frontend
 * (e.g. `api.sellevate.site` vs `sellevate.site`). Rendering the raw relative
 * URL in an `<img>` would resolve it against the frontend origin and 404.
 * Absolute URLs are returned unchanged.
 */
export function resolveAvatarUrl(avatarUrl: string): string {
    if (avatarUrl.startsWith("http://") || avatarUrl.startsWith("https://")) {
        return avatarUrl;
    }
    const base = EnvironmentConfiguration.apiBaseUrl.replace(/\/$/, "");
    return `${base}${avatarUrl}`;
}
