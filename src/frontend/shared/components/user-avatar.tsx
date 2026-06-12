"use client";

import { useState } from "react";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { EnvironmentConfiguration } from "@/config/environment";

interface UserAvatarProps {
    avatarUrl?: string | null;
    seed: string;
    size: number;
    circle?: boolean;
    className?: string;
}

function resolveAvatarUrl(avatarUrl: string): string {
    if (avatarUrl.startsWith("http://") || avatarUrl.startsWith("https://")) {
        return avatarUrl;
    }
    const base = EnvironmentConfiguration.apiBaseUrl.replace(/\/$/, "");
    return `${base}${avatarUrl}`;
}

export function UserAvatar({ avatarUrl, seed, size, circle = false, className = "" }: UserAvatarProps) {
    const [errored, setErrored] = useState(false);

    if (avatarUrl && !errored) {
        const src = resolveAvatarUrl(avatarUrl);
        return (
            <img
                src={src}
                alt={seed}
                width={size}
                height={size}
                onError={() => setErrored(true)}
                style={{
                    width: size,
                    height: size,
                    borderRadius: circle ? "50%" : size * 0.25,
                    objectFit: "cover",
                    flexShrink: 0,
                }}
                className={className}
            />
        );
    }

    return <GeoAvatar seed={seed} size={size} circle={circle} className={className} />;
}
