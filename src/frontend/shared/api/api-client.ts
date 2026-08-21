import { EnvironmentConfiguration } from "@/config/environment";

const API_BASE_URL = EnvironmentConfiguration.apiBaseUrl;

export class ApiError extends Error {
    readonly status: number;
    readonly payload: Record<string, unknown>;

    constructor(status: number, payload: Record<string, unknown>) {
        super(
            typeof payload.message === "string"
                ? payload.message
                : `HTTP ${status}`
        );
        this.name = "ApiError";
        this.status = status;
        this.payload = payload;
    }
}

/**
 * Thrown when a request did not answer within its `timeoutMs` budget. Callers that
 * wait behind a spinner must give up at some point — a request that never settles
 * leaves the UI loading forever.
 */
export class RequestTimeoutError extends Error {
    readonly timeoutMs: number;

    constructor(timeoutMs: number) {
        super(`Request timed out after ${timeoutMs}ms`);
        this.name = "RequestTimeoutError";
        this.timeoutMs = timeoutMs;
    }
}

/**
 * Thrown when a request got a 401 and the refresh attempt also failed (or was skipped because
 * the session was deliberately ended, see `SESSION_TERMINATED_KEY` below). `fetchWithAuthToken`
 * already starts a hard navigation to `/login` itself in this case, so callers must not also
 * soft-navigate or show a "your session may have been stolen" warning on top of a plain expiry
 * (R-2).
 */
export class SessionExpiredError extends Error {
    constructor() {
        super("Session expired");
        this.name = "SessionExpiredError";
    }
}

/**
 * R-1: set by `useAuthStore.clearAuthSession({ terminated: true })` whenever the client
 * deliberately ends a session — a real logout, or `/auth/me` explicitly rejecting the stored
 * token (401) — regardless of whether the server-side revoke call itself succeeded. As long as
 * this flag is set, `attemptTokenRefresh` below refuses to mint a new access token from a
 * leftover refresh-cookie, so a failed `POST /auth/logout` cannot be silently undone by the very
 * next request's 401. Cleared by `useAuthStore.setAccessToken` on the next successful login,
 * which is the only place a new session legitimately begins — the ordinary "access token merely
 * expired, refresh cookie is still good" refresh on a *live* session never sets this flag, so
 * that path is unaffected.
 *
 * R2-5: a *transient* failure (network error, timeout, 500) is not a deliberate end of session —
 * callers must not set this flag for those, or a momentary blip permanently disables refresh.
 * Exported so `useAuthStore` never has to duplicate the literal (R2-6).
 */
export const SESSION_TERMINATED_KEY = "authSessionTerminated";

export interface ApiRequestOptions {
    /** Abort the request after this many milliseconds and throw `RequestTimeoutError`. */
    timeoutMs?: number;
}

async function fetchWithAuthToken<TResponseBody>(
    path: string,
    requestOptions?: RequestInit,
    apiRequestOptions?: ApiRequestOptions
): Promise<TResponseBody> {
    const accessToken =
        typeof window !== "undefined"
            ? localStorage.getItem("accessToken")
            : null;

    const isFormData = requestOptions?.body instanceof FormData;

    const timeoutMs = apiRequestOptions?.timeoutMs;
    const timeoutController = timeoutMs ? new AbortController() : null;
    const timeoutHandle = timeoutController
        ? setTimeout(() => timeoutController.abort(), timeoutMs)
        : null;

    let response: Response;
    try {
        response = await fetch(`${API_BASE_URL}${path}`, {
            ...requestOptions,
            credentials: "include",
            ...(timeoutController ? { signal: timeoutController.signal } : {}),
            headers: {
                ...(isFormData ? {} : { "Content-Type": "application/json" }),
                ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
                ...requestOptions?.headers,
            },
        });
    } catch (error) {
        if (timeoutController?.signal.aborted && timeoutMs) {
            throw new RequestTimeoutError(timeoutMs);
        }
        throw error;
    } finally {
        if (timeoutHandle) clearTimeout(timeoutHandle);
    }

    if (response.status === 401) {
        const refreshSucceeded = await attemptTokenRefresh();
        if (refreshSucceeded) {
            return fetchWithAuthToken<TResponseBody>(path, requestOptions, apiRequestOptions);
        }
        localStorage.removeItem("accessToken");
        window.location.href = "/login";
        throw new SessionExpiredError();
    }

    if (!response.ok) {
        const errorBody = await response.json().catch(() => ({}));
        throw new ApiError(response.status, errorBody);
    }

    if (response.status === 204) {
        return undefined as TResponseBody;
    }

    return response.json() as Promise<TResponseBody>;
}

let pendingRefresh: Promise<boolean> | null = null;

async function attemptTokenRefresh(): Promise<boolean> {
    if (typeof window !== "undefined" && localStorage.getItem(SESSION_TERMINATED_KEY)) {
        return false;
    }
    if (pendingRefresh) return pendingRefresh;
    pendingRefresh = doRefresh().finally(() => {
        pendingRefresh = null;
    });
    return pendingRefresh;
}

async function doRefresh(): Promise<boolean> {
    try {
        const refreshResponse = await fetch(`${API_BASE_URL}/auth/refresh`, {
            method: "POST",
            credentials: "include",
        });

        if (!refreshResponse.ok) return false;

        const { accessToken } = await refreshResponse.json();
        localStorage.setItem("accessToken", accessToken);
        return true;
    } catch {
        return false;
    }
}

export const apiClient = {
    get: <TResponseBody>(path: string) =>
        fetchWithAuthToken<TResponseBody>(path),

    post: <TResponseBody>(path: string, requestBody: unknown, options?: ApiRequestOptions) =>
        fetchWithAuthToken<TResponseBody>(
            path,
            {
                method: "POST",
                body: JSON.stringify(requestBody),
            },
            options
        ),

    postFile: <TResponseBody>(path: string, formData: FormData) =>
        fetchWithAuthToken<TResponseBody>(path, {
            method: "POST",
            headers: {},
            body: formData,
        }),

    put: <TResponseBody>(path: string, requestBody: unknown) =>
        fetchWithAuthToken<TResponseBody>(path, {
            method: "PUT",
            body: JSON.stringify(requestBody),
        }),

    patch: <TResponseBody>(path: string, requestBody: unknown) =>
        fetchWithAuthToken<TResponseBody>(path, {
            method: "PATCH",
            body: JSON.stringify(requestBody),
        }),

    delete: <TResponseBody>(path: string) =>
        fetchWithAuthToken<TResponseBody>(path, { method: "DELETE" }),
};
