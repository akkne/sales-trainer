const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

async function fetchWithAuthToken<TResponseBody>(
    path: string,
    requestOptions?: RequestInit
): Promise<TResponseBody> {
    const accessToken =
        typeof window !== "undefined"
            ? localStorage.getItem("accessToken")
            : null;

    const response = await fetch(`${API_BASE_URL}${path}`, {
        ...requestOptions,
        headers: {
            "Content-Type": "application/json",
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
        throw new Error(errorBody.message ?? `HTTP ${response.status}`);
    }

    return response.json() as Promise<TResponseBody>;
}

async function attemptTokenRefresh(): Promise<boolean> {
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

    put: <TResponseBody>(path: string, requestBody: unknown) =>
        fetchWithAuthToken<TResponseBody>(path, {
            method: "PUT",
            body: JSON.stringify(requestBody),
        }),

    delete: <TResponseBody>(path: string) =>
        fetchWithAuthToken<TResponseBody>(path, { method: "DELETE" }),
};
