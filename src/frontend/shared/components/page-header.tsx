"use client";

import Link from "next/link";
import { ReactNode } from "react";
import { Icon } from "./icon";

interface PageHeaderProps {
    title: string;
    subtitle?: string;
    action?: ReactNode;
    backHref?: string;
    backLabel?: string;
    className?: string;
}

/** The same masthead on every screen of the organization panel: where you are, one way back, one verb. */
export function PageHeader({
    title,
    subtitle,
    action,
    backHref,
    backLabel,
    className = "",
}: PageHeaderProps) {
    return (
        <header className={`mb-6 ${className}`}>
            {backHref && (
                <Link
                    href={backHref}
                    className="inline-flex items-center gap-1.5 mb-3 text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    <Icon name="arrow-left" size="sm" />
                    {backLabel ?? "Назад"}
                </Link>
            )}
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                    <h1 className="text-xl font-bold text-ink">{title}</h1>
                    {subtitle && <p className="mt-1 text-sm text-ink-3">{subtitle}</p>}
                </div>
                {action && <div className="shrink-0">{action}</div>}
            </div>
        </header>
    );
}
