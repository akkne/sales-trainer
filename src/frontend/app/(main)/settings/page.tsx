"use client";

import Link from "next/link";
import { useLogout } from "@/features/auth/hooks/use-auth";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useThemeStore } from "@/shared/stores/theme-store";
import { useNotificationPreferencesStore } from "@/shared/stores/notification-preferences-store";

// NOTE: No email-change or password-change flow exists in this frontend.
// Those rows are rendered as read-only / display-only accordingly.
// NOTE: Notification preferences are persisted to localStorage via notification-preferences-store.

type Theme = "light" | "dark";

const THEME_OPTIONS: { value: Theme; label: string; icon: React.ReactNode }[] = [
    {
        value: "light",
        label: "Light",
        icon: (
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="4"/>
                <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"/>
            </svg>
        ),
    },
    {
        value: "dark",
        label: "Dark",
        icon: (
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
            </svg>
        ),
    },
];

export default function SettingsPage() {
    const logoutMutation = useLogout();
    const { authenticatedUser } = useAuthStore();
    const { theme, setTheme } = useThemeStore();

    const {
        isPracticeRemindersEnabled,
        isProductUpdatesEnabled,
        setPracticeRemindersEnabled,
        setProductUpdatesEnabled,
    } = useNotificationPreferencesStore();

    const isAdmin =
        authenticatedUser?.role === "Admin" ||
        authenticatedUser?.role === "SuperAdmin";

    // Collapse "system" to the nearest explicit choice for the 2-segment selector.
    // The underlying store still records "system" — we just display it as the
    // currently-resolved value (light/dark) until the user explicitly picks one.
    const activeSegment: Theme = theme === "dark" ? "dark" : "light";

    return (
        <div className="stg-scroll">
            <div className="stg-inner">
                {/* Header */}
                <div className="stg-header">
                    <h1 className="stg-title">Settings</h1>
                    <p className="stg-sub">Manage appearance and account</p>
                </div>

                {/* Appearance */}
                <div className="stg-card">
                    <p className="stg-card-title">Appearance</p>
                    <div className="stg-seg" role="group" aria-label="Theme">
                        {THEME_OPTIONS.map((opt) => (
                            <button
                                key={opt.value}
                                type="button"
                                className={`stg-seg-btn${activeSegment === opt.value ? " active" : ""}`}
                                onClick={() => setTheme(opt.value)}
                                aria-pressed={activeSegment === opt.value}
                            >
                                {opt.icon}
                                {opt.label}
                            </button>
                        ))}
                    </div>
                </div>

                {/* Notifications */}
                <div className="stg-card">
                    <p className="stg-card-title">Notifications</p>

                    <div className="stg-toggle-row">
                        <div className="stg-toggle-body">
                            <p className="stg-toggle-label">Practice reminders</p>
                            <p className="stg-toggle-sub">Daily nudge to keep your streak</p>
                        </div>
                        <button
                            type="button"
                            role="switch"
                            aria-checked={isPracticeRemindersEnabled}
                            className="stg-switch"
                            onClick={() => setPracticeRemindersEnabled(!isPracticeRemindersEnabled)}
                            aria-label="Practice reminders"
                        />
                    </div>

                    <div className="stg-toggle-row">
                        <div className="stg-toggle-body">
                            <p className="stg-toggle-label">Product updates</p>
                            <p className="stg-toggle-sub">News about new skills and features</p>
                        </div>
                        <button
                            type="button"
                            role="switch"
                            aria-checked={isProductUpdatesEnabled}
                            className="stg-switch"
                            onClick={() => setProductUpdatesEnabled(!isProductUpdatesEnabled)}
                            aria-label="Product updates"
                        />
                    </div>
                </div>

                {/* Account */}
                <div className="stg-card">
                    <p className="stg-card-title">Account</p>

                    {/* Email — read-only: no email-change flow in the app */}
                    <div className="stg-row">
                        <div className="stg-row-body">
                            <p className="stg-row-label">Email</p>
                            <p className="stg-row-sub">
                                {authenticatedUser?.email ?? "—"}
                            </p>
                        </div>
                        {/* "Change" omitted: no email-change flow exists */}
                    </div>

                    {/* Password — omitted: no password-change flow exists in the frontend */}

                    {/* Admin area — admins only */}
                    {isAdmin && (
                        <div className="stg-row">
                            <div className="stg-row-body">
                                <p className="stg-row-label">Admin panel</p>
                                <p className="stg-row-sub">Manage content and users</p>
                            </div>
                            <Link href="/admin/skills" className="stg-row-action">
                                Open
                                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                                    <path d="m9 18 6-6-6-6"/>
                                </svg>
                            </Link>
                        </div>
                    )}
                </div>

                {/* Log out */}
                <button
                    type="button"
                    className="stg-logout"
                    onClick={() => logoutMutation.mutate()}
                    disabled={logoutMutation.isPending}
                >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/>
                    </svg>
                    {logoutMutation.isPending ? "Logging out…" : "Log out"}
                </button>
            </div>
        </div>
    );
}
