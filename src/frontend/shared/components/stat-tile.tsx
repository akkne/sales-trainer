"use client";

import { ReactNode } from "react";

export type StatTileTone =
    | "neutral"
    | "rust"
    | "olive"
    | "indigo"
    | "flame"
    | "primary"
    | "violet"
    | "success"
    | "amber";

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
    neutral: { color: "var(--ink)", bg: "var(--surface-2)" },
    // legacy tone names mapped to the new palette
    rust: { color: "var(--flame)", bg: "var(--flame-soft)" },
    olive: { color: "var(--success)", bg: "var(--success-soft)" },
    indigo: { color: "var(--primary)", bg: "var(--primary-soft)" },
    // new palette tones
    flame: { color: "var(--flame)", bg: "var(--flame-soft)" },
    primary: { color: "var(--primary)", bg: "var(--primary-soft)" },
    violet: { color: "var(--violet)", bg: "var(--violet-soft)" },
    success: { color: "var(--success)", bg: "var(--success-soft)" },
    amber: { color: "var(--amber)", bg: "var(--amber-soft)" },
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
        <div className={`stat ${className}`} style={big ? { padding: 20 } : undefined}>
            <div className="stat-head">
                {icon && (
                    <span
                        className="stat-ic"
                        style={{ background: t.bg, color: t.color }}
                    >
                        {icon}
                    </span>
                )}
                <span className="stat-label">{label}</span>
            </div>
            <div className="stat-num" style={{ fontSize: big ? 32 : 24 }}>
                <span className="tnum">{value}</span>
                {unit && <small>{unit}</small>}
            </div>
        </div>
    );
}
