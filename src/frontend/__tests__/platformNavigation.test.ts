import { describe, expect, it } from "vitest";
import { ICON_NAMES } from "@/shared/components/icon";
import { PLATFORM_NAVIGATION_ITEMS } from "@/features/admin/constants/navigation";

/**
 * The platform admin panel's nav. The reason this list is a module-level constant with a test at
 * all is Q-5 (`docs/NIGHT_AUDIT_QUESTIONS.md`): the two gamification screens were unlinked because
 * XP/streaks/leagues are out of the product and one of their silent mutations ("close week",
 * W-15) is irreversible. Nothing else stops someone from re-adding the entry in a later diff, so
 * that is what the last test here is for.
 */
describe("platform navigation", () => {
    it("declares every entry exactly once", () => {
        const hrefs = PLATFORM_NAVIGATION_ITEMS.map((item) => item.href);
        expect(new Set(hrefs).size).toBe(hrefs.length);
    });

    it("labels and routes every entry under /admin", () => {
        for (const item of PLATFORM_NAVIGATION_ITEMS) {
            expect(item.href.startsWith("/admin/")).toBe(true);
            expect(item.label.trim().length).toBeGreaterThan(0);
        }
    });

    it("uses icons the shared icon set actually has", () => {
        const availableIconNames = Object.values(ICON_NAMES);
        for (const item of PLATFORM_NAVIGATION_ITEMS) {
            expect(availableIconNames).toContain(item.icon);
        }
    });

    it("offers no route into the retired gamification screens (Q-5)", () => {
        const hrefs = PLATFORM_NAVIGATION_ITEMS.map((item) => item.href);
        expect(hrefs).not.toContain("/admin/leagues");
        expect(hrefs).not.toContain("/admin/gamification");

        const allText = PLATFORM_NAVIGATION_ITEMS.map(
            (item) => `${item.href} ${item.label}`
        ).join(" ");
        expect(allText).not.toMatch(/league|gamification|streak|\bxp\b/i);
    });
});
