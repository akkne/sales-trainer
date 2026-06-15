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

async function fetchWithAuthToken<TResponseBody>(
    path: string,
    requestOptions?: RequestInit
): Promise<TResponseBody> {
    const accessToken =
        typeof window !== "undefined"
            ? localStorage.getItem("accessToken")
            : null;

    const isFormData = requestOptions?.body instanceof FormData;

    const response = await fetch(`${API_BASE_URL}${path}`, {
        ...requestOptions,
        credentials: "include",
        headers: {
            ...(isFormData ? {} : { "Content-Type": "application/json" }),
            ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
            ...requestOptions?.headers,
        },
    });

    if (response.status === 401) {
        const refreshSucceeded = await attemptTokenRefresh();
        if (refreshSucceeded) {
            return fetchWithAuthToken<TResponseBody>(path, requestOptions);
        }
        localStorage.removeItem("accessToken");
        window.location.href = "/login";
        throw new Error("Session expired");
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

    post: <TResponseBody>(path: string, requestBody: unknown) =>
        fetchWithAuthToken<TResponseBody>(path, {
            method: "POST",
            body: JSON.stringify(requestBody),
        }),

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

    delete: <TResponseBody>(path: string) =>
        fetchWithAuthToken<TResponseBody>(path, { method: "DELETE" }),
};
