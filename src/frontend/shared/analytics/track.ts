import { apiClient } from "@/shared/api/api-client";

/**
 * Lightweight usage tracking. Posts events to the backend (`POST /tracking/events`),
 * which folds them into Prometheus counters. The backend validates event/page against
 * a whitelist, so the names used here must stay in sync with
 * `Features/Metrics/Constants/TrackedEvents.cs`.
 *
 * Tracking is best-effort: every call swallows errors and never throws, so analytics
 * can never break the UX. Events are only sent for authenticated users (the endpoint
 * requires auth) — anonymous calls are skipped silently.
 */

export type TrackedPage =
    | "tree"
    | "dialog"
    | "profile"
    | "guidebook"
    | "friends"
    | "discuss"
    | "session"
    | "login"
    | "register"
    | "onboarding"
    | "admin"
    | "other";

export type TrackedEvent =
    | "start_dialog"
    | "start_lesson"
    | "complete_lesson"
    | "open_skill"
    | "open_technique"
    | "send_message"
    | "add_friend"
    | "edit_profile";

const PAGE_VIEW_EVENT = "page_view";

function hasAccessToken(): boolean {
    return (
        typeof window !== "undefined" &&
        localStorage.getItem("accessToken") !== null
    );
}

async function send(event: string, page: TrackedPage): Promise<void> {
    if (!hasAccessToken()) return;
    try {
        await apiClient.post("/tracking/events", { event, page });
    } catch {
        // best-effort: analytics must never surface errors to the user
    }
}

export function trackEvent(event: TrackedEvent, page: TrackedPage): void {
    void send(event, page);
}

export function trackPageView(page: TrackedPage): void {
    void send(PAGE_VIEW_EVENT, page);
}
