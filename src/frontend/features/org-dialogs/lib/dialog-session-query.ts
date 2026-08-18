import { toApiMaximumScore } from "@/features/org-dialogs/lib/dialog-score";

/**
 * The query string of `GET /admin/dialog-sessions` and the paging arithmetic O5 does around it
 * (docs/TENANCY/ADMIN_UI_DESIGN.md O5).
 *
 * There is no cursor on this endpoint and there will not be one: `DialogSessionRepository` clamps
 * `limit` to a hundred and returns a plain array. «Показать ещё» therefore re-reads a longer page
 * rather than appending to a shorter one, and it stops at the ceiling with a sentence rather than
 * a disabled button, because the way out of a full page here is a narrower filter.
 */

/** `AdminDialogSessionsController.DefaultPageSize`, and the step «Показать ещё» adds. */
export const DIALOG_SESSION_PAGE_SIZE = 25;

/** `DialogSessionRepository.MaximumAdminPageSize`. Asking for more silently gets this. */
export const MAXIMUM_DIALOG_SESSION_PAGE_SIZE = 100;

export interface DialogSessionFilters {
    /** A team member's user id, or null for everybody. */
    userId: string | null;
    /** A scenario's mode id, or null for every scenario. */
    modeId: string | null;
    /** Upper bound on the grade, on the panel's 0–100 scale. Null lifts the filter entirely. */
    maximumPanelScore: number | null;
    limit: number;
}

export const DEFAULT_DIALOG_SESSION_LIMIT = DIALOG_SESSION_PAGE_SIZE;

/** How much of the page the next «Показать ещё» asks for. Never past what the server will serve. */
export function nextDialogSessionLimit(currentLimit: number): number {
    return Math.min(currentLimit + DIALOG_SESSION_PAGE_SIZE, MAXIMUM_DIALOG_SESSION_PAGE_SIZE);
}

export function isDialogSessionLimitAtMaximum(limit: number): boolean {
    return limit >= MAXIMUM_DIALOG_SESSION_PAGE_SIZE;
}

/**
 * The path with only the parameters that are actually set. An empty `userId=` is not the same
 * request as no `userId` at all to a controller binding `Guid?`, so absent filters are absent.
 */
export function buildDialogSessionQueryPath(filters: DialogSessionFilters): string {
    const searchParameters = new URLSearchParams();

    if (filters.userId) searchParameters.set("userId", filters.userId);
    if (filters.modeId) searchParameters.set("modeId", filters.modeId);
    if (filters.maximumPanelScore !== null) {
        searchParameters.set("maxScore", String(toApiMaximumScore(filters.maximumPanelScore)));
    }
    searchParameters.set("limit", String(filters.limit));

    return `/admin/dialog-sessions?${searchParameters.toString()}`;
}
