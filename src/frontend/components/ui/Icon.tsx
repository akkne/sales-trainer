"use client";

import { CSSProperties, SVGProps } from "react";

export type IconSize = "xs" | "sm" | "md" | "lg" | "xl" | "2xl";

interface IconProps {
    name: IconName;
    size?: IconSize | number;
    color?: string;
    className?: string;
    style?: CSSProperties;
    "aria-label"?: string;
    "aria-hidden"?: boolean;
}

const SIZE_MAP: Record<IconSize, number> = {
    xs: 12,
    sm: 16,
    md: 18,
    lg: 20,
    xl: 24,
    "2xl": 32,
};

type IconName =
    | "flame"
    | "bolt"
    | "trophy"
    | "target"
    | "heart"
    | "book"
    | "message"
    | "users"
    | "user"
    | "bell"
    | "mic"
    | "close"
    | "check"
    | "lock"
    | "chevron-right"
    | "chevron-left"
    | "chevron-down"
    | "chevron-up"
    | "arrow-right"
    | "arrow-up"
    | "arrow-left"
    | "search"
    | "plus"
    | "sparkle"
    | "play"
    | "phone"
    | "settings"
    | "moon"
    | "sun"
    | "grid"
    | "layers"
    | "compass"
    | "folder"
    | "clock"
    | "zap"
    | "star"
    | "link"
    | "edit"
    | "delete"
    | "info"
    | "warning"
    | "send";

export type { IconName };

