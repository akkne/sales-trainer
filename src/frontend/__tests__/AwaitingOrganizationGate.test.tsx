import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: vi.fn() }),
}));

const logoutMutate = vi.fn();
vi.mock("@/features/auth/hooks/use-auth", () => ({
    useLogout: () => ({ mutate: logoutMutate, isPending: false }),
}));

import { AwaitingOrganizationGate } from "@/features/auth/components/awaiting-organization-gate";
import { useAuthStore, type UserRole } from "@/shared/stores/auth-store";

function signIn(role: UserRole, orgId: string | null) {
    useAuthStore.getState().setAuthenticatedUser({
        id: "u1",
        email: "someone@test.com",
        displayName: "Someone",
        isOnboardingCompleted: true,
        role,
        orgId,
        orgRole: orgId ? "Manager" : null,
    });
}

function renderGate() {
    return render(
        <AwaitingOrganizationGate>
            <p>tree</p>
        </AwaitingOrganizationGate>
    );
}

/**
 * Phase 40.37: registering creates an identity with no membership, so the learner app needs a
 * waiting room. What these pin down is who gets held and — more importantly — who does not.
 */
describe("AwaitingOrganizationGate", () => {
    beforeEach(() => {
        useAuthStore.getState().clearAuthSession();
        useAuthStore.setState({ authenticatedUser: null });
        logoutMutate.mockClear();
    });

    it("holds an ordinary user who belongs to no organization", () => {
        signIn("User", null);

        renderGate();

        expect(screen.getByText(/Ждём приглашение от компании/)).toBeTruthy();
        expect(screen.queryByText("tree")).toBeNull();
    });

    it("lets a user with an organization through", () => {
        signIn("User", "org-1");

        renderGate();

        expect(screen.getByText("tree")).toBeTruthy();
        expect(screen.queryByText(/Ждём приглашение/)).toBeNull();
    });

    /**
     * Platform roles are deliberately not bound to tenancy — they hold no membership anywhere, so
     * gating on `orgId` alone would lock Sellevate's own staff out of their own product.
     */
    it.each(["Admin", "SuperAdmin"] as const)(
        "lets platform staff (%s) through despite having no organization",
        (role) => {
            signIn(role, null);

            renderGate();

            expect(screen.getByText("tree")).toBeTruthy();
        }
    );

    /**
     * The gate must not become a second login redirect: an anonymous visitor is the api-client's
     * business, and the demo token has no user row at all.
     */
    it("leaves an unauthenticated visitor alone", () => {
        renderGate();

        expect(screen.getByText("tree")).toBeTruthy();
    });

    it("offers a way out, since the gate also covers /settings", () => {
        signIn("User", null);

        renderGate();
        screen.getByRole("button", { name: "Выйти" }).click();

        expect(logoutMutate).toHaveBeenCalled();
    });
});
