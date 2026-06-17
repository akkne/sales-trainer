"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { trackPageView, type TrackedPage } from "@/shared/analytics/track";

/**
 * Maps the current App Router pathname to a bounded page name and fires a single page
 * view whenever it changes. The mapping keeps label cardinality fixed regardless of
 * dynamic segments (e.g. `/session/abc123` → `"session"`); anything unrecognised folds
 * to `"other"`, which the backend whitelist also accepts.
 */

function resolvePage(pathname: string): TrackedPage {
    const firstSegment = pathname.split("/").filter(Boolean)[0];

    switch (firstSegment) {
        case "tree":
        case "league":
        case "dialog":
        case "profile":
        case "guidebook":
        case "friends":
        case "discuss":
        case "session":
        case "login":
        case "register":
        case "onboarding":
        case "admin":
            return firstSegment;
        default:
            return "other";
    }
}

export function usePageViewTracker(): void {
    const pathname = usePathname();

    useEffect(() => {
        if (!pathname) return;
        trackPageView(resolvePage(pathname));
    }, [pathname]);
}
