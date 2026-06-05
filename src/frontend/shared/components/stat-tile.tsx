"use client";

import { ReactNode } from "react";

export type StatTileTone = "neutral" | "rust" | "olive" | "indigo";

interface StatTileProps {
    label: string;
    value: string | number;
    unit?: string;
    icon?: ReactNode;
    tone?: StatTileTone;
    big?: boolean;
    className?: string;
}

const TONE_STYLES: Record<StatTileTone, { color: string; bg: string }> = {
    neutral: { color: "var(--ink)", bg: "var(--surface)" },
    rust: { color: "var(--rust)", bg: "var(--rust-soft)" },
    olive: { color: "var(--olive)", bg: "var(--olive-soft)" },
    indigo: { color: "var(--indigo)", bg: "var(--indigo-soft)" },
};

export function StatTile({
    label,
    value,
    unit,
    icon,
    tone = "neutral",
    big = false,
    className = "",
}: StatTileProps) {
    const t = TONE_STYLES[tone];

    return (
        <div
            className={className}
            style={{
                background: t.bg,
                border: "1px solid var(--line)",
                borderRadius: "var(--r-md)",
                padding: big ? "20px" : "14px",
                display: "flex",
                flexDirection: "column",
                gap: "6px",
            }}
        >
            <div
                style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "6px",
                    fontSize: "11px",
                    color: "var(--ink-3)",
                    textTransform: "uppercase",
                    letterSpacing: "0.5px",
                    fontWeight: 500,
                }}
            >
                {icon}
                {label}
            </div>
            <div
                style={{
                    display: "flex",
                    alignItems: "baseline",
                    gap: "4px",
                    color: t.color,
                    fontFamily: "var(--f-sans)",
                    fontWeight: 500,
                    fontSize: big ? "32px" : "22px",
                    letterSpacing: "-0.5px",
                }}
            >
                <span className="tnum">{value}</span>
                {unit && (
                    <span style={{ fontSize: big ? "14px" : "12px", color: "var(--ink-3)" }}>
                        {unit}
                    </span>
                )}
            </div>
        </div>
    );
}
