import { describe, expect, it } from "vitest";
import {
    canManageOrganizationPeople,
    isOrganizationStaff,
    isPlatformStaff,
    type OrgRole,
    type UserRole,
} from "@/shared/stores/auth-store";

/**
 * The organization-panel half of the owner's 2026-08-16 role split (ADMIN_UI_DESIGN.md §1.2).
 *
 * Same contract as `roleGating.test.ts` keeps for the platform axis: these predicates only decide
 * which affordances are drawn — `RequireOrgAdmin` and `RequireOrgSuperAdmin` are what enforce the
 * rule — so what matters is that a `TenancyAdmin` is never shown a button that would answer 403.
 */
describe("organization role gating", () => {
    const everyOrganizationRole: OrgRole[] = ["Manager", "TenancyAdmin", "TenancySuperAdmin"];
    const everyPlatformRole: UserRole[] = ["User", "Admin", "SuperAdmin"];

    it("admits both organization admin roles to the organization panel", () => {
        expect(isOrganizationStaff("TenancyAdmin")).toBe(true);
        expect(isOrganizationStaff("TenancySuperAdmin")).toBe(true);
    });

    it("keeps a plain manager and a member with no organization role out", () => {
        expect(isOrganizationStaff("Manager")).toBe(false);
        expect(isOrganizationStaff(null)).toBe(false);
        expect(isOrganizationStaff(undefined)).toBe(false);
    });

    it("lets only an organization superadmin invite and deactivate people", () => {
        expect(canManageOrganizationPeople("TenancySuperAdmin")).toBe(true);
        expect(canManageOrganizationPeople("TenancyAdmin")).toBe(false);
        expect(canManageOrganizationPeople("Manager")).toBe(false);
        expect(canManageOrganizationPeople(null)).toBe(false);
        expect(canManageOrganizationPeople(undefined)).toBe(false);
    });

    it("treats managing people as the only thing separating the two admin roles", () => {
        expect(isOrganizationStaff("TenancyAdmin")).toBe(isOrganizationStaff("TenancySuperAdmin"));
        expect(canManageOrganizationPeople("TenancyAdmin")).not.toBe(
            canManageOrganizationPeople("TenancySuperAdmin")
        );
    });

    it("covers every organization role the backend can mint", () => {
        expect(everyOrganizationRole).toHaveLength(3);
        expect(everyOrganizationRole.filter(isOrganizationStaff)).toEqual([
            "TenancyAdmin",
            "TenancySuperAdmin",
        ]);
        expect(everyOrganizationRole.filter(canManageOrganizationPeople)).toEqual([
            "TenancySuperAdmin",
        ]);
    });

    it("never mistakes a platform role for an organization membership", () => {
        for (const platformRole of everyPlatformRole) {
            expect(isOrganizationStaff(platformRole as unknown as OrgRole)).toBe(false);
            expect(canManageOrganizationPeople(platformRole as unknown as OrgRole)).toBe(false);
        }
    });

    it("no longer recognises the retired OrgAdmin name", () => {
        expect(isOrganizationStaff("OrgAdmin" as unknown as OrgRole)).toBe(false);
        expect(canManageOrganizationPeople("OrgAdmin" as unknown as OrgRole)).toBe(false);
    });

    it("keeps the two axes independent — the panel gate is a union of them", () => {
        const isAdmittedToOrganizationPanel = (role: UserRole, orgRole: OrgRole | null) =>
            isOrganizationStaff(orgRole) || isPlatformStaff(role);

        expect(isAdmittedToOrganizationPanel("User", "Manager")).toBe(false);
        expect(isAdmittedToOrganizationPanel("User", "TenancyAdmin")).toBe(true);
        expect(isAdmittedToOrganizationPanel("Admin", null)).toBe(true);
        expect(isAdmittedToOrganizationPanel("SuperAdmin", "TenancySuperAdmin")).toBe(true);
    });
});
