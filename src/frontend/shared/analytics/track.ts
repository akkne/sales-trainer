import { apiClient } from "@/shared/api/api-client";
import { TimingConstants } from "@/shared/constants/timing-constants";

/**
 * Lightweight usage tracking. Posts events to the backend (`POST /tracking/events`),
 * which folds them into Prometheus counters. The backend validates event/page against
 * a whitelist, so the names used here must stay in sync with
 * `Features/Metrics/Constants/TrackedEvents.cs`.
 *
 * Tracking is best-effort: every call swallows errors and never throws, so analytics
 * can never break the UX. Events are only sent for authenticated users (the endpoint
 * requires auth) — anonymous calls are skipped silently.
 *
 * Bounded by a short client-side timeout (docs/AUDIT_PROD.md A-1): production has shown
 * this endpoint answering 503 specifically on a hard page load, when a burst of other
 * page-data requests fires at the same time — most likely an upstream-connectivity blip
 * between the gateway and analytics-service, not a bug in this code (see docs/DONT_FORGET.md).
 * Without a timeout a stalled call would sit open for the browser's default request
 * lifetime; capping it means a slow/cold upstream can never hold a connection during the
 * page's real data fetches, on top of the try/catch below already making failure silent.
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
    | "invite"
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
        await apiClient.post(
            "/tracking/events",
            { event, page },
            { timeoutMs: TimingConstants.fiveSecondsMs }
        );
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
