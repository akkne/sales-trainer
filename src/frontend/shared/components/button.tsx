"use client";

import { ButtonHTMLAttributes, forwardRef, ReactNode } from "react";
import { Icon, IconName } from "./icon";

export type ButtonVariant = "primary" | "accent" | "secondary" | "ghost" | "outline" | "destructive";
export type ButtonSize = "sm" | "md" | "lg" | "xl";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
    variant?: ButtonVariant;
    size?: ButtonSize;
    icon?: ReactNode;
    iconRight?: ReactNode;
    iconLeft?: IconName;
    iconRightName?: IconName;
    loading?: boolean;
    fullWidth?: boolean;
    children?: ReactNode;
}

const SIZE_STYLES: Record<ButtonSize, { padding: string; fontSize: string; height: string; radius: string; iconSize: number }> = {
    sm: { padding: "6px 12px", fontSize: "13px", height: "30px", radius: "8px", iconSize: 14 },
    md: { padding: "10px 16px", fontSize: "14px", height: "38px", radius: "10px", iconSize: 16 },
    lg: { padding: "14px 22px", fontSize: "15px", height: "48px", radius: "12px", iconSize: 18 },
    xl: { padding: "18px 28px", fontSize: "16px", height: "56px", radius: "14px", iconSize: 20 },
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
    (
        {
            variant = "primary",
            size = "md",
            icon,
            iconRight,
            iconLeft,
            iconRightName,
            loading = false,
            fullWidth = false,
            disabled,
            className = "",
            children,
            style,
            ...props
        },
        ref
    ) => {
        const isDisabled = disabled || loading;
        const s = SIZE_STYLES[size];

        const variantStyles: Record<ButtonVariant, React.CSSProperties> = {
            primary: {
                background: "var(--primary)",
                color: "var(--on-primary)",
                boxShadow: "var(--sh-primary)",
            },
            accent: {
                background: "var(--ink)",
                color: "var(--bg)",
                boxShadow: "var(--sh-2)",
            },
            secondary: {
                background: "var(--primary-soft)",
                color: "var(--primary)",
            },
            ghost: {
                background: "transparent",
                color: "var(--ink-2)",
            },
            outline: {
                background: "transparent",
                color: "var(--ink)",
                border: "1px solid var(--line-2)",
            },
            destructive: {
                background: "var(--heart)",
                color: "white",
            },
        };

        const baseStyle: React.CSSProperties = {
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            gap: "8px",
            padding: s.padding,
            height: s.height,
            fontSize: s.fontSize,
            borderRadius: s.radius,
            border: "1px solid transparent",
            fontWeight: 700,
            letterSpacing: "-0.1px",
            cursor: isDisabled ? "not-allowed" : "pointer",
            transition: "transform 0.08s ease, background 0.15s ease, border-color 0.15s ease, opacity 0.15s ease",
            width: fullWidth ? "100%" : "auto",
            fontFamily: "var(--f-sans)",
            opacity: isDisabled ? 0.6 : 1,
            ...variantStyles[variant],
            ...style,
        };

        const handleMouseDown = (e: React.MouseEvent<HTMLButtonElement>) => {
            if (!isDisabled) {
                e.currentTarget.style.transform = "translateY(1px)";
            }
            props.onMouseDown?.(e);
        };

        const handleMouseUp = (e: React.MouseEvent<HTMLButtonElement>) => {
            e.currentTarget.style.transform = "translateY(0)";
            props.onMouseUp?.(e);
        };

        const handleMouseLeave = (e: React.MouseEvent<HTMLButtonElement>) => {
            e.currentTarget.style.transform = "translateY(0)";
            props.onMouseLeave?.(e);
        };

        return (
            <button
                ref={ref}
                disabled={isDisabled}
                style={baseStyle}
                className={className}
                onMouseDown={handleMouseDown}
                onMouseUp={handleMouseUp}
                onMouseLeave={handleMouseLeave}
                {...props}
            >
                {loading ? (
                    <span
                        style={{
                            width: s.iconSize,
                            height: s.iconSize,
                            border: "2px solid currentColor",
                            borderTopColor: "transparent",
                            borderRadius: "50%",
                            animation: "spin 0.8s linear infinite",
                        }}
                    />
                ) : (
                    <>
                        {icon}
                        {iconLeft && <Icon name={iconLeft} size={s.iconSize} />}
                        {children}
                        {iconRightName && <Icon name={iconRightName} size={s.iconSize} />}
                        {iconRight}
                    </>
                )}
            </button>
        );
    }
);

Button.displayName = "Button";

interface IconButtonProps extends Omit<ButtonProps, "icon" | "iconRight" | "iconLeft" | "iconRightName" | "children"> {
    icon: IconName;
    "aria-label": string;
}

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(
    ({ icon, size = "md", ...props }, ref) => {
        const sizeMap: Record<ButtonSize, number> = {
            sm: 30,
            md: 38,
            lg: 48,
            xl: 56,
        };
        const iconSizeMap: Record<ButtonSize, number> = {
            sm: 14,
            md: 16,
            lg: 18,
            xl: 20,
        };
        const dimension = sizeMap[size];

        return (
            <Button
                ref={ref}
                size={size}
                style={{ width: dimension, height: dimension, padding: 0 }}
                {...props}
            >
                <Icon name={icon} size={iconSizeMap[size]} />
            </Button>
        );
    }
);

IconButton.displayName = "IconButton";
