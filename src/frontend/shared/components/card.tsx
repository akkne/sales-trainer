"use client";

import { HTMLAttributes, forwardRef, ReactNode } from "react";

export type CardVariant =
    | "surface"
    | "surface-low"
    | "surface-high"
    | "primary"
    | "primary-container"
    | "secondary-container"
    | "tertiary-container"
    | "error-container";

export type CardSize = "sm" | "md" | "lg";

interface CardProps extends HTMLAttributes<HTMLDivElement> {
    /** Background variant */
    variant?: CardVariant;
    /** Padding size */
    size?: CardSize;
    /** Clickable/interactive card */
    interactive?: boolean;
    /** Children */
    children: ReactNode;
}

const VARIANT_CLASSES: Record<CardVariant, string> = {
    "surface": "bg-surface-container",
    "surface-low": "bg-surface-container-low",
    "surface-high": "bg-surface-container-high",
    "primary": "bg-primary text-on-primary",
    "primary-container": "bg-primary-container text-on-primary-container",
    "secondary-container": "bg-secondary-container text-on-secondary-container",
    "tertiary-container": "bg-tertiary-container text-on-tertiary-container",
    "error-container": "bg-error-container text-on-error-container",
};

const SIZE_CLASSES: Record<CardSize, string> = {
    sm: "p-4 rounded-xl",
    md: "p-6 rounded-2xl",
    lg: "p-8 rounded-2xl",
};

/**
 * Card component with design system styling.
 *
 * Design principle: NO borders — use background color shifts only.
 *
 * @example
 * <Card>Basic card content</Card>
 * <Card variant="primary-container" size="lg">Highlighted card</Card>
 * <Card interactive onClick={() => {}}>Clickable card</Card>
 */
export const Card = forwardRef<HTMLDivElement, CardProps>(
    (
        {
            variant = "surface",
            size = "md",
            interactive = false,
            className = "",
            children,
            ...props
        },
        ref
    ) => {
        return (
            <div
                ref={ref}
                className={`
                    ${VARIANT_CLASSES[variant]}
                    ${SIZE_CLASSES[size]}
                    ${interactive
                        ? "cursor-pointer hover:shadow-md hover:scale-[1.01] active:scale-[0.99] tonal-transition"
                        : ""
                    }
                    ${className}
                `.trim()}
                {...props}
            >
                {children}
            </div>
        );
    }
);

Card.displayName = "Card";

/**
 * Card Header component
 */
interface CardHeaderProps extends HTMLAttributes<HTMLDivElement> {
    /** Title text */
    title: string;
    /** Subtitle/description text */
    subtitle?: string;
    /** Right side action element */
    action?: ReactNode;
    /** Icon element */
    icon?: ReactNode;
}

export function CardHeader({
    title,
    subtitle,
    action,
    icon,
    className = "",
    ...props
}: CardHeaderProps) {
    return (
        <div className={`flex items-start gap-3 ${className}`} {...props}>
            {icon && (
                <div className="shrink-0">{icon}</div>
            )}
            <div className="flex-1 min-w-0">
                <h3 className="font-headline font-semibold text-lg truncate">{title}</h3>
                {subtitle && (
                    <p className="text-sm text-on-surface-variant mt-0.5">{subtitle}</p>
                )}
            </div>
            {action && (
                <div className="shrink-0">{action}</div>
            )}
        </div>
    );
}

/**
 * Card Content area
 */
interface CardContentProps extends HTMLAttributes<HTMLDivElement> {
    children: ReactNode;
}

export function CardContent({
    className = "",
    children,
    ...props
}: CardContentProps) {
    return (
        <div className={`mt-4 ${className}`} {...props}>
            {children}
        </div>
    );
}

/**
 * Card Footer with actions
 */
interface CardFooterProps extends HTMLAttributes<HTMLDivElement> {
    children: ReactNode;
}

export function CardFooter({
    className = "",
    children,
    ...props
}: CardFooterProps) {
    return (
        <div className={`mt-4 flex items-center gap-3 ${className}`} {...props}>
            {children}
        </div>
    );
}

/**
 * Stat Card for displaying metrics
 */
interface StatCardProps {
    /** Stat label */
    label: string;
    /** Stat value */
    value: string | number;
    /** Icon element */
    icon?: ReactNode;
    /** Card background variant */
    variant?: CardVariant;
    /** Trend indicator */
    trend?: "up" | "down" | "neutral";
    /** Trend value text */
    trendValue?: string;
    /** Additional CSS classes */
    className?: string;
}

export function StatCard({
    label,
    value,
    icon,
    variant = "surface",
    trend,
    trendValue,
    className = "",
}: StatCardProps) {
    const trendColors = {
        up: "text-primary",
        down: "text-error",
        neutral: "text-on-surface-variant",
    };

    return (
        <Card variant={variant} size="sm" className={className}>
            <div className="flex items-center gap-3">
                {icon && (
                    <div className="shrink-0">{icon}</div>
                )}
                <div className="flex-1 min-w-0">
                    <p className="text-xs text-on-surface-variant uppercase tracking-wide">{label}</p>
                    <div className="flex items-baseline gap-2 mt-1">
                        <span className="font-headline font-bold text-2xl">{value}</span>
                        {trend && trendValue && (
                            <span className={`text-xs font-medium ${trendColors[trend]}`}>
                                {trend === "up" ? "↑" : trend === "down" ? "↓" : ""} {trendValue}
                            </span>
                        )}
                    </div>
                </div>
            </div>
        </Card>
    );
}

/**
 * Skeleton Card for loading states
 */
interface CardSkeletonProps {
    /** Size preset */
    size?: CardSize;
    /** Number of content lines */
    lines?: number;
    /** Show header */
    showHeader?: boolean;
    /** Additional CSS classes */
    className?: string;
}

export function CardSkeleton({
    size = "md",
    lines = 3,
    showHeader = true,
    className = "",
}: CardSkeletonProps) {
    return (
        <Card variant="surface" size={size} className={`animate-pulse ${className}`}>
            {showHeader && (
                <div className="flex items-center gap-3 mb-4">
                    <div className="w-10 h-10 rounded-full bg-surface-container-high" />
                    <div className="flex-1 space-y-2">
                        <div className="h-4 bg-surface-container-high rounded w-3/4" />
                        <div className="h-3 bg-surface-container-high rounded w-1/2" />
                    </div>
                </div>
            )}
            <div className="space-y-2">
                {Array.from({ length: lines }).map((_, i) => (
                    <div
                        key={i}
                        className="h-3 bg-surface-container-high rounded"
                        style={{ width: `${100 - i * 15}%` }}
                    />
                ))}
            </div>
        </Card>
    );
}
