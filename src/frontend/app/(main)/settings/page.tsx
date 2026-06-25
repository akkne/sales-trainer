"use client";

import Link from "next/link";
import { useLogout } from "@/features/auth/hooks/use-auth";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useThemeStore } from "@/shared/stores/theme-store";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";

type Theme = "light" | "dark" | "system";

const THEME_OPTIONS: { value: Theme; label: string; icon: IconName }[] = [
    { value: "light",  label: "Светлая",   icon: "sun" },
    { value: "dark",   label: "Тёмная",    icon: "moon" },
    { value: "system", label: "Системная", icon: "settings" },
];

export default function SettingsPage() {
    const logoutMutation = useLogout();
    const { authenticatedUser } = useAuthStore();
    const { theme, setTheme } = useThemeStore();

    const isAdmin =
        authenticatedUser?.role === "Admin" ||
        authenticatedUser?.role === "SuperAdmin";

    return (
        <div className="page">
            <div className="app-backdrop" />
            <div className="container" style={{ maxWidth: 720 }}>
                <h1 className="h1" style={{ marginBottom: 24 }}>
                    Настройки
                </h1>

                {/* Appearance */}
                <div className="card card-pad" style={{ marginBottom: 16 }}>
                    <span className="eyebrow muted">Тема оформления</span>
                    <div className="theme-grid" style={{ marginTop: 14 }}>
                        {THEME_OPTIONS.map((option) => (
                            <button
                                key={option.value}
                                className={"theme-opt" + (theme === option.value ? " active" : "")}
                                onClick={() => setTheme(option.value)}
                            >
                                <Icon name={option.icon} size={22} />
                                <span>{option.label}</span>
                            </button>
                        ))}
                    </div>
                </div>

                {/* Admin area (admins only) */}
                {isAdmin && (
                    <Link
                        href="/admin/skills"
                        className="card card-pad logout-row"
                        style={{ marginBottom: 16 }}
                    >
                        <span className="itile primary" style={{ width: 40, height: 40 }}>
                            <Icon name="settings" size={20} />
                        </span>
                        <div className="grow" style={{ textAlign: "left" }}>
                            <div className="h4">Панель администратора</div>
                            <div className="small">Управление контентом</div>
                        </div>
                        <Icon name="chevron-right" style={{ color: "var(--ink-4)" }} />
                    </Link>
                )}

                {/* Logout */}
                <button
                    className="card card-pad logout-row"
                    onClick={() => logoutMutation.mutate()}
                    disabled={logoutMutation.isPending}
                >
                    <span className="itile heart" style={{ width: 40, height: 40 }}>
                        <Icon name="arrow-left" size={20} />
                    </span>
                    <div className="grow" style={{ textAlign: "left" }}>
                        <div className="h4" style={{ color: "var(--heart)" }}>
                            Выйти из аккаунта
                        </div>
                        <div className="small">Завершить сессию</div>
                    </div>
                    <Icon name="chevron-right" style={{ color: "var(--ink-4)" }} />
                </button>
            </div>
        </div>
    );
}
