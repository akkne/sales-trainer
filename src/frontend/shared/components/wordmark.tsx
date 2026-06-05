"use client";

interface WordmarkProps {
    size?: number;
    color?: string;
    accentColor?: string;
    variant?: "full" | "mark";
}

export function Wordmark({
    size = 28,
    color,
    accentColor,
    variant = "full",
}: WordmarkProps) {
    const c = color || "var(--ink)";
    const a = accentColor || "var(--indigo)";
    const sq = Math.round(size * 0.22);

    if (variant === "mark") {
        return (
            <span
                style={{
                    display: "inline-block",
                    width: size,
                    height: size,
                    background: a,
                    borderRadius: 4,
                    position: "relative",
                }}
            >
                <span
                    style={{
                        position: "absolute",
                        inset: "22%",
                        background: "var(--bg)",
                        borderRadius: 2,
                    }}
                />
            </span>
        );
    }

    return (
        <span
            style={{
                display: "inline-flex",
                alignItems: "center",
                gap: size * 0.18,
                fontFamily: "var(--f-sans)",
                fontWeight: 500,
                fontSize: size,
                letterSpacing: `-${size * 0.03}px`,
                color: c,
                lineHeight: 1,
            }}
        >
            <span
                style={{
                    display: "inline-block",
                    width: sq,
                    height: sq,
                    background: a,
                    borderRadius: 3,
                }}
            />
            <span>sellevate</span>
        </span>
    );
}
