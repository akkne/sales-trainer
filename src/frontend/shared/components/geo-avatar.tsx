"use client";

import { CSSProperties, useMemo } from "react";

interface GeoAvatarProps {
    seed?: string;
    size?: number;
    palette?: string[];
    className?: string;
    style?: CSSProperties;
}

const DEFAULT_PALETTES = [
    ["var(--primary)", "var(--amber)", "var(--violet)"],
    ["var(--success)", "var(--primary)", "var(--amber)"],
    ["var(--flame)", "var(--amber)", "var(--violet)"],
    ["var(--violet)", "var(--success)", "var(--primary)"],
];

export function GeoAvatar({
    seed = "a",
    size = 40,
    palette,
    className = "",
    style = {},
}: GeoAvatarProps) {
    const { bg, fg, shape, accent } = useMemo(() => {
        const s = String(seed || "a");
        let h = 0;
        for (let i = 0; i < s.length; i++) {
            h = ((h << 5) - h + s.charCodeAt(i)) | 0;
        }

        const rnd = (n: number) => {
            h = (h * 9301 + 49297) % 233280;
            return (h / 233280) * n;
        };

        const palettes = palette ? [palette] : DEFAULT_PALETTES;
        const p = palettes[Math.floor(rnd(palettes.length))] || palettes[0];
        const shapeIdx = Math.floor(rnd(4));
        const bgColor = p[Math.floor(rnd(p.length))] || "var(--ink)";
        const fgColor = p[(Math.floor(rnd(p.length)) + 1) % p.length] || "var(--bg)";
        const accentPos = { x: 10 + rnd(20), y: 8 + rnd(10), r: 2 + rnd(3) };

        return { bg: bgColor, fg: fgColor, shape: shapeIdx, accent: accentPos };
    }, [seed, palette]);

    return (
        <div
            className={className}
            style={{
                width: size,
                height: size,
                borderRadius: size * 0.25,
                background: bg,
                position: "relative",
                overflow: "hidden",
                flexShrink: 0,
                ...style,
            }}
        >
            <svg viewBox="0 0 40 40" width={size} height={size} style={{ display: "block" }}>
                {shape === 0 && <circle cx="20" cy="24" r="12" fill={fg} opacity="0.85" />}
                {shape === 1 && <rect x="8" y="18" width="24" height="24" rx="3" fill={fg} opacity="0.85" />}
                {shape === 2 && <polygon points="20,8 34,32 6,32" fill={fg} opacity="0.85" />}
                {shape === 3 && <path d="M8 28 Q 20 6 32 28 Z" fill={fg} opacity="0.85" />}
                <circle cx={accent.x} cy={accent.y} r={accent.r} fill="var(--ink-2)" opacity="0.6" />
            </svg>
        </div>
    );
}
