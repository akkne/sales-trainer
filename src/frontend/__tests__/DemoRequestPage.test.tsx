import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

vi.mock("@/shared/api/api-client", () => ({
    ApiError: class ApiError extends Error {
        status: number;
        payload: Record<string, unknown>;
        constructor(status: number, payload: Record<string, unknown>) {
            super(typeof payload.message === "string" ? payload.message : `HTTP ${status}`);
            this.status = status;
            this.payload = payload;
        }
    },
    apiClient: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
        delete: vi.fn(),
    },
}));

import { ApiError, apiClient } from "@/shared/api/api-client";
import DemoRequestPage from "@/app/demo/page";

const mockPost = apiClient.post as ReturnType<typeof vi.fn>;

function renderDemoRequestPage() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    return render(<DemoRequestPage />, { wrapper });
}

interface FillOptions {
    tickDataProcessingConsent?: boolean;
    tickMarketingConsent?: boolean;
    fillPhone?: boolean;
}

async function fillRequiredFields(options: FillOptions = {}) {
    const { tickDataProcessingConsent = true, tickMarketingConsent = false, fillPhone = true } = options;

    await userEvent.type(screen.getByLabelText("Имя и фамилия"), "Иван Петров");
    await userEvent.type(screen.getByLabelText("Рабочий email"), "ivan@customer.test");
    if (fillPhone) {
        await userEvent.type(screen.getByLabelText("Телефон"), "+7 900 123-45-67");
    }
    await userEvent.type(screen.getByLabelText("Компания"), "ООО Ромашка");
    await userEvent.type(screen.getByLabelText("Должность"), "Руководитель отдела продаж");
    await userEvent.selectOptions(
        screen.getByLabelText("Размер отдела продаж"),
        "SixToTwenty",
    );
    await userEvent.type(screen.getByLabelText("Комментарий"), "Интересует голосовая практика");
    if (tickDataProcessingConsent) {
        await userEvent.click(screen.getByLabelText(/Даю согласие/));
    }
    if (tickMarketingConsent) {
        await userEvent.click(screen.getByLabelText(/информационные и рекламные материалы/));
    }
}

describe("DemoRequestPage", () => {
    beforeEach(() => {
        mockPost.mockReset();
    });

    it("renders every field from the frozen contract", () => {
        renderDemoRequestPage();

        expect(screen.getByLabelText("Имя и фамилия")).toBeInTheDocument();
        expect(screen.getByLabelText("Рабочий email")).toBeInTheDocument();
        expect(screen.getByLabelText("Телефон")).toBeInTheDocument();
        expect(screen.getByLabelText("Компания")).toBeInTheDocument();
        expect(screen.getByLabelText("Должность")).toBeInTheDocument();
        expect(screen.getByLabelText("Размер отдела продаж")).toBeInTheDocument();
        expect(screen.getByLabelText("Комментарий")).toBeInTheDocument();
        expect(screen.getByLabelText(/Даю согласие/)).toBeInTheDocument();
        expect(
            screen.getByLabelText(/информационные и рекламные материалы/),
        ).toBeInTheDocument();
        expect(screen.getByLabelText("Не заполняйте это поле")).toBeInTheDocument();
    });

    it("submits the exact payload shape, with salesTeamSize as the English enum value and marketingConsentGiven: false when left unticked", async () => {
        mockPost.mockResolvedValueOnce({ id: "demo-1", submittedAt: "2026-08-20T10:00:00Z" });
        renderDemoRequestPage();

        await fillRequiredFields();
        await userEvent.click(screen.getByRole("button", { name: /Отправить заявку/ }));

        await waitFor(() =>
            expect(mockPost).toHaveBeenCalledWith("/demo-requests", {
                fullName: "Иван Петров",
                workEmail: "ivan@customer.test",
                phone: "+7 900 123-45-67",
                companyName: "ООО Ромашка",
                jobTitle: "Руководитель отдела продаж",
                salesTeamSize: "SixToTwenty",
                comment: "Интересует голосовая практика",
                consentGiven: true,
                marketingConsentGiven: false,
                website: "",
            }),
        );
    });

    it("sends marketingConsentGiven: true when the marketing checkbox is ticked", async () => {
        mockPost.mockResolvedValueOnce({ id: "demo-1", submittedAt: "2026-08-20T10:00:00Z" });
        renderDemoRequestPage();

        await fillRequiredFields({ tickMarketingConsent: true });
        await userEvent.click(screen.getByRole("button", { name: /Отправить заявку/ }));

        await waitFor(() =>
            expect(mockPost).toHaveBeenCalledWith(
                "/demo-requests",
                expect.objectContaining({ marketingConsentGiven: true, consentGiven: true }),
            ),
        );
    });

    it("blocks submission when the required data-processing consent is left unticked", async () => {
        renderDemoRequestPage();

        await fillRequiredFields({ tickDataProcessingConsent: false });
        await userEvent.click(screen.getByRole("button", { name: /Отправить заявку/ }));

        expect(mockPost).not.toHaveBeenCalled();
    });

    it("blocks submission when phone is left empty, now that the owner requires it", async () => {
        renderDemoRequestPage();

        await fillRequiredFields({ fillPhone: false });
        await userEvent.click(screen.getByRole("button", { name: /Отправить заявку/ }));

        expect(mockPost).not.toHaveBeenCalled();
    });

    it("shows the success heading after a resolved 202", async () => {
        mockPost.mockResolvedValueOnce({ id: "demo-1", submittedAt: "2026-08-20T10:00:00Z" });
        renderDemoRequestPage();

        await fillRequiredFields();
        await userEvent.click(screen.getByRole("button", { name: /Отправить заявку/ }));

        expect(await screen.findByText("Отлично, мы с вами свяжемся")).toBeInTheDocument();
        expect(screen.getByText("ivan@customer.test")).toBeInTheDocument();
    });

    it("maps a 429 to the cooldown message", async () => {
        mockPost.mockRejectedValueOnce(
            new ApiError(429, { message: "too soon", retryAfterSeconds: 900 }),
        );
        renderDemoRequestPage();

        await fillRequiredFields();
        await userEvent.click(screen.getByRole("button", { name: /Отправить заявку/ }));

        expect(
            await screen.findByText(/Повторить можно будет примерно через 15 мин/),
        ).toBeInTheDocument();
    });

    it("disables the submit button while the request is pending", async () => {
        mockPost.mockImplementationOnce(() => new Promise(() => {}));
        renderDemoRequestPage();

        await fillRequiredFields();
        await userEvent.click(screen.getByRole("button", { name: /Отправить заявку/ }));

        expect(await screen.findByRole("button", { name: "Отправляем..." })).toBeDisabled();
    });
});
