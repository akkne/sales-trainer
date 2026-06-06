"use client";

import { useThemeStore } from "@/shared/stores/theme-store";
import { Icon, IconName } from "@/shared/components/icon";

type Theme = "light" | "dark" | "system";

const THEME_OPTIONS: { value: Theme; label: string; icon: IconName }[] = [
    { value: "light", label: "Светлая", icon: "sun" },
    { value: "dark", label: "Тёмная", icon: "moon" },
    { value: "system", label: "Системная", icon: "settings" },
];

export function ThemeToggle() {
    const { theme, setTheme } = useThemeStore();

    return (
        <div
            className="bg-surface border border-line rounded-2xl p-4"
            style={{ boxShadow: "var(--sh-1)" }}
        >
            <div className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4 mb-3">
                Тема оформления
            </div>
            <div className="flex gap-2">
                {THEME_OPTIONS.map((option) => (
                    <button
                        key={option.value}
                        onClick={() => setTheme(option.value)}
                        className={`flex-1 flex flex-col items-center gap-2 py-3 px-2 rounded-xl transition-all ${
                            theme === option.value
                                ? "bg-indigo text-white"
                                : "bg-bg-2 text-ink-3 hover:text-ink"
                        }`}
                        style={theme === option.value ? { boxShadow: "var(--sh-2)" } : undefined}
                    >
                        <Icon name={option.icon} size="md" />
                        <span className="text-xs font-medium">{option.label}</span>
                    </button>
                ))}
            </div>
        </div>
    );
}
