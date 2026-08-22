import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { apiClient } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import { toast } from "@/features/notifications/store/toast-store";

export interface NotificationPreferences {
    practiceReminders: boolean;
    productUpdates: boolean;
    /**
     * True while nobody has ever saved these — the server is answering with its documented defaults
     * rather than a stored choice. The one thing that distinguishes "new user" from "user who chose
     * the defaults", and therefore the only safe trigger for the migration below.
     */
    isDefault: boolean;
}

const QUERY_KEY = ["notification-preferences"];
const ENDPOINT = "/notifications/preferences";

/**
 * Keys the browser-only store used before there was an API (Q-4, `docs/NIGHT_AUDIT_QUESTIONS.md`).
 * Read exactly once per browser, to carry a real choice up to the server, and then deleted.
 */
const LEGACY_PRACTICE_REMINDERS_KEY = "notif.practiceReminders";
const LEGACY_PRODUCT_UPDATES_KEY = "notif.productUpdates";

interface LegacyPreferences {
    practiceReminders?: boolean;
    productUpdates?: boolean;
}

function readLegacyPreferences(): LegacyPreferences {
    if (typeof window === "undefined") return {};
    try {
        const legacy: LegacyPreferences = {};
        const practiceReminders = localStorage.getItem(LEGACY_PRACTICE_REMINDERS_KEY);
        const productUpdates = localStorage.getItem(LEGACY_PRODUCT_UPDATES_KEY);
        if (practiceReminders !== null) legacy.practiceReminders = practiceReminders === "true";
        if (productUpdates !== null) legacy.productUpdates = productUpdates === "true";
        return legacy;
    } catch {
        return {};
    }
}

function clearLegacyPreferences(): void {
    if (typeof window === "undefined") return;
    try {
        localStorage.removeItem(LEGACY_PRACTICE_REMINDERS_KEY);
        localStorage.removeItem(LEGACY_PRODUCT_UPDATES_KEY);
    } catch {
        // Nothing to recover: the keys are only ever read while the server row is still absent, so
        // a failure here at worst re-attempts the same idempotent migration on the next visit.
    }
}

export function useNotificationPreferences() {
    return useQuery({
        queryKey: QUERY_KEY,
        queryFn: () => apiClient.get<NotificationPreferences>(ENDPOINT),
    });
}

/**
 * Saves both switches. The server answers with what it stored, which is written straight into the
 * cache — so the screen shows the server's state rather than the state it hoped it sent, and
 * `isDefault` flips in the same round trip.
 */
export function useUpdateNotificationPreferences() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (body: { practiceReminders: boolean; productUpdates: boolean }) =>
            apiClient.put<NotificationPreferences>(ENDPOINT, body),
        onSuccess: (saved) => {
            queryClient.setQueryData(QUERY_KEY, saved);
        },
        onError: (error) => {
            clientLogger.error("Failed to save notification preferences", {
                error: (error as Error).message,
            });
            toast.error("Не удалось сохранить настройки уведомлений");
        },
    });
}

/**
 * Q-4, migration step (b). Carries the values the old browser-only store held up to the server, once
 * per browser, the first time the settings screen loads after this deploy.
 *
 * Without this, everyone who had deliberately configured these switches would read as "default" the
 * moment the server became the source of truth — and someone who had switched product updates off
 * would silently be switched back on. It fires only while the server says `isDefault`, so a real
 * saved preference is never overwritten by a stale `localStorage` on some other machine; the legacy
 * keys are deleted either way, so this can only ever run once per browser.
 */
export function useMigrateLegacyNotificationPreferences(
    preferences: NotificationPreferences | undefined
): void {
    const update = useUpdateNotificationPreferences();
    const hasRun = useRef(false);

    useEffect(() => {
        if (!preferences || hasRun.current) return;
        hasRun.current = true;

        const legacy = readLegacyPreferences();
        const hasLegacyValue =
            legacy.practiceReminders !== undefined || legacy.productUpdates !== undefined;

        if (!hasLegacyValue) return;

        // The server already holds a real choice — that one wins, and the leftover local keys are
        // just noise from before the API existed.
        if (!preferences.isDefault) {
            clearLegacyPreferences();
            return;
        }

        update.mutate(
            {
                practiceReminders: legacy.practiceReminders ?? preferences.practiceReminders,
                productUpdates: legacy.productUpdates ?? preferences.productUpdates,
            },
            {
                onSuccess: () => {
                    clientLogger.info("Migrated browser-only notification preferences to the server");
                    clearLegacyPreferences();
                },
                // On failure the keys stay put so the next visit tries again — dropping them here
                // would lose the user's choice for good, which is the outcome this exists to avoid.
            }
        );
        // `update` is a fresh object every render; the ref above is what makes this run once.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [preferences]);
}
