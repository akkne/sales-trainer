/**
 * Bookkeeping for an active impersonation session (Phase 40.9).
 *
 * The impersonation token replaces the platform staff member's own access token in
 * `localStorage`, because every request in the app reads it from there. The token it displaces is
 * parked in `sessionStorage` so the person can get back out — without it, impersonating would be a
 * one-way door until the short token expired.
 *
 * `sessionStorage` rather than `localStorage` on purpose: the parked platform token dies with the
 * tab, so a shared or forgotten machine does not keep a superadmin token sitting next to the
 * impersonation one.
 */

const PLATFORM_ACCESS_TOKEN_KEY = "impersonation.platformAccessToken";
const ORGANIZATION_NAME_KEY = "impersonation.organizationName";
const EXPIRES_AT_KEY = "impersonation.expiresAt";

export interface ImpersonationSession {
    platformAccessToken: string;
    organizationName: string;
    expiresAt: string;
}

function isBrowser(): boolean {
    return typeof window !== "undefined" && typeof window.sessionStorage !== "undefined";
}

/**
 * `sessionStorage` fires no event in the tab that wrote it, so writers announce themselves and
 * `subscribeToImpersonationSession` listens. Without this the banner would only notice a change
 * after a full page load.
 */
export const IMPERSONATION_SESSION_CHANGED_EVENT = "impersonation-session-changed";

function announceChange(): void {
    if (!isBrowser()) return;
    window.dispatchEvent(new Event(IMPERSONATION_SESSION_CHANGED_EVENT));
}

export function beginImpersonationSession(session: ImpersonationSession): void {
    if (!isBrowser()) return;
    sessionStorage.setItem(PLATFORM_ACCESS_TOKEN_KEY, session.platformAccessToken);
    sessionStorage.setItem(ORGANIZATION_NAME_KEY, session.organizationName);
    sessionStorage.setItem(EXPIRES_AT_KEY, session.expiresAt);
    announceChange();
}

export function readImpersonationSession(): ImpersonationSession | null {
    if (!isBrowser()) return null;

    const platformAccessToken = sessionStorage.getItem(PLATFORM_ACCESS_TOKEN_KEY);
    const organizationName = sessionStorage.getItem(ORGANIZATION_NAME_KEY);
    const expiresAt = sessionStorage.getItem(EXPIRES_AT_KEY);

    if (!platformAccessToken || !organizationName || !expiresAt) {
        return null;
    }

    return { platformAccessToken, organizationName, expiresAt };
}

export function clearImpersonationSession(): void {
    if (!isBrowser()) return;
    sessionStorage.removeItem(PLATFORM_ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(ORGANIZATION_NAME_KEY);
    sessionStorage.removeItem(EXPIRES_AT_KEY);
    announceChange();
}

/**
 * `useSyncExternalStore` compares snapshots by reference and re-renders forever if a fresh object
 * comes back every call, so the snapshot is the serialized form and the previous string is kept to
 * return the identical instance while nothing has changed.
 */
let lastSerializedSession: string | null = null;

export function readSerializedImpersonationSession(): string | null {
    const session = readImpersonationSession();
    const serialized = session === null ? null : JSON.stringify(session);
    if (serialized !== lastSerializedSession) {
        lastSerializedSession = serialized;
    }
    return lastSerializedSession;
}

export function readServerImpersonationSession(): string | null {
    return null;
}

export function subscribeToImpersonationSession(onChange: () => void): () => void {
    if (!isBrowser()) return () => undefined;

    window.addEventListener("storage", onChange);
    window.addEventListener(IMPERSONATION_SESSION_CHANGED_EVENT, onChange);
    return () => {
        window.removeEventListener("storage", onChange);
        window.removeEventListener(IMPERSONATION_SESSION_CHANGED_EVENT, onChange);
    };
}

export function isImpersonationExpired(
    session: ImpersonationSession,
    now: Date = new Date()
): boolean {
    const expiresAt = new Date(session.expiresAt);
    return Number.isNaN(expiresAt.getTime()) || expiresAt.getTime() <= now.getTime();
}
