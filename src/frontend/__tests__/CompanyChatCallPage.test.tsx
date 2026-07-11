import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const mockPush = vi.fn();
const mockReplace = vi.fn();
let mockSearchParamsGoal: string | null = null;
vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: mockPush, replace: mockReplace }),
    useParams: () => ({ id: "c1" }),
    useSearchParams: () => ({ get: (key: string) => (key === "goal" ? mockSearchParamsGoal : null) }),
}));

const useCompany = vi.fn();
vi.mock("@/features/companies/hooks/use-companies", () => ({
    useCompany: (...args: unknown[]) => useCompany(...args),
}));

const useCompanyCallMode = vi.fn();
vi.mock("@/features/companies/hooks/use-company-call-mode", () => ({
    useCompanyCallMode: (...args: unknown[]) => useCompanyCallMode(...args),
}));

const mutateCreatePracticeCall = vi.fn();
vi.mock("@/features/companies/hooks/use-practice-calls", () => ({
    useCreatePracticeCall: () => ({ mutate: mutateCreatePracticeCall, isPending: false }),
}));

const startDialogSession = vi.fn();
const sendDialogMessage = vi.fn();
const completeDialogSession = vi.fn();
vi.mock("@/features/dialog/hooks/use-dialog", () => ({
    startDialogSession: (...args: unknown[]) => startDialogSession(...args),
    sendDialogMessage: (...args: unknown[]) => sendDialogMessage(...args),
    completeDialogSession: (...args: unknown[]) => completeDialogSession(...args),
}));

import CompanyChatCallPage from "@/app/companies/[id]/call/chat/page";

const COMPANY = {
    id: "c1",
    name: "Ромашка",
    description: "Продаёт цветы оптом",
    callLogCount: 0,
    practiceCallCount: 0,
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: "2026-07-08T00:00:00Z",
};

const CALL_MODE = { bundleId: "b1", modeId: "m1" };

function renderPage() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return render(
        <QueryClientProvider client={queryClient}>
            <CompanyChatCallPage />
        </QueryClientProvider>
    );
}

describe("CompanyChatCallPage", () => {
    beforeEach(() => {
        useCompany.mockReset();
        useCompanyCallMode.mockReset();
        mutateCreatePracticeCall.mockReset();
        startDialogSession.mockReset();
        sendDialogMessage.mockReset();
        completeDialogSession.mockReset();
        mockPush.mockReset();
        mockReplace.mockReset();
        mockSearchParamsGoal = null;
        window.sessionStorage.clear();

        useCompany.mockReturnValue({ data: COMPANY, isLoading: false, error: null });
        useCompanyCallMode.mockReturnValue({ data: CALL_MODE, isLoading: false, error: null });
        startDialogSession.mockResolvedValue({ id: "sess-1", messages: [] });
    });

    it("creates the session with companyContext built from company + goal (sessionStorage wins over query param)", async () => {
        window.sessionStorage.setItem("company-call-goal:c1", "Договориться о встрече");
        mockSearchParamsGoal = "Fallback goal from URL";

        renderPage();

        await waitFor(() => expect(startDialogSession).toHaveBeenCalledOnce());
        expect(startDialogSession).toHaveBeenCalledWith("b1", "m1", {
            companyName: "Ромашка",
            companyDescription: "Продаёт цветы оптом",
            callGoal: "Договориться о встрече",
        });
    });

    it("includes persona fields in companyContext when a persona is stored in sessionStorage", async () => {
        window.sessionStorage.setItem("company-call-goal:c1", "Договориться о встрече");
        window.sessionStorage.setItem(
            "company-call-persona:c1",
            JSON.stringify({ name: "Мария Соколова", position: "Закупщик", personality: "Прагматична.", difficulty: "Hard" })
        );

        renderPage();

        await waitFor(() => expect(startDialogSession).toHaveBeenCalledOnce());
        expect(startDialogSession).toHaveBeenCalledWith("b1", "m1", {
            companyName: "Ромашка",
            companyDescription: "Продаёт цветы оптом",
            callGoal: "Договориться о встрече",
            personaName: "Мария Соколова",
            personaPosition: "Закупщик",
            personaPersonality: "Прагматична.",
            personaDifficulty: "Hard",
        });
    });

    it("falls back to the query param when sessionStorage has no stored goal", async () => {
        mockSearchParamsGoal = "Fallback goal from URL";

        renderPage();

        await waitFor(() => expect(startDialogSession).toHaveBeenCalledOnce());
        expect(startDialogSession).toHaveBeenCalledWith(
            "b1",
            "m1",
            expect.objectContaining({ callGoal: "Fallback goal from URL" })
        );
    });

    it("registers the practice call with the session id and the full goal once feedback is formed", async () => {
        window.sessionStorage.setItem("company-call-goal:c1", "Узнать бюджет на Q3");
        completeDialogSession.mockResolvedValue({
            summary: "Хорошо", content: "Разбор", generatedAt: "2026-07-09T00:00:00Z", xpEarned: 10,
        });

        renderPage();
        await waitFor(() => expect(startDialogSession).toHaveBeenCalledOnce());

        screen.getByRole("button", { name: "Завершить" }).click();

        await waitFor(() => expect(mutateCreatePracticeCall).toHaveBeenCalledOnce());
        expect(mutateCreatePracticeCall).toHaveBeenCalledWith({
            dialogSessionId: "sess-1",
            goal: "Узнать бюджет на Q3",
        });
    });

    it("does not register a practice call when the session ends without feedback", async () => {
        window.sessionStorage.setItem("company-call-goal:c1", "Узнать бюджет на Q3");
        completeDialogSession.mockResolvedValue(null);

        renderPage();
        await waitFor(() => expect(startDialogSession).toHaveBeenCalledOnce());

        screen.getByRole("button", { name: "Завершить" }).click();

        await waitFor(() => expect(completeDialogSession).toHaveBeenCalledWith("sess-1"));
        expect(mutateCreatePracticeCall).not.toHaveBeenCalled();
    });

    it("navigates back to /companies/[id] on close (not /dialog)", async () => {
        renderPage();
        await waitFor(() => expect(startDialogSession).toHaveBeenCalledOnce());

        screen.getByLabelText("Закрыть").click();
        expect(mockPush).toHaveBeenCalledWith("/companies/c1");
    });

    it("shows a graceful error state with a back link when the company-call mode is unavailable", () => {
        useCompanyCallMode.mockReturnValue({ data: undefined, isLoading: false, error: new Error("unavailable") });

        renderPage();

        expect(screen.getByText("Тренировочные звонки недоступны")).toBeTruthy();
        expect(screen.getByText("← К компании")).toBeTruthy();
    });
});
