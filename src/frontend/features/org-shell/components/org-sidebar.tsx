"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import {
    ORGANIZATION_NAVIGATION_ITEMS,
    isOrganizationNavigationItemActive,
    type OrganizationNavigationBadge,
} from "@/features/org-shell/constants/navigation";
import type { OrganizationNavigationBadgeCounts } from "@/features/org-shell/hooks/use-org-nav-badges";

interface OrgSidebarProps {
    organizationName: string;
    isOpen: boolean;
    onClose: () => void;
    badges: OrganizationNavigationBadgeCounts;
}

function readBadgeCount(
    badge: OrganizationNavigationBadge | undefined,
    badges: OrganizationNavigationBadgeCounts
): number | null {
    if (badge === "assignments") return badges.activeAssignmentCount;
    if (badge === "reviews") return badges.openScoreDisputeCount;
    return null;
}

/**
 * The organization panel's navigation. Structurally the platform sidebar — the same `w-56`, the
 * same mobile drawer, the same `min-h-0 overflow-y-auto` — because the two panels are siblings and
 * a person who holds both roles should not have to relearn the furniture. What differs is the
 * language, the component library, and the fact that the entries live in a constants file.
 *
 * On desktop it is `sticky` and exactly one viewport tall, not `static`. As a stretched flex item
 * it inherited the height of `<main>`, so on any screen taller than the window — the people table,
 * the content list — the footer sat at the bottom of the *document* and «В приложение» could only
 * be reached by scrolling past all the data. Bounding the height puts the exit in the bottom-left
 * corner of the viewport on every screen, which is what makes the `overflow-y-auto` on the nav
 * below actually do something.
 *
 * Following a link closes the mobile drawer here, at the click, rather than in an effect watching
 * the pathname: navigating within the panel is the only thing that changes it, and the effect
 * version re-renders the whole shell twice for every route change.
 */
export function OrgSidebar({ organizationName, isOpen, onClose, badges }: OrgSidebarProps) {
    const pathname = usePathname();

    return (
        <aside
            className={`w-56 shrink-0 bg-surface flex flex-col fixed md:sticky top-0 bottom-0 md:bottom-auto md:h-screen left-0 z-50 border-r border-line md:border-r-0 transition-transform duration-200 md:translate-x-0 ${
                isOpen ? "translate-x-0" : "-translate-x-full"
            }`}
        >
            <div className="px-5 py-4 flex items-start justify-between gap-2">
                <div className="min-w-0">
                    <span className="block truncate font-bold text-ink text-sm">
                        {organizationName}
                    </span>
                    <span className="block text-xs text-ink-3 mt-0.5">Панель управления</span>
                </div>
                <button
                    type="button"
                    onClick={onClose}
                    aria-label="Закрыть меню"
                    className="md:hidden shrink-0 grid place-items-center w-8 h-8 rounded-lg text-ink-3 hover:text-ink"
                >
                    <Icon name="close" size="sm" />
                </button>
            </div>

            {/* min-h-0 is required for overflow-y-auto to take effect: a flex item's auto
                minimum size otherwise refuses to shrink below its content height, and the nav
                links overflow the one-viewport-tall aside on short screens. */}
            <nav className="flex-1 min-h-0 overflow-y-auto py-2 px-2 space-y-0.5">
                {ORGANIZATION_NAVIGATION_ITEMS.map((item) => {
                    const isActive = isOrganizationNavigationItemActive(item.href, pathname);
                    const badgeCount = readBadgeCount(item.badge, badges);
                    const showsStaleDot = item.badge === "staleContent" && badges.hasStaleContent;

                    return (
                        <Link
                            key={item.href}
                            href={item.href}
                            onClick={onClose}
                            aria-current={isActive ? "page" : undefined}
                            className={`flex items-center gap-3 px-3 py-2.5 text-sm rounded-xl transition-colors ${
                                isActive
                                    ? "bg-primary-soft text-primary-ink font-medium"
                                    : "text-ink-3 hover:text-ink hover:bg-bg-2"
                            }`}
                        >
                            <Icon name={item.icon} size="sm" />
                            <span className="flex-1 min-w-0 truncate">{item.label}</span>
                            {badgeCount !== null && badgeCount > 0 && (
                                <span
                                    className="tnum inline-flex items-center justify-center min-w-5 h-5 px-1.5 rounded-full text-[11px] font-semibold"
                                    style={{
                                        fontFamily: "var(--font-mono)",
                                        background: "var(--bg-2)",
                                        color: "var(--ink-2)",
                                    }}
                                >
                                    {badgeCount}
                                </span>
                            )}
                            {showsStaleDot && (
                                <span
                                    aria-label="Есть устаревшие версии"
                                    className="inline-block w-2 h-2 rounded-full"
                                    style={{ background: "var(--amber)" }}
                                />
                            )}
                        </Link>
                    );
                })}
            </nav>

            {/* The exit, and only the exit. The footer used to carry a second link into the
                platform panel for Sellevate staff; it is gone on the owner's instruction, so the
                organization panel now offers exactly one way out of itself on every screen and
                never advertises the internal tool to the customer looking over a shoulder. */}
            <div className="px-5 py-4">
                <Link
                    href="/tree"
                    onClick={onClose}
                    className="flex items-center gap-2 text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    <Icon name="arrow-left" size="sm" />В приложение
                </Link>
            </div>
        </aside>
    );
}
