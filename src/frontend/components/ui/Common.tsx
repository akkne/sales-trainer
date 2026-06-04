"use client";

import { HTMLAttributes, forwardRef, ReactNode } from "react";

/**
 * Badge component for status indicators, counts, and labels
 */
export type BadgeVariant =
    | "primary"
    | "secondary"
    | "tertiary"
    | "error"
    | "neutral"
    | "success";

export type BadgeSize = "sm" | "md" | "lg";

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
    /** Visual variant */
    variant?: BadgeVariant;
    /** Size preset */
    size?: BadgeSize;
    /** Pill shape (full rounded) */
    pill?: boolean;
    /** Children */
    children: ReactNode;
}

const VARIANT_CLASSES: Record<BadgeVariant, string> = {
    primary: "bg-ink text-bg",
    secondary: "bg-olive-soft text-olive",
    tertiary: "bg-accent-soft text-accent-ink",
    error: "bg-bad-soft text-bad",
    neutral: "bg-surface-2 text-ink",
    success: "bg-indigo-soft text-indigo-ink",
};

const SIZE_CLASSES: Record<BadgeSize, string> = {
    sm: "px-2 py-0.5 text-xs",
    md: "px-2.5 py-1 text-xs",
    lg: "px-3 py-1.5 text-sm",
};

/**
 * Badge component for inline labels and indicators.
 *
 * @example
 * <Badge>Default</Badge>
 * <Badge variant="primary" pill>Popular</Badge>
 * <Badge variant="error" size="sm">3</Badge>
 */
export const Badge = forwardRef<HTMLSpanElement, BadgeProps>(
    (
        {
            variant = "neutral",
            size = "md",
            pill = false,
            className = "",
            children,
            ...props
        },
        ref
    ) => {
        return (
            <span
                ref={ref}
                className={`
                    inline-flex items-center justify-center font-semibold
                    ${VARIANT_CLASSES[variant]}
                    ${SIZE_CLASSES[size]}
                    ${pill ? "rounded-full" : "rounded-lg"}
                    ${className}
                `.trim()}
                {...props}
            >
                {children}
            </span>
        );
    }
);

Badge.displayName = "Badge";

/**
 * Status Badge with icon support
 */
interface StatusBadgeProps extends Omit<BadgeProps, "children"> {
    /** Status text */
    label: string;
    /** Optional icon element */
    icon?: ReactNode;
}

export function StatusBadge({
    label,
    icon,
    ...props
}: StatusBadgeProps) {
    return (
        <Badge {...props}>
            {icon && <span className="mr-1">{icon}</span>}
            {label}
        </Badge>
    );
}

/**
 * Notification dot indicator
 */
interface NotificationDotProps {
    /** Show the dot */
    show?: boolean;
    /** Dot color variant */
    variant?: "primary" | "error" | "warning";
    /** Position relative to parent */
    position?: "top-right" | "top-left" | "bottom-right" | "bottom-left";
    /** Additional CSS classes */
    className?: string;
}

export function NotificationDot({
    show = true,
    variant = "error",
    position = "top-right",
    className = "",
}: NotificationDotProps) {
    if (!show) return null;

    const variantColors = {
        primary: "bg-ink",
        error: "bg-bad",
        warning: "bg-gold",
    };

    const positionClasses = {
        "top-right": "-top-1 -right-1",
        "top-left": "-top-1 -left-1",
        "bottom-right": "-bottom-1 -right-1",
        "bottom-left": "-bottom-1 -left-1",
    };

    return (
        <span
            className={`
                absolute w-2.5 h-2.5 rounded-full ring-2 ring-line
                ${variantColors[variant]}
                ${positionClasses[position]}
                ${className}
            `}
        />
    );
}

/**
 * Avatar component with initials fallback
 */
interface AvatarProps extends HTMLAttributes<HTMLDivElement> {
    /** Image source URL */
    src?: string | null;
    /** Alt text for image */
    alt?: string;
    /** Fallback initials when no image */
    initials?: string;
    /** Size preset */
    size?: "sm" | "md" | "lg" | "xl";
    /** Show ring around avatar */
    ring?: boolean;
    /** Ring color variant */
    ringVariant?: "primary" | "secondary" | "surface";
}