export function Icon({
    name,
    size = "lg",
    color = "currentColor",
    className = "",
    style,
    "aria-label": ariaLabel,
    "aria-hidden": ariaHidden = !ariaLabel,
}: IconProps) {
    const sizeValue = typeof size === "number" ? size : SIZE_MAP[size];

    const svgProps: SVGProps<SVGSVGElement> = {
        width: sizeValue,
        height: sizeValue,
        viewBox: "0 0 24 24",
        fill: "none",
        stroke: color,
        strokeWidth: 1.5,
        strokeLinecap: "round",
        strokeLinejoin: "round",
        className,
        style,
        "aria-label": ariaLabel,
        "aria-hidden": ariaHidden,
        role: ariaLabel ? "img" : undefined,
    };

    switch (name) {
        case "flame":
            return (
                <svg {...svgProps}>
                    <path d="M12 3c2 3 4 5 4 8a4 4 0 0 1-8 0c0-1 .5-2 1-3 0 2 1 3 2 3 0-3-1-5 1-8z" />
                    <path d="M8 16a4 4 0 0 0 8 0" />
                </svg>
            );
        case "bolt":
            return (
                <svg {...svgProps}>
                    <path d="M13 3L5 14h6l-1 7 8-11h-6l1-7z" />
                </svg>
            );
        case "trophy":
            return (
                <svg {...svgProps}>
                    <path d="M8 21h8M12 17v4M7 4h10v4a5 5 0 0 1-10 0V4z" />
                    <path d="M17 5h3v2a3 3 0 0 1-3 3M7 5H4v2a3 3 0 0 0 3 3" />
                </svg>
            );
        case "target":
            return (
                <svg {...svgProps}>
                    <circle cx="12" cy="12" r="9" />
                    <circle cx="12" cy="12" r="5" />
                    <circle cx="12" cy="12" r="1.5" fill={color} stroke="none" />
                </svg>
            );
        case "heart":
            return (
                <svg {...svgProps}>
                    <path d="M12 20s-7-4.5-7-10a4 4 0 0 1 7-2 4 4 0 0 1 7 2c0 5.5-7 10-7 10z" />
                </svg>
            );
        case "book":
            return (
                <svg {...svgProps}>
                    <path d="M4 5a2 2 0 0 1 2-2h12v16H6a2 2 0 0 0-2 2V5z" />
                    <path d="M4 19a2 2 0 0 0 2 2h12" />
                </svg>
            );
        case "message":
            return (
                <svg {...svgProps}>
                    <path d="M21 12a8 8 0 1 1-3-6.2L21 5l-.8 3.2A8 8 0 0 1 21 12z" />
                </svg>
            );
        case "users":
            return (
                <svg {...svgProps}>
                    <circle cx="9" cy="8" r="4" />
                    <path d="M2 21a7 7 0 0 1 14 0" />
                    <circle cx="17" cy="7" r="3" />
                    <path d="M22 18a5 5 0 0 0-7-4.6" />
                </svg>
            );
        case "user":
            return (
                <svg {...svgProps}>
                    <circle cx="12" cy="8" r="4" />
                    <path d="M4 21a8 8 0 0 1 16 0" />
                </svg>
            );
        case "bell":
            return (
                <svg {...svgProps}>
                    <path d="M6 8a6 6 0 0 1 12 0c0 7 3 7 3 9H3c0-2 3-2 3-9z" />
                    <path d="M10 21a2 2 0 0 0 4 0" />
                </svg>
            );
        case "mic":
            return (
                <svg {...svgProps}>
                    <rect x="9" y="3" width="6" height="12" rx="3" />
                    <path d="M5 11a7 7 0 0 0 14 0M12 18v3" />
                </svg>
            );
        case "close":
            return (
                <svg {...svgProps}>
                    <path d="M6 6l12 12M6 18L18 6" />
                </svg>
            );
        case "check":
            return (
                <svg {...svgProps}>
                    <path d="M4 12l5 5L20 6" />
                </svg>
            );
        case "lock":
            return (
                <svg {...svgProps}>
                    <rect x="4" y="10" width="16" height="11" rx="2" />
                    <path d="M8 10V7a4 4 0 0 1 8 0v3" />
                </svg>
            );
        case "chevron-right":
            return (
                <svg {...svgProps}>
                    <path d="M9 5l7 7-7 7" />
                </svg>
            );
        case "chevron-left":
            return (
                <svg {...svgProps}>
                    <path d="M15 5l-7 7 7 7" />
                </svg>
            );
        case "chevron-down":
            return (
                <svg {...svgProps}>
                    <path d="M5 9l7 7 7-7" />
                </svg>
            );
        case "chevron-up":
            return (
                <svg {...svgProps}>
                    <path d="M5 15l7-7 7 7" />
                </svg>
            );
        case "arrow-right":
            return (
                <svg {...svgProps}>
                    <path d="M4 12h16M14 6l6 6-6 6" />
                </svg>
            );
        case "arrow-left":
            return (
                <svg {...svgProps}>
                    <path d="M20 12H4M10 6l-6 6 6 6" />
                </svg>
            );
        case "arrow-up":
            return (
                <svg {...svgProps}>
                    <path d="M12 20V4M6 10l6-6 6 6" />
                </svg>
            );
        case "search":
            return (
                <svg {...svgProps}>
                    <circle cx="11" cy="11" r="7" />
                    <path d="M21 21l-4-4" />
                </svg>
            );
        case "plus":
            return (
                <svg {...svgProps}>
                    <path d="M12 4v16M4 12h16" />
                </svg>
            );
        case "sparkle":
            return (
                <svg {...svgProps}>
                    <path d="M12 3l2 6 6 2-6 2-2 6-2-6-6-2 6-2 2-6zM19 14l1 2 2 1-2 1-1 2-1-2-2-1 2-1 1-2z" />
                </svg>
            );
        case "play":
            return (
                <svg {...svgProps}>
                    <path d="M6 4v16l14-8z" />
                </svg>
            );
        case "phone":
            return (
                <svg {...svgProps}>
                    <path d="M22 16v3a2 2 0 0 1-2 2A18 18 0 0 1 3 4a2 2 0 0 1 2-2h3l2 5-3 2a14 14 0 0 0 7 7l2-3 5 2z" />
                </svg>
            );
        case "settings":
            return (
                <svg {...svgProps}>
                    <circle cx="12" cy="12" r="3" />
                    <path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 0 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 0 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3h0a1.7 1.7 0 0 0 1-1.5V3a2 2 0 0 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8v0a1.7 1.7 0 0 0 1.5 1H21a2 2 0 0 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z" />
                </svg>
            );
        case "moon":
            return (
                <svg {...svgProps}>
                    <path d="M21 13A9 9 0 1 1 11 3a7 7 0 0 0 10 10z" />
                </svg>
            );
        case "sun":
            return (
                <svg {...svgProps}>
                    <circle cx="12" cy="12" r="4" />
                    <path d="M12 2v2M12 20v2M4 12H2M22 12h-2M5 5l1.5 1.5M17.5 17.5L19 19M5 19l1.5-1.5M17.5 6.5L19 5" />
                </svg>
            );
        case "grid":
            return (
                <svg {...svgProps}>
                    <rect x="3" y="3" width="7" height="7" rx="1" />
                    <rect x="14" y="3" width="7" height="7" rx="1" />
                    <rect x="3" y="14" width="7" height="7" rx="1" />
                    <rect x="14" y="14" width="7" height="7" rx="1" />
                </svg>
            );
        case "layers":
            return (
                <svg {...svgProps}>
                    <path d="M12 3l9 5-9 5-9-5 9-5zM3 13l9 5 9-5M3 18l9 5 9-5" />
                </svg>
            );
        case "compass":
            return (
                <svg {...svgProps}>
                    <circle cx="12" cy="12" r="9" />
                    <path d="M15 9l-2 6-6 2 2-6 6-2z" />
                </svg>
            );
        case "folder":
            return (
                <svg {...svgProps}>
                    <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z" />
                </svg>
            );
        case "clock":
            return (
                <svg {...svgProps}>
                    <circle cx="12" cy="12" r="9" />
                    <path d="M12 7v5l3 2" />
                </svg>
            );
        case "zap":
            return (
                <svg {...svgProps} fill={color} stroke="none">
                    <path d="M13 2L4 14h7l-1 8 9-12h-7l1-8z" />
                </svg>
            );
        case "star":
            return (
                <svg {...svgProps}>
                    <path d="M12 2l3 6 7 1-5 5 1 7-6-3-6 3 1-7-5-5 7-1 3-6z" />
                </svg>
            );
        case "link":
            return (
                <svg {...svgProps}>
                    <path d="M10 13a5 5 0 0 0 7.5 0l3-3a5 5 0 0 0-7-7l-2 2" />
                    <path d="M14 11a5 5 0 0 0-7.5 0l-3 3a5 5 0 0 0 7 7l2-2" />
                </svg>
            );
        case "edit":
            return (
                <svg {...svgProps}>
                    <path d="M17 3l4 4-11 11H6v-4L17 3z" />
                    <path d="M14 6l4 4" />
                </svg>
            );
        case "delete":
            return (
                <svg {...svgProps}>
                    <path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" />
                    <path d="M10 11v6M14 11v6" />
                </svg>
            );
        case "info":
            return (
                <svg {...svgProps}>
                    <circle cx="12" cy="12" r="9" />
                    <path d="M12 8v0M12 12v4" />
                </svg>
            );
        case "warning":
            return (
                <svg {...svgProps}>
                    <path d="M10.3 2.3a2 2 0 0 1 3.4 0l8.3 14.4a2 2 0 0 1-1.7 3H3.7a2 2 0 0 1-1.7-3l8.3-14.4z" />
                    <path d="M12 8v4M12 16v0" />
                </svg>
            );
        case "send":
            return (
                <svg {...svgProps}>
                    <path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z" />
                </svg>
            );
        default:
            return null;
    }
}

export const ICON_NAMES = {
    flame: "flame",
    bolt: "bolt",
    trophy: "trophy",
    target: "target",
    heart: "heart",
    book: "book",
    message: "message",
    users: "users",
    user: "user",
    bell: "bell",
    mic: "mic",
    close: "close",
    check: "check",
    lock: "lock",
    chevronRight: "chevron-right",
    chevronLeft: "chevron-left",
    chevronDown: "chevron-down",
    chevronUp: "chevron-up",
    arrowRight: "arrow-right",
    arrowLeft: "arrow-left",
    arrowUp: "arrow-up",
    search: "search",
    plus: "plus",
    sparkle: "sparkle",
    play: "play",
    phone: "phone",
    settings: "settings",
    moon: "moon",
    sun: "sun",
    grid: "grid",
    layers: "layers",
    compass: "compass",
    folder: "folder",
    clock: "clock",
    zap: "zap",
    star: "star",
    link: "link",
    edit: "edit",
    delete: "delete",
    info: "info",
    warning: "warning",
    send: "send",
} as const;
