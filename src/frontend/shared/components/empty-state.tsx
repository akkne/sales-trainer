"use client";

import { ReactNode } from "react";
import { Icon, type IconName } from "./icon";

interface EmptyStateProps {
    icon?: IconName;
    title: string;
    description?: string;
    action?: ReactNode;
    compact?: boolean;
    className?: string;
}

/**
 * Emptiness in the organization panel almost always explains the section rather than reporting a
 * zero — the РОП opening «Спорные оценки» for the first time needs to learn what would appear
 * there, not to be told that nothing has. One shape keeps that tone across nineteen screens.
 */
export function EmptyState({
    icon,
    title,
    description,
    action,
    compact = false,
    className = "",
}: EmptyStateProps) {
    return (
        <div
            className={`flex flex-col items-center justify-center text-center ${compact ? "py-8" : "py-16"} ${className}`}
        >
            {icon && (
                <div className="w-14 h-14 rounded-2xl bg-bg-2 flex items-center justify-center mb-4">
                    <Icon name={icon} size="lg" className="text-ink-3" />
                </div>
            )}
            <h3 className="font-medium text-ink mb-1">{title}</h3>
            {description && <p className="text-sm text-ink-3 max-w-sm">{description}</p>}
            {action && <div className="mt-5">{action}</div>}
        </div>
    );
}
