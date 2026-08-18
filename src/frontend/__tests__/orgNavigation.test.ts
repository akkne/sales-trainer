import { describe, expect, it } from "vitest";
import { ICON_NAMES } from "@/shared/components/icon";
import {
    ORGANIZATION_NAVIGATION_ITEMS,
    isOrganizationNavigationItemActive,
} from "@/features/org-shell/constants/navigation";

/**
 * The nine entries of the organization panel, fixed by ADMIN_UI_DESIGN.md §1.6 and owned by
 * slice 0. Slices 1–11 fill the routes in and are forbidden from editing this list, so this test
 * is the thing that notices if one of them does anyway.
 */
describe("organization navigation", () => {
    it("declares all nine entries, in the design's order", () => {
        expect(ORGANIZATION_NAVIGATION_ITEMS.map((item) => item.href)).toEqual([
            "/org",
            "/org/assignments",
            "/org/dialogs",
            "/org/reviews",
            "/org/content",
            "/org/profile",
            "/org/program",
            "/org/people",
            "/org/usage",
        ]);
    });

    it("labels every entry in Russian", () => {
        expect(ORGANIZATION_NAVIGATION_ITEMS.map((item) => item.label)).toEqual([
            "Команда",
            "Задания",
            "Разговоры",
            "Спорные оценки",
            "Контент",
            "Профиль компании",
            "Программа",
            "Люди",
            "Расход ИИ",
        ]);
    });

    it("uses icons the shared icon set actually has", () => {
        const availableIconNames = Object.values(ICON_NAMES);
        for (const item of ORGANIZATION_NAVIGATION_ITEMS) {
            expect(availableIconNames).toContain(item.icon);
        }
    });

    it("carries exactly the three badges the design allows", () => {
        const badgedEntries = ORGANIZATION_NAVIGATION_ITEMS.filter((item) => item.badge);
        expect(badgedEntries.map((item) => [item.href, item.badge])).toEqual([
            ["/org/assignments", "assignments"],
            ["/org/reviews", "reviews"],
            ["/org/content", "staleContent"],
        ]);
    });

    it("mentions no gamification anywhere", () => {
        const allText = ORGANIZATION_NAVIGATION_ITEMS.map(
            (item) => `${item.href} ${item.label}`
        ).join(" ");
        expect(allText).not.toMatch(/XP|стрик|лиг|Лиг/i);
    });

    it("lights the team entry only on the panel index", () => {
        expect(isOrganizationNavigationItemActive("/org", "/org")).toBe(true);
        expect(isOrganizationNavigationItemActive("/org", "/org/")).toBe(true);
        expect(isOrganizationNavigationItemActive("/org", "/org/assignments")).toBe(false);
        expect(isOrganizationNavigationItemActive("/org", "/org/content/overrides")).toBe(false);
    });

    it("keeps a section lit on its own sub-routes but not on a sibling with a shared stem", () => {
        expect(isOrganizationNavigationItemActive("/org/assignments", "/org/assignments/new")).toBe(
            true
        );
        expect(
            isOrganizationNavigationItemActive("/org/content", "/org/content/generation/17")
        ).toBe(true);
        expect(isOrganizationNavigationItemActive("/org/people", "/org/peopleandpets")).toBe(false);
    });
});
