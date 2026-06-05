"use client";

import { ReactNode } from "react";

export type ChipTone = "neutral" | "rust" | "olive" | "indigo" | "good" | "bad" | "warn" | "ghost";
export type ChipSize = "sm" | "md";

interface ChipProps {
    children: ReactNode;
    tone?: ChipTone;
    size?: ChipSize;
    active?: boolean;
    onClick?: () => void;
    icon?: ReactNode;
    className?: string;
}

const TONE_STYLES: Record<ChipTone, { bg: string; color: string; border: string }> = {
    neutral: { bg: "var(--bg-2)", color: "var(--ink-2)", border: "var(--line)" },
    rust: { bg: "var(--rust-soft)", color: "var(--rust-ink)", border: "transparent" },
    olive: { bg: "var(--olive-soft)", color: "var(--olive)", border: "transparent" },
    indigo: { bg: "var(--indigo-soft)", color: "var(--indigo)", border: "transparent" },
    good: { bg: "var(--good-soft)", color: "var(--good)", border: "transparent" },
    bad: { bg: "var(--bad-soft)", color: "var(--bad)", border: "transparent" },
    warn: { bg: "var(--warn-soft)", color: "oklch(0.45 0.10 80)", border: "transparent" },
    ghost: { bg: "transparent", color: "var(--ink-3)", border: "var(--line-2)" },
};

export function Chip({
    children,
    tone = "neutral",
    size = "md",
    active = false,
    onClick,
    icon,
    className = "",
}: ChipProps) {
    const t = TONE_STYLES[tone];
    const sizeStyles = size === "sm"
        ? { padding: "2px 8px", fontSize: "11px", height: "20px" }
        : { padding: "4px 10px", fontSize: "12px", height: "24px" };

    const style: React.CSSProperties = {
        display: "inline-flex",
        alignItems: "center",
        gap: "6px",
        background: active ? "var(--ink)" : t.bg,
        color: active ? "var(--bg)" : t.color,
        border: `1px solid ${active ? "var(--ink)" : t.border}`,
        borderRadius: "999px",
        ...sizeStyles,
        fontWeight: 500,
        fontFamily: "var(--f-sans)",
        letterSpacing: 0,
        cursor: onClick ? "pointer" : "default",
        whiteSpace: "nowrap",
        transition: "all 0.15s ease",
    };

    return (
        <span onClick={onClick} style={style} className={className}>
            {icon}
            {children}
        </span>
    );
}
