import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
    useRouter: () => ({ replace }),
}));

import LandingPage from "@/app/landing/page";
import RootPage from "@/app/page";
import { useAuthStore } from "@/shared/stores/auth-store";

describe("RootPage", () => {
    beforeEach(() => {
        replace.mockClear();
        useAuthStore.getState().clearAuthSession();
    });

    it("sends an anonymous visitor to the landing", () => {
        render(<RootPage />);

        expect(replace).toHaveBeenCalledWith("/landing");
    });

    it("sends a signed-in visitor straight into the app", () => {
        useAuthStore.getState().setAccessToken("token");

        render(<RootPage />);

        expect(replace).toHaveBeenCalledWith("/tree");
    });
});

describe("LandingPage", () => {
    beforeEach(() => {
        replace.mockClear();
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

    it("stays put for a signed-in visitor instead of bouncing them to /tree", () => {
        useAuthStore.getState().setAccessToken("token");

        render(<LandingPage />);

        expect(replace).not.toHaveBeenCalled();
        expect(screen.getAllByRole("link", { name: /Запросить демо/ }).length).toBeGreaterThan(0);
    });
});
