"use client";

import { CSSProperties } from "react";

export type ProgressVariant = "primary" | "tertiary" | "secondary" | "error";
export type ProgressSize = "sm" | "md" | "lg";

interface ProgressBarProps {
    /** Progress value (0-100) */
    value: number;
    /** Maximum value (defaults to 100) */
    max?: number;
    /** Visual variant */
    variant?: ProgressVariant;
    /** Size preset */
    size?: ProgressSize;
    /** Show percentage label */
    showLabel?: boolean;
    /** Custom label text (overrides percentage) */
    label?: string;
    /** Animate the progress fill */
    animated?: boolean;
    /** Additional CSS classes */
    className?: string;
}

const VARIANT_CLASSES: Record<ProgressVariant, string> = {
    primary: "bg-primary",
    secondary: "bg-secondary",
    tertiary: "bg-tertiary",
    error: "bg-error",
};

const SIZE_CLASSES: Record<ProgressSize, string> = {
    sm: "h-1.5",
    md: "h-3",
    lg: "h-4",
};

/**
 * Progress bar component with design system styling.
 *
 * @example
 * <ProgressBar value={75} />
 * <ProgressBar value={50} variant="tertiary" showLabel />
 * <ProgressBar value={30} max={50} size="lg" animated />
 */
export function ProgressBar({
    value,
    max = 100,
    variant = "primary",
    size = "md",
    showLabel = false,
    label,
    animated = true,
    className = "",
}: ProgressBarProps) {
    const percentage = Math.min(100, Math.max(0, (value / max) * 100));
    const displayLabel = label ?? `${Math.round(percentage)}%`;

    return (
        <div className={`flex flex-col gap-1 ${className}`}>
            {showLabel && (
                <div className="flex justify-between text-xs text-on-surface-variant">
                    <span>{displayLabel}</span>
                </div>
            )}
            <div
                className={`
                    w-full rounded-full bg-surface-variant overflow-hidden
                    ${SIZE_CLASSES[size]}
                `}
                role="progressbar"
                aria-valuenow={value}
                aria-valuemin={0}
                aria-valuemax={max}
            >
                <div
                    className={`
                        h-full rounded-full
                        ${VARIANT_CLASSES[variant]}
                        ${animated ? "transition-all duration-500" : ""}
                    `}
                    style={{ width: `${percentage}%` }}
                />
            </div>
        </div>
    );
}

/**
 * Circular progress indicator
 */
interface CircularProgressProps {
    /** Progress value (0-100) */
    value: number;
    /** Maximum value (defaults to 100) */
    max?: number;
    /** Circle size in pixels */
    size?: number;
    /** Stroke width */
    strokeWidth?: number;
    /** Visual variant */
    variant?: ProgressVariant;
    /** Show percentage in center */
    showLabel?: boolean;
    /** Custom label (overrides percentage) */
    label?: string;
    /** Additional CSS classes */
    className?: string;
}

export function CircularProgress({
    value,
    max = 100,
    size = 48,
    strokeWidth = 4,
    variant = "primary",
    showLabel = false,
    label,
    className = "",
}: CircularProgressProps) {
    const percentage = Math.min(100, Math.max(0, (value / max) * 100));
    const radius = (size - strokeWidth) / 2;
    const circumference = radius * 2 * Math.PI;
    const strokeDashoffset = circumference - (percentage / 100) * circumference;
    const displayLabel = label ?? `${Math.round(percentage)}%`;

    const variantColors: Record<ProgressVariant, string> = {
        primary: "var(--color-primary)",
        secondary: "var(--color-secondary)",
        tertiary: "var(--color-tertiary)",
        error: "var(--color-error)",
    };

    return (
        <div
            className={`relative inline-flex items-center justify-center ${className}`}
            style={{ width: size, height: size }}
        >
            <svg
                className="transform -rotate-90"
                width={size}
                height={size}
                role="progressbar"
                aria-valuenow={value}
                aria-valuemin={0}
                aria-valuemax={max}
            >
                {/* Background circle */}
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    stroke="var(--color-surface-variant)"
                    strokeWidth={strokeWidth}
                />
                {/* Progress circle */}
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    stroke={variantColors[variant]}
                    strokeWidth={strokeWidth}
                    strokeLinecap="round"
                    strokeDasharray={circumference}
                    strokeDashoffset={strokeDashoffset}
                    className="transition-all duration-500"
                />
            </svg>
            {showLabel && (
                <span className="absolute text-xs font-semibold text-on-surface">
                    {displayLabel}
                </span>
            )}
        </div>
    );
}

/**
 * Step progress indicator for multi-step forms
 */
interface StepProgressProps {
    /** Current step (1-indexed) */
    currentStep: number;
    /** Total number of steps */
    totalSteps: number;
    /** Visual variant */
    variant?: ProgressVariant;
    /** Show step label */
    showLabel?: boolean;
    /** Additional CSS classes */
    className?: string;
}

export function StepProgress({
    currentStep,
    totalSteps,
    variant = "primary",
    showLabel = true,
    className = "",
}: StepProgressProps) {
    const progress = (currentStep / totalSteps) * 100;

    return (
        <div className={`flex flex-col gap-2 ${className}`}>
            {showLabel && (
                <p className="text-sm text-on-surface-variant">
                    Шаг {currentStep} из {totalSteps}
                </p>
            )}
            <ProgressBar value={progress} variant={variant} size="sm" animated />
        </div>
    );
}

/**
 * Skeleton progress bar for loading states
 */
interface ProgressSkeletonProps {
    /** Size preset */
    size?: ProgressSize;
    /** Additional CSS classes */
    className?: string;
}

export function ProgressSkeleton({
    size = "md",
    className = "",
}: ProgressSkeletonProps) {
    return (
        <div
            className={`
                w-full rounded-full bg-surface-container animate-pulse
                ${SIZE_CLASSES[size]}
                ${className}
            `}
        />
    );
}
