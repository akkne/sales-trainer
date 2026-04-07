"use client";

import { CSSProperties } from "react";

export type IconVariant = "outlined" | "filled";
export type IconSize = "sm" | "md" | "lg" | "xl" | "2xl";

interface IconProps {
    /** Material Symbols icon name (e.g., "school", "trophy", "settings") */
    name: string;
    /** Icon variant - outlined or filled */
    variant?: IconVariant;
    /** Predefined size or custom className */
    size?: IconSize;
    /** Additional CSS classes */
    className?: string;
    /** Custom inline styles */
    style?: CSSProperties;
    /** Accessible label */
    "aria-label"?: string;
    /** Whether icon is decorative (hides from screen readers) */
    "aria-hidden"?: boolean;
}

const SIZE_CLASSES: Record<IconSize, string> = {
    sm: "text-[16px]",
    md: "text-[20px]",
    lg: "text-[24px]",
    xl: "text-[28px]",
    "2xl": "text-[32px]",
};

/**
 * Material Symbols icon component.
 * Uses the Material Symbols Outlined font loaded in layout.tsx.
 *
 * @example
 * <Icon name="school" />
 * <Icon name="check_circle" variant="filled" size="lg" className="text-primary" />
 */
export function Icon({
    name,
    variant = "outlined",
    size = "lg",
    className = "",
    style,
    "aria-label": ariaLabel,
    "aria-hidden": ariaHidden = !ariaLabel,
}: IconProps) {
    const sizeClass = SIZE_CLASSES[size];
    const variantClass = variant === "filled" ? "filled" : "";

    return (
        <span
            className={`material-symbols-outlined ${variantClass} ${sizeClass} ${className}`.trim()}
            style={style}
            aria-label={ariaLabel}
            aria-hidden={ariaHidden}
            role={ariaLabel ? "img" : undefined}
        >
            {name}
        </span>
    );
}

/**
 * Common icon names used throughout the app.
 * Reference: https://fonts.google.com/icons
 */
export const ICON_NAMES = {
    // Navigation
    school: "school",
    trophy: "trophy",
    menuBook: "menu_book",
    forum: "forum",
    person: "person",
    leaderboard: "leaderboard",
    settings: "settings",
    queryStats: "query_stats",

    // Actions
    arrowBack: "arrow_back",
    arrowForward: "arrow_forward",
    close: "close",
    check: "check",
    checkCircle: "check_circle",
    add: "add",
    edit: "edit",
    editNote: "edit_note",
    delete: "delete",
    search: "search",
    bolt: "bolt",

    // Status
    lock: "lock",
    lockOpen: "lock_open",
    verified: "verified",
    info: "info",
    warning: "warning",
    error: "error",
    schedule: "schedule",
    timer: "timer",

    // Notifications
    notifications: "notifications",
    emojiEvents: "emoji_events",
    militaryTech: "military_tech",

    // Content
    call: "call",
    handshake: "handshake",
    psychology: "psychology",
    layers: "layers",
    verifiedUser: "verified_user",
    assignmentTurnedIn: "assignment_turned_in",
    scheduleSend: "schedule_send",

    // Media
    mic: "mic",
    micOff: "mic_off",
    playCircle: "play_circle",
    stopCircle: "stop_circle",
    volumeUp: "volume_up",

    // Categories (onboarding)
    cloudDone: "cloud_done",
    homeWork: "home_work",
    shoppingBag: "shopping_bag",
    accountBalance: "account_balance",
    diversity3: "diversity_3",

    // Misc
    localFireDepartment: "local_fire_department",
    trendingUp: "trending_up",
    expandLess: "expand_less",
    expandMore: "expand_more",
    chevronRight: "chevron_right",
    link: "link",
    arrowOutward: "arrow_outward",
    star: "star",
    token: "token",
} as const;

export type IconName = (typeof ICON_NAMES)[keyof typeof ICON_NAMES];
