"use client";

import { Icon } from "./icon";

interface WordmarkProps {
    size?: number;
    color?: string;
    accentColor?: string;
    variant?: "full" | "mark";
}

export function Wordmark({
    size = 28,
    color,
    variant = "full",
}: WordmarkProps) {
    const colorValue = color || "var(--ink)";
    const markSize = Math.round(size * 1.2);

    const mark = (
        <span
            style={{
                display: "grid",
                placeItems: "center",
                width: markSize,
                height: markSize,
                background: "linear-gradient(135deg, var(--primary), var(--violet))",
                borderRadius: Math.round(markSize * 0.32),
                color: "#fff",
                boxShadow: "var(--sh-primary)",
                flex: "none",
            }}
        >
            <Icon name="bolt" size={Math.round(markSize * 0.6)} />
        </span>
    );

    if (variant === "mark") {
        return mark;
    }

    return (
        <span
            style={{
                display: "inline-flex",
                alignItems: "center",
                gap: size * 0.35,
                fontFamily: "var(--font-display)",
                fontWeight: 800,
                fontSize: size * 0.72,
                letterSpacing: "-0.02em",
                color: colorValue,
                lineHeight: 1,
            }}
        >
            {mark}
            <span>
                Sellevate<span style={{ color: "var(--primary)" }}>.</span>
            </span>
        </span>
    );
}
