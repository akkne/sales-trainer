import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";

const redirect = vi.fn();

vi.mock("next/navigation", () => ({
    useRouter: () => ({ replace: vi.fn() }),
    redirect: (path: string) => redirect(path),
}));

import LandingPage from "@/app/landing/page";
import RootPage from "@/app/page";
import { useAuthStore } from "@/shared/stores/auth-store";

describe("RootPage", () => {
    it("redirects the default path to /landing", () => {
        redirect.mockClear();

        RootPage();

        expect(redirect).toHaveBeenCalledWith("/landing");
    });
});

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
