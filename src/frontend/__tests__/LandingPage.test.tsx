import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("next/navigation", () => ({
    useRouter: () => ({ replace: vi.fn() }),
}));

import LandingPage from "@/app/page";
import { useAuthStore } from "@/shared/stores/auth-store";

describe("LandingPage", () => {
    beforeEach(() => {
        useAuthStore.getState().clearAuthSession();
    });

    it("offers a «Запросить демо» CTA that links to /demo", () => {
        render(<LandingPage />);

        const demoLinks = screen.getAllByRole("link", { name: /Запросить демо/ });
        expect(demoLinks.length).toBeGreaterThan(0);
        for (const link of demoLinks) {
            expect(link).toHaveAttribute("href", "/demo");
        }
    });

    it("keeps the existing «Войти» link reachable", () => {
        render(<LandingPage />);

        const loginLinks = screen.getAllByRole("link", { name: "Войти" });
        expect(loginLinks.length).toBeGreaterThan(0);
        for (const link of loginLinks) {
            expect(link).toHaveAttribute("href", "/login");
        }
    });
});
