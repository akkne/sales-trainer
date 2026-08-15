import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

vi.mock("@/shared/api/api-client", () => ({
    ApiError: class ApiError extends Error {
        payload: Record<string, unknown> = {};
    },
    apiClient: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
        delete: vi.fn(),
    },
}));

const routerPush = vi.fn();
vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: routerPush, replace: vi.fn(), refresh: vi.fn() }),
}));

vi.mock("@/shared/components/google-login-button", () => ({
    GoogleLoginButton: () => <button type="button">Google</button>,
}));

import { apiClient } from "@/shared/api/api-client";
import LoginPage from "@/app/(auth)/login/page";

const mockPost = apiClient.post as ReturnType<typeof vi.fn>;

function renderLoginPage() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    return render(<LoginPage />, { wrapper });
}

/**
 * Phase 40.8 — the login screen is two-stage because the login method is a per-organization
 * setting the server names (docs/TENANCY/TENANCY.md §4.5).
 */
describe("LoginPage", () => {
    beforeEach(() => {
        mockPost.mockReset();
        routerPush.mockReset();
    });

    it("asks only for the email first and resolves the method from the server", async () => {
        mockPost.mockResolvedValueOnce({ method: "password" });
        renderLoginPage();

        expect(screen.queryByPlaceholderText("Пароль")).not.toBeInTheDocument();

        await userEvent.type(screen.getByPlaceholderText("Email"), "member@customer.test");
        await userEvent.click(screen.getByRole("button", { name: "Продолжить" }));

        await waitFor(() =>
            expect(mockPost).toHaveBeenCalledWith("/auth/login/start", {
                email: "member@customer.test",
            }),
        );
        expect(await screen.findByPlaceholderText("Пароль")).toBeInTheDocument();
    });

    it("posts the credential to /auth/login only on the second stage", async () => {
        mockPost
            .mockResolvedValueOnce({ method: "password" })
            .mockResolvedValueOnce({
                accessToken: "token",
                userId: "user-1",
                displayName: "Member",
                isOnboardingCompleted: true,
                role: "User",
            });
        renderLoginPage();

        await userEvent.type(screen.getByPlaceholderText("Email"), "member@customer.test");
        await userEvent.click(screen.getByRole("button", { name: "Продолжить" }));
        await userEvent.type(await screen.findByPlaceholderText("Пароль"), "Password123!");
        await userEvent.click(screen.getByRole("button", { name: "Войти" }));

        await waitFor(() =>
            expect(mockPost).toHaveBeenCalledWith("/auth/login", {
                email: "member@customer.test",
                password: "Password123!",
            }),
        );
    });

    /**
     * The seam has to be visible: password login is refused server-side for an SSO organization,
     * so showing the field anyway would only produce a confusing 401.
     */
    it("offers no password field when the organization is configured for SSO", async () => {
        mockPost.mockResolvedValueOnce({ method: "oidc" });
        renderLoginPage();

        await userEvent.type(screen.getByPlaceholderText("Email"), "employee@bigcustomer.test");
        await userEvent.click(screen.getByRole("button", { name: "Продолжить" }));

        expect(await screen.findByText(/корпоративный вход \(SSO\)/)).toBeInTheDocument();
        expect(screen.queryByPlaceholderText("Пароль")).not.toBeInTheDocument();
    });

    it("returns to the email stage when the address is changed", async () => {
        mockPost.mockResolvedValueOnce({ method: "password" });
        renderLoginPage();

        await userEvent.type(screen.getByPlaceholderText("Email"), "member@customer.test");
        await userEvent.click(screen.getByRole("button", { name: "Продолжить" }));
        await userEvent.click(await screen.findByRole("button", { name: "Изменить" }));

        expect(screen.getByPlaceholderText("Email")).toBeInTheDocument();
        expect(screen.queryByPlaceholderText("Пароль")).not.toBeInTheDocument();
    });
});