const AVATAR_SIZE_CLASSES = {
    sm: "w-8 h-8 text-xs",
    md: "w-10 h-10 text-sm",
    lg: "w-14 h-14 text-lg",
    xl: "w-20 h-20 text-xl",
};

const RING_CLASSES = {
    primary: "ring-4 ring-indigo-soft",
    secondary: "ring-4 ring-olive-soft",
    surface: "ring-4 ring-line",
};

export function Avatar({
    src,
    alt = "",
    initials,
    size = "md",
    ring = false,
    ringVariant = "primary",
    className = "",
    ...props
}: AvatarProps) {
    const hasImage = !!src;

    return (
        <div
            className={`
                relative rounded-full flex items-center justify-center font-bold
                bg-ink text-bg overflow-hidden
                ${AVATAR_SIZE_CLASSES[size]}
                ${ring ? RING_CLASSES[ringVariant] : ""}
                ${className}
            `}
            {...props}
        >
            {hasImage ? (
                <img
                    src={src}
                    alt={alt}
                    className="w-full h-full object-cover"
                />
            ) : (
                <span>{initials || alt?.charAt(0)?.toUpperCase() || "?"}</span>
            )}
        </div>
    );
}

/**
 * Avatar group for showing multiple avatars stacked
 */
interface AvatarGroupProps {
    /** Array of avatar props */
    avatars: Array<{ src?: string; alt?: string; initials?: string }>;
    /** Maximum avatars to show before +N indicator */
    max?: number;
    /** Size preset */
    size?: "sm" | "md" | "lg";
    /** Additional CSS classes */
    className?: string;
}

export function AvatarGroup({
    avatars,
    max = 4,
    size = "md",
    className = "",
}: AvatarGroupProps) {
    const visibleAvatars = avatars.slice(0, max);
    const remaining = avatars.length - max;

    return (
        <div className={`flex -space-x-2 ${className}`}>
            {visibleAvatars.map((avatar, index) => (
                <Avatar
                    key={index}
                    {...avatar}
                    size={size}
                    className="ring-2 ring-line"
                />
            ))}
            {remaining > 0 && (
                <div
                    className={`
                        flex items-center justify-center rounded-full
                        bg-surface-2 text-ink font-semibold
                        ring-2 ring-line
                        ${AVATAR_SIZE_CLASSES[size]}
                    `}
                >
                    +{remaining}
                </div>
            )}
        </div>
    );
}

/**
 * Divider component for visual separation
 */
interface DividerProps {
    /** Orientation */
    orientation?: "horizontal" | "vertical";
    /** Additional CSS classes */
    className?: string;
}

export function Divider({
    orientation = "horizontal",
    className = "",
}: DividerProps) {
    return (
        <div
            className={`
                bg-bg-2
                ${orientation === "horizontal" ? "h-px w-full" : "w-px h-full"}
                ${className}
            `}
            role="separator"
        />
    );
}

/**
 * Chip component for filter pills and tags
 */
interface ChipProps extends HTMLAttributes<HTMLButtonElement> {
    /** Selected/active state */
    selected?: boolean;
    /** Icon element */
    icon?: ReactNode;
    /** Disabled state */
    disabled?: boolean;
    /** Children */
    children: ReactNode;
}

export function Chip({
    selected = false,
    icon,
    disabled = false,
    className = "",
    children,
    ...props
}: ChipProps) {
    return (
        <button
            type="button"
            disabled={disabled}
            className={`
                inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full
                text-sm font-medium transition-colors
                disabled:opacity-60 disabled:cursor-not-allowed
                ${selected
                    ? "bg-ink text-bg"
                    : "bg-bg-2 text-ink-3 hover:bg-surface-2"
                }
                ${className}
            `}
            {...props}
        >
            {icon}
            {children}
        </button>
    );
}
