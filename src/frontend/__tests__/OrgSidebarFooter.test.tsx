import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("next/navigation", () => ({
    usePathname: () => "/org/people",
}));

import { OrgSidebar } from "@/features/org-shell/components/org-sidebar";

const NO_BADGES = {
    activeAssignmentCount: 0,
    openScoreDisputeCount: 0,
    hasStaleContent: false,
};

function renderSidebar() {
    return render(
        <OrgSidebar
            organizationName="Мосмет"
            isOpen={false}
            onClose={() => {}}
            badges={NO_BADGES}
        />
    );
}

/**
 * The two rules the organization panel's footer has to keep on *every* screen, both reported from
 * the running app: the way out is always there, and it is the only thing there.
 */
describe("OrgSidebar footer", () => {
    it("offers «В приложение» as the way out", () => {
        renderSidebar();

        expect(screen.getByRole("link", { name: /В приложение/ })).toHaveAttribute("href", "/tree");
    });

    it("carries no link into the platform panel", () => {
        const { container } = renderSidebar();

        const platformLinks = Array.from(container.querySelectorAll("a[href]")).filter((link) =>
            (link.getAttribute("href") ?? "").startsWith("/admin")
        );

        expect(platformLinks).toEqual([]);
        expect(screen.queryByText(/Платформенная админка/)).toBeNull();
    });

    /**
     * The regression behind "the exit button is missing on some pages": as a stretched `static`
     * flex item the aside took its height from `<main>`, so on a screen taller than the window the
     * footer sat at the bottom of the document instead of the viewport. jsdom computes no layout,
     * so the assertion is on the contract that fixes it — bounded height, and `sticky` rather than
     * `static`, from the `md` breakpoint up.
     */
    it("is one viewport tall and sticky on desktop, so the footer cannot drift down the page", () => {
        const { container } = renderSidebar();

        const aside = container.querySelector("aside");

        expect(aside).not.toBeNull();
        expect(aside!.className).toContain("md:sticky");
        expect(aside!.className).toContain("md:h-screen");
        expect(aside!.className).not.toContain("md:static");
    });
});
