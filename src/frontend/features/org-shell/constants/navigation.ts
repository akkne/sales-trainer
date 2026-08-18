import type { IconName } from "@/shared/components/icon";

/** Which of the three navigation counters a nav entry carries, if any (ADMIN_UI_DESIGN.md §1.6). */
export type OrganizationNavigationBadge = "assignments" | "reviews" | "staleContent";

export interface OrganizationNavigationItem {
    href: string;
    label: string;
    icon: IconName;
    badge?: OrganizationNavigationBadge;
}

/**
 * Every entry of the organization panel, declared at module level and in one file.
 *
 * Module level rather than inside the sidebar so that the eleven screen slices of block 40.20 do
 * not all edit the same line of the same component; one file so that adding a screen is one diff
 * and not a merge conflict. Slices after slice 0 read this list and never write it
 * (docs/TENANCY/ADMIN_UI_DESIGN.md §5).
 *
 * Order is the order on screen and it is the РОП's week: the team first, then what was handed to
 * it, then what came back, then the settings that shape all three.
 */
export const ORGANIZATION_NAVIGATION_ITEMS: OrganizationNavigationItem[] = [
    { href: "/org", label: "Команда", icon: "target" },
    { href: "/org/assignments", label: "Задания", icon: "grid", badge: "assignments" },
    { href: "/org/dialogs", label: "Разговоры", icon: "message" },
    { href: "/org/reviews", label: "Спорные оценки", icon: "warning", badge: "reviews" },
    { href: "/org/content", label: "Контент", icon: "layers", badge: "staleContent" },
    { href: "/org/profile", label: "Профиль компании", icon: "briefcase" },
    { href: "/org/program", label: "Программа", icon: "book" },
    { href: "/org/people", label: "Люди", icon: "users" },
    { href: "/org/usage", label: "Расход ИИ", icon: "zap" },
];

/**
 * `/org` is a prefix of every other entry, so a `startsWith` test would light «Команда» on every
 * screen of the panel. The index entry matches exactly; the rest match by prefix, which is what
 * keeps «Задания» lit on `/org/assignments/new`.
 */
export function isOrganizationNavigationItemActive(itemHref: string, pathname: string): boolean {
    if (itemHref === "/org") {
        return pathname === "/org" || pathname === "/org/";
    }
    return pathname === itemHref || pathname.startsWith(`${itemHref}/`);
}
