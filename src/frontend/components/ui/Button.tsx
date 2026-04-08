"use client";

import { ButtonHTMLAttributes, forwardRef, ReactNode } from "react";
import { Icon, IconName } from "./Icon";

export type ButtonVariant = "primary" | "secondary" | "tertiary" | "ghost" | "error";
export type ButtonSize = "sm" | "md" | "lg";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
    /** Visual variant */
    variant?: ButtonVariant;
    /** Size preset */
    size?: ButtonSize;
    /** Icon name to show before text */
    iconLeft?: IconName;
    /** Icon name to show after text */
    iconRight?: IconName;
    /** Show loading spinner */
    loading?: boolean;
    /** Full width button */
    fullWidth?: boolean;
    /** Children */
    children?: ReactNode;
}

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
    primary:
        "bg-primary text-on-primary shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 hover:bg-primary-dim",
    secondary:
        "bg-secondary-container text-on-secondary-container hover:bg-secondary hover:text-on-secondary",
    tertiary:
        "bg-surface-container-high text-on-surface hover:bg-surface-container-highest",
    ghost:
        "bg-transparent text-primary hover:bg-primary-container hover:text-on-primary-container",
    error:
        "bg-error text-on-error shadow-[0_4px_0_var(--color-red-shadow)] active:shadow-none active:translate-y-1 hover:opacity-90",
};

const SIZE_CLASSES: Record<ButtonSize, string> = {
    sm: "px-4 py-2 text-sm gap-1.5",
    md: "px-5 py-3 text-base gap-2",
    lg: "px-6 py-4 text-base gap-2 font-bold",
};

const ICON_SIZE_MAP: Record<ButtonSize, "sm" | "md" | "lg"> = {
    sm: "sm",
    md: "md",
    lg: "md",
};

/**
 * Standardized Button component with design system variants.
 *
 * Variants:
 * - primary: Main CTA, rounded-full, deep green with 3D shadow
 * - secondary: Secondary actions, teal container background
 * - tertiary: Neutral surface background
 * - ghost: Transparent with primary text, no background
 * - error: Destructive actions, red with 3D shadow
 *
 * @example
 * <Button variant="primary" size="lg">Continue</Button>
 * <Button variant="ghost" iconLeft="arrow_back">Back</Button>
 * <Button variant="primary" iconRight="arrow_forward" loading>Loading</Button>
 */
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
    (
        {
            variant = "primary",
            size = "md",
            iconLeft,
            iconRight,
            loading = false,
            fullWidth = false,
            disabled,
            className = "",
            children,
            ...props
        },
        ref
    ) => {
        const isDisabled = disabled || loading;
        const iconSize = ICON_SIZE_MAP[size];

        return (
            <button
                ref={ref}
                disabled={isDisabled}
                className={`
                    inline-flex items-center justify-center rounded-full font-semibold
                    tonal-transition select-none
                    disabled:opacity-60 disabled:cursor-not-allowed disabled:shadow-none disabled:translate-y-0
                    ${VARIANT_CLASSES[variant]}
                    ${SIZE_CLASSES[size]}
                    ${fullWidth ? "w-full" : ""}
                    ${className}
                `.trim()}
                {...props}
            >
                {loading ? (
                    <span className="w-5 h-5 border-2 border-current border-t-transparent rounded-full animate-spin" />
                ) : (
                    <>
                        {iconLeft && <Icon name={iconLeft} size={iconSize} />}
                        {children}
                        {iconRight && <Icon name={iconRight} size={iconSize} />}
                    </>
                )}
            </button>
        );
    }
);

Button.displayName = "Button";

/**
 * Icon-only button variant for compact actions.
 *
 * @example
 * <IconButton icon="close" aria-label="Close" />
 * <IconButton icon="settings" variant="ghost" size="lg" />
 */
interface IconButtonProps extends Omit<ButtonProps, "iconLeft" | "iconRight" | "children"> {
    /** Icon name */
    icon: IconName;
    /** Accessible label (required for icon-only buttons) */
    "aria-label": string;
}

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(
    ({ icon, size = "md", className = "", ...props }, ref) => {
        const sizeClasses: Record<ButtonSize, string> = {
            sm: "w-8 h-8",
            md: "w-10 h-10",
            lg: "w-12 h-12",
        };

        return (
            <Button
                ref={ref}
                size={size}
                className={`!p-0 ${sizeClasses[size]} ${className}`}
                {...props}
            >
                <Icon name={icon} size={ICON_SIZE_MAP[size]} />
            </Button>
        );
    }
);

IconButton.displayName = "IconButton";
