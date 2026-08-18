"use client";

import {
    TEAM_WINDOW_DAY_OPTIONS,
    TEAM_WINDOW_LABELS,
    type TeamWindowDays,
} from "@/features/org-team/constants/team-window";

interface TeamWindowSelectorProps {
    value: TeamWindowDays;
    onChange: (windowDays: TeamWindowDays) => void;
    disabled?: boolean;
}

/// One window for the whole screen. Both the heat map and the suggestion panel are drawn from it,
/// because a map over ninety days beside advice computed over thirty is a contradiction the screen
/// has no way to explain.
export function TeamWindowSelector({ value, onChange, disabled = false }: TeamWindowSelectorProps) {
    return (
        <div
            role="radiogroup"
            aria-label="Окно наблюдения"
            className="inline-flex items-center gap-1 p-1 rounded-xl border border-line bg-bg-2"
        >
            <span className="px-2 text-xs text-ink-3">Окно</span>
            {TEAM_WINDOW_DAY_OPTIONS.map((windowDays) => {
                const isSelected = windowDays === value;
                return (
                    <button
                        key={windowDays}
                        type="button"
                        role="radio"
                        aria-checked={isSelected}
                        disabled={disabled}
                        onClick={() => onChange(windowDays)}
                        className="px-3 h-7 rounded-lg text-xs font-medium transition-colors disabled:opacity-50"
                        style={{
                            background: isSelected ? "var(--ink)" : "transparent",
                            color: isSelected ? "var(--bg)" : "var(--ink-2)",
                        }}
                    >
                        {TEAM_WINDOW_LABELS[windowDays]}
                    </button>
                );
            })}
        </div>
    );
}
