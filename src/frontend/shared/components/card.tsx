"use client";

import { HTMLAttributes, forwardRef, ReactNode, useState } from "react";

interface CardProps extends HTMLAttributes<HTMLDivElement> {
    padding?: number | string;
    hover?: boolean;
    children: ReactNode;
}

export const Card = forwardRef<HTMLDivElement, CardProps>(
    (
        {
            padding = 20,
            hover = false,
            className = "",
            style = {},
            children,
            onClick,
            ...props
        },
        ref
    ) => {
        const [isHovered, setIsHovered] = useState(false);

        const baseStyle: React.CSSProperties = {
            background: "var(--surface)",
            border: "1px solid var(--line)",
            borderRadius: "var(--r-lg)",
            padding: typeof padding === "number" ? `${padding}px` : padding,
            boxShadow: isHovered && hover ? "var(--sh-2)" : "var(--sh-1)",
            transform: isHovered && hover ? "translateY(-1px)" : "none",
            transition: "transform 0.15s ease, box-shadow 0.15s ease",
            cursor: onClick ? "pointer" : "default",
            ...style,
        };

        return (
            <div
                ref={ref}
                className={className}
                style={baseStyle}
                onClick={onClick}
                onMouseEnter={() => hover && setIsHovered(true)}
                onMouseLeave={() => hover && setIsHovered(false)}
                {...props}
            >
                {children}
            </div>
        );
    }
);

Card.displayName = "Card";

interface CardHeaderProps extends HTMLAttributes<HTMLDivElement> {
    title: string;
    subtitle?: string;
    action?: ReactNode;
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
        <div
            className={className}
            style={{ display: "flex", alignItems: "flex-start", gap: "12px" }}
            {...props}
        >
            {icon && <div style={{ flexShrink: 0 }}>{icon}</div>}
            <div style={{ flex: 1, minWidth: 0 }}>
                <h3 style={{ fontSize: "16px", fontWeight: 500, margin: 0 }}>{title}</h3>
                {subtitle && (
                    <p style={{ fontSize: "13px", color: "var(--ink-3)", marginTop: "2px" }}>
                        {subtitle}
                    </p>
                )}
            </div>
            {action && <div style={{ flexShrink: 0 }}>{action}</div>}
        </div>
    );
}

interface CardContentProps extends HTMLAttributes<HTMLDivElement> {
    children: ReactNode;
}

export function CardContent({ className = "", children, style, ...props }: CardContentProps) {
    return (
        <div className={className} style={{ marginTop: "16px", ...style }} {...props}>
            {children}
        </div>
    );
}

interface CardFooterProps extends HTMLAttributes<HTMLDivElement> {
    children: ReactNode;
}

export function CardFooter({ className = "", children, style, ...props }: CardFooterProps) {
    return (
        <div
            className={className}
            style={{ marginTop: "16px", display: "flex", alignItems: "center", gap: "12px", ...style }}
            {...props}
        >
            {children}
        </div>
    );
}

interface CardSkeletonProps {
    lines?: number;
    showHeader?: boolean;
    className?: string;
}

export function CardSkeleton({
    lines = 3,
    showHeader = true,
    className = "",
}: CardSkeletonProps) {
    return (
        <Card className={className} padding={24}>
            {showHeader && (
                <div style={{ display: "flex", alignItems: "center", gap: "12px", marginBottom: "16px" }}>
                    <div
                        style={{
                            width: 40,
                            height: 40,
                            borderRadius: "50%",
                            background: "var(--bg-2)",
                            animation: "pulse 2s ease-in-out infinite",
                        }}
                    />
                    <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: "8px" }}>
                        <div
                            style={{
                                height: 14,
                                background: "var(--bg-2)",
                                borderRadius: 6,
                                width: "75%",
                                animation: "pulse 2s ease-in-out infinite",
                            }}
                        />
                        <div
                            style={{
                                height: 12,
                                background: "var(--bg-2)",
                                borderRadius: 6,
                                width: "50%",
                                animation: "pulse 2s ease-in-out infinite",
                            }}
                        />
                    </div>
                </div>
            )}
            <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                {Array.from({ length: lines }).map((_, i) => (
                    <div
                        key={i}
                        style={{
                            height: 12,
                            background: "var(--bg-2)",
                            borderRadius: 6,
                            width: `${100 - i * 15}%`,
                            animation: "pulse 2s ease-in-out infinite",
                        }}
                    />
                ))}
            </div>
        </Card>
    );
}
