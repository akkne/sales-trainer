import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

import { apiClient, ApiError, SessionExpiredError } from "@/shared/api/api-client";

function jsonResponse(status: number, body: unknown) {
    return {
        ok: status >= 200 && status < 300,
        status,
        json: async () => body,
    } as unknown as Response;
}

/**
 * 2026-08-27 — a wrong password on `/auth/login` used to be treated the same as a live session's
 * access token dying: the generic 401 handler tried a token refresh, then hard-navigated to
 * `/login` and threw `SessionExpiredError`, so the password form showed "Session expired" instead
 * of the server's actual "Invalid email or password." and reset itself mid-attempt. Bootstrap
 * endpoints authenticate a session rather than use one, so their 401 can never mean the session
 * expired — there isn't one yet.
 */
describe("fetchWithAuthToken 401 handling", () => {
    beforeEach(() => {
        vi.unstubAllGlobals();
        localStorage.clear();
    });

    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it("surfaces a wrong-password /auth/login 401 as the server's own message, without refreshing or redirecting", async () => {
        const fetchMock = vi
            .fn()
            .mockResolvedValueOnce(jsonResponse(401, { message: "Invalid email or password." }));
        vi.stubGlobal("fetch", fetchMock);

        await expect(
            apiClient.post("/auth/login", { email: "a@test.com", password: "wrong" })
        ).rejects.toMatchObject({
            message: "Invalid email or password.",
        });

        expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    it("still recovers a genuinely expired token on a protected endpoint via refresh", async () => {
        localStorage.setItem("accessToken", "stale-token");
        const fetchMock = vi
            .fn()
            // Original request with the stale token.
            .mockResolvedValueOnce(jsonResponse(401, {}))
            // Refresh succeeds with a new token.
            .mockResolvedValueOnce(jsonResponse(200, { accessToken: "fresh-token" }))
            // Retried original request now succeeds.
            .mockResolvedValueOnce(jsonResponse(200, { ok: true }));
        vi.stubGlobal("fetch", fetchMock);

        await expect(apiClient.get("/skill-tree")).resolves.toEqual({ ok: true });
        expect(fetchMock).toHaveBeenCalledTimes(3);
        expect(fetchMock.mock.calls[1][0]).toContain("/auth/refresh");
    });

    it("ends the session when a protected endpoint 401s and refresh also fails", async () => {
        localStorage.setItem("accessToken", "stale-token");
        const fetchMock = vi
            .fn()
            .mockResolvedValueOnce(jsonResponse(401, {}))
            .mockResolvedValueOnce(jsonResponse(401, {}));
        vi.stubGlobal("fetch", fetchMock);

        await expect(apiClient.get("/skill-tree")).rejects.toBeInstanceOf(SessionExpiredError);
        expect(localStorage.getItem("accessToken")).toBeNull();
    });

    it("still throws a plain ApiError for a non-401 /auth/login failure", async () => {
        const fetchMock = vi
            .fn()
            .mockResolvedValueOnce(jsonResponse(403, { message: "Organization suspended" }));
        vi.stubGlobal("fetch", fetchMock);

        await expect(
            apiClient.post("/auth/login", { email: "a@test.com", password: "x" })
        ).rejects.toBeInstanceOf(ApiError);
    });
});
