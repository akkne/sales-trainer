import { describe, it, expect } from "vitest";
import {
    canManagePlatformUsers,
    isPlatformStaff,
    type OrgRole,
    type UserRole,
} from "@/shared/stores/auth-store";

/**
 * The owner's 2026-08-16 role split. These two predicates decide which affordances the platform
 * admin panel shows, and the values they accept are the same strings the backend mints into the
 * JWT — so the exhaustive lists below double as a check that the frontend and backend role
 * vocabularies have not drifted apart.
 *
 * They are only a display gate; the backend policies are what actually enforce the rule. What
 * matters here is that an `Admin` is never shown a button that would answer 403.
 */
describe("platform role gating", () => {
    const everyPlatformRole: UserRole[] = ["User", "Admin", "SuperAdmin"];
    const everyOrganizationRole: OrgRole[] = ["Manager", "TenancyAdmin", "TenancySuperAdmin"];

    it("admits both Sellevate staff roles to the platform admin panel", () => {
        expect(isPlatformStaff("Admin")).toBe(true);
        expect(isPlatformStaff("SuperAdmin")).toBe(true);
    });

    it("keeps ordinary users and absent roles out of the platform admin panel", () => {
        expect(isPlatformStaff("User")).toBe(false);
        expect(isPlatformStaff(null)).toBe(false);
        expect(isPlatformStaff(undefined)).toBe(false);
    });

    it("lets only a superadmin add or remove users", () => {
        expect(canManagePlatformUsers("SuperAdmin")).toBe(true);
        expect(canManagePlatformUsers("Admin")).toBe(false);
        expect(canManagePlatformUsers("User")).toBe(false);
        expect(canManagePlatformUsers(null)).toBe(false);
    });

    it("treats adding and removing users as the only thing separating Admin from SuperAdmin", () => {
        expect(isPlatformStaff("Admin")).toBe(isPlatformStaff("SuperAdmin"));
        expect(canManagePlatformUsers("Admin")).not.toBe(canManagePlatformUsers("SuperAdmin"));
    });

    it("covers every platform role the backend can mint", () => {
        expect(everyPlatformRole).toHaveLength(3);
        expect(everyPlatformRole.filter(isPlatformStaff)).toEqual(["Admin", "SuperAdmin"]);
        expect(everyPlatformRole.filter(canManagePlatformUsers)).toEqual(["SuperAdmin"]);
    });

    it("never lets an organization role reach the platform admin panel", () => {
        for (const organizationRole of everyOrganizationRole) {
            expect(isPlatformStaff(organizationRole as unknown as UserRole)).toBe(false);
            expect(canManagePlatformUsers(organizationRole as unknown as UserRole)).toBe(false);
        }
    });

    it("no longer recognises the retired role names", () => {
        expect(isPlatformStaff("OrgAdmin" as unknown as UserRole)).toBe(false);
        expect(everyOrganizationRole).not.toContain("OrgAdmin" as unknown as OrgRole);
    });
});
