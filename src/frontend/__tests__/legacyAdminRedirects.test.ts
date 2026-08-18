import { describe, expect, it } from "vitest";
import { resolveLegacyAdminRedirect } from "@/features/org-shell/lib/legacy-admin-redirects";

/**
 * Block 40.20 §1.5. Two of these paths are not hypothetical: the Phase 40.26 notification jobs
 * already minted them into `actionUrl` columns, so a person clicking a digest they received last
 * week must land on the organization panel rather than on a 404.
 */
describe("legacy /admin redirect table", () => {
    it("carries the assignment reminder link, query and all", () => {
        expect(
            resolveLegacyAdminRedirect(
                "/admin/assignments/8f0c2b3a-1111-2222-3333-444455556666",
                "?action=remind&scope=not_started"
            )
        ).toBe(
            "/org/assignments/8f0c2b3a-1111-2222-3333-444455556666?action=remind&scope=not_started"
        );
    });

    it("carries the disputed-review link with its note parameter", () => {
        expect(resolveLegacyAdminRedirect("/admin/dialog-reviews", "?note=abc-123")).toBe(
            "/org/reviews?note=abc-123"
        );
    });

    it("accepts a query string that arrives without its leading question mark", () => {
        expect(resolveLegacyAdminRedirect("/admin/dialog-reviews", "note=abc-123")).toBe(
            "/org/reviews?note=abc-123"
        );
    });

    it("maps every prefix the design lists", () => {
        expect(resolveLegacyAdminRedirect("/admin/assignments")).toBe("/org/assignments");
        expect(resolveLegacyAdminRedirect("/admin/dialog-reviews")).toBe("/org/reviews");
        expect(resolveLegacyAdminRedirect("/admin/dialog-sessions")).toBe("/org/dialogs");
        expect(resolveLegacyAdminRedirect("/admin/team")).toBe("/org");
        expect(resolveLegacyAdminRedirect("/admin/content/overrides")).toBe(
            "/org/content/overrides"
        );
        expect(resolveLegacyAdminRedirect("/admin/dialog/overrides")).toBe(
            "/org/content/overrides"
        );
        expect(resolveLegacyAdminRedirect("/admin/content-generation")).toBe(
            "/org/content/generation"
        );
        expect(resolveLegacyAdminRedirect("/admin/content/adaptations")).toBe(
            "/org/content/adaptations"
        );
        expect(resolveLegacyAdminRedirect("/admin/ai-usage")).toBe("/org/usage");
    });

    it("lets the longer prefix win so the platform dialog screen keeps its sub-routes", () => {
        expect(resolveLegacyAdminRedirect("/admin/dialog")).toBeNull();
        expect(resolveLegacyAdminRedirect("/admin/dialog/some-bundle-id")).toBeNull();
        expect(resolveLegacyAdminRedirect("/admin/dialog/overrides/modes/7")).toBe(
            "/org/content/overrides/modes/7"
        );
    });

    it("leaves every platform screen alone", () => {
        const platformPaths = [
            "/admin",
            "/admin/organizations",
            "/admin/organizations/42/quota",
            "/admin/import",
            "/admin/skills",
            "/admin/skill-stages",
            "/admin/topics",
            "/admin/lessons",
            "/admin/reference",
            "/admin/techniques",
            "/admin/quotes",
            "/admin/discuss",
            "/admin/prompts",
            "/admin/voice/usage",
            "/admin/leagues",
            "/admin/gamification",
            "/admin/users",
        ];
        for (const platformPath of platformPaths) {
            expect(resolveLegacyAdminRedirect(platformPath)).toBeNull();
        }
    });

    it("does not fire on a path that merely starts with the same letters", () => {
        expect(resolveLegacyAdminRedirect("/admin/teams")).toBeNull();
        expect(resolveLegacyAdminRedirect("/admin/assignments-archive")).toBeNull();
    });

    it("keeps the deeper part of a redirected path", () => {
        expect(resolveLegacyAdminRedirect("/admin/assignments/17/progress")).toBe(
            "/org/assignments/17/progress"
        );
        expect(resolveLegacyAdminRedirect("/admin/content/overrides/lessons/9")).toBe(
            "/org/content/overrides/lessons/9"
        );
    });
});
