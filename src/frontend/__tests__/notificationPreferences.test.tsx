import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";

vi.mock("@/shared/api/api-client", () => ({
    apiClient: {
        get: vi.fn(),
        put: vi.fn(),
    },
}));

import { apiClient } from "@/shared/api/api-client";
import {
    useMigrateLegacyNotificationPreferences,
    type NotificationPreferences,
} from "@/features/notifications/hooks/use-notification-preferences";

const mockPut = apiClient.put as ReturnType<typeof vi.fn>;

const PRACTICE_KEY = "notif.practiceReminders";
const UPDATES_KEY = "notif.productUpdates";

function wrapper({ children }: { children: ReactNode }) {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

const SERVER_DEFAULTS: NotificationPreferences = {
    practiceReminders: true,
    productUpdates: false,
    isDefault: true,
};

const SERVER_SAVED: NotificationPreferences = {
    practiceReminders: true,
    productUpdates: false,
    isDefault: false,
};

/**
 * Q-4, migration step (b) (`docs/NIGHT_AUDIT_QUESTIONS.md`). These two switches lived only in
 * `localStorage` until the API existed. The migration is what stops the switch to a server-side
 * source of truth from quietly discarding every choice already made in a browser — in particular
 * from switching product updates back *on* for someone who had deliberately turned them off.
 *
 * The trap it has to avoid is the mirror image: a stale `localStorage` on one machine must never
 * overwrite a preference the user has since saved from another. That is what `isDefault` is for, and
 * it is the reason each of these cases is worth pinning.
 */
describe("legacy notification-preference migration", () => {
    beforeEach(() => {
        mockPut.mockReset();
        mockPut.mockResolvedValue(SERVER_SAVED);
        localStorage.clear();
    });

    it("does nothing when the browser holds no legacy value", async () => {
        renderHook(() => useMigrateLegacyNotificationPreferences(SERVER_DEFAULTS), { wrapper });

        await waitFor(() => expect(mockPut).not.toHaveBeenCalled());
    });

    it("uploads a legacy choice the server has never heard", async () => {
        localStorage.setItem(UPDATES_KEY, "true");
        localStorage.setItem(PRACTICE_KEY, "false");

        renderHook(() => useMigrateLegacyNotificationPreferences(SERVER_DEFAULTS), { wrapper });

        await waitFor(() =>
            expect(mockPut).toHaveBeenCalledWith("/notifications/preferences", {
                practiceReminders: false,
                productUpdates: true,
            })
        );
        // Deleted, so it cannot be replayed over a later, deliberate change.
        await waitFor(() => expect(localStorage.getItem(UPDATES_KEY)).toBeNull());
        expect(localStorage.getItem(PRACTICE_KEY)).toBeNull();
    });

    it("fills only the switch the browser actually held, from the server for the other", async () => {
        // Somebody who touched one toggle and never the other: the untouched one must come from the
        // server's own answer, not from a guessed `false`.
        localStorage.setItem(UPDATES_KEY, "true");

        renderHook(() => useMigrateLegacyNotificationPreferences(SERVER_DEFAULTS), { wrapper });

        await waitFor(() =>
            expect(mockPut).toHaveBeenCalledWith("/notifications/preferences", {
                practiceReminders: true,
                productUpdates: true,
            })
        );
    });

    it("never overwrites a preference the user has already saved", async () => {
        localStorage.setItem(UPDATES_KEY, "true");

        renderHook(() => useMigrateLegacyNotificationPreferences(SERVER_SAVED), { wrapper });

        await waitFor(() => expect(localStorage.getItem(UPDATES_KEY)).toBeNull());
        // The stored choice wins; the stale local key is only cleaned up.
        expect(mockPut).not.toHaveBeenCalled();
    });

    it("keeps the legacy keys when the upload fails, so the next visit retries", async () => {
        mockPut.mockRejectedValue(new Error("500"));
        localStorage.setItem(UPDATES_KEY, "true");

        renderHook(() => useMigrateLegacyNotificationPreferences(SERVER_DEFAULTS), { wrapper });

        await waitFor(() => expect(mockPut).toHaveBeenCalledTimes(1));
        // Dropping them here would lose the user's choice for good.
        expect(localStorage.getItem(UPDATES_KEY)).toBe("true");
    });

    it("waits for the server's answer before deciding anything", async () => {
        localStorage.setItem(UPDATES_KEY, "true");

        // `undefined` is "the read has not landed yet". Acting on it would mean guessing whether a
        // stored preference exists, which is the one thing the migration must not guess.
        renderHook(() => useMigrateLegacyNotificationPreferences(undefined), { wrapper });

        await waitFor(() => expect(mockPut).not.toHaveBeenCalled());
        expect(localStorage.getItem(UPDATES_KEY)).toBe("true");
    });

    it("uploads once even across re-renders", async () => {
        localStorage.setItem(UPDATES_KEY, "true");

        const { rerender } = renderHook(
            (preferences: NotificationPreferences | undefined) =>
                useMigrateLegacyNotificationPreferences(preferences),
            { wrapper, initialProps: SERVER_DEFAULTS as NotificationPreferences | undefined }
        );

        await waitFor(() => expect(mockPut).toHaveBeenCalledTimes(1));

        rerender(SERVER_DEFAULTS);
        rerender({ ...SERVER_DEFAULTS });

        await waitFor(() => expect(mockPut).toHaveBeenCalledTimes(1));
    });
});
