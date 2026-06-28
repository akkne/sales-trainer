"use client";

import { CSSProperties } from "react";

interface SkeletonProps {
    /** Height in px or any CSS size. */
    height?: number | string;
    width?: number | string;
    rounded?: number | string;
    className?: string;
    style?: CSSProperties;
}

/** Pulsing placeholder block on the April palette. */
export function Skeleton({ height = 16, width = "100%", rounded = 12, className = "", style }: SkeletonProps) {
    return (
        <div
            aria-hidden
            className={`animate-pulse bg-surface-2 ${className}`}
            style={{ height, width, borderRadius: rounded, ...style }}
        />
    );
}

interface SkeletonListProps {
    /** Number of placeholder rows. */
    count?: number;
    /** Row height in px. */
    rowHeight?: number;
    gap?: number;
    className?: string;
}

/** Vertical stack of pulsing card placeholders — default loading state for lists. */
export function SkeletonList({ count = 3, rowHeight = 72, gap = 12, className = "" }: SkeletonListProps) {
    return (
        <div className={`flex flex-col ${className}`} style={{ gap }} aria-label="Loading...">
            {Array.from({ length: count }, (_, index) => (
                <Skeleton key={index} height={rowHeight} rounded={16} />
            ))}
        </div>
    );
}
