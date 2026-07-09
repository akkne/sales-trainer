import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
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
const useCreatePracticeCall = vi.fn(() => ({ mutate: mutateCreatePracticeCall, isPending: false }));
vi.mock("@/features/companies/hooks/use-practice-calls", () => ({
    useCreatePracticeCall: (...args: unknown[]) => useCreatePracticeCall(...args),
}));

const completeDialogSession = vi.fn();
vi.mock("@/features/dialog/hooks/use-dialog", () => ({
    completeDialogSession: (...args: unknown[]) => completeDialogSession(...args),
}));

const useVoiceUsage = vi.fn(() => ({ data: undefined, refetch: vi.fn() }));
vi.mock("@/features/voice/hooks/use-voice-usage", () => ({
    useVoiceUsage: (...args: unknown[]) => useVoiceUsage(...args),
}));

let capturedVoiceOptions: Record<string, unknown> | null = null;
const startVoiceMock = vi.fn(async () => {
    (capturedVoiceOptions?.onSessionCreated as ((id: string) => void) | undefined)?.("sess-1");
});
const stopVoiceMock = vi.fn();
vi.mock("@/features/voice/hooks/use-voice", () => ({
    useVoice: (options: Record<string, unknown>) => {
        capturedVoiceOptions = options;
        return {
            state: "idle",
            currentTranscript: "",
            isVoiceAvailable: true,
            startVoice: startVoiceMock,
            stopVoice: stopVoiceMock,
        };
    },
}));

import CompanyVoiceCallPage from "@/app/companies/[id]/call/voice/page";

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
            <CompanyVoiceCallPage />
        </QueryClientProvider>
    );
}

describe("CompanyVoiceCallPage", () => {
    beforeEach(() => {
        useCompany.mockReset();
        useCompanyCallMode.mockReset();
        useCreatePracticeCall.mockClear();
        mutateCreatePracticeCall.mockReset();
        completeDialogSession.mockReset();
        mockPush.mockReset();
        mockReplace.mockReset();
        startVoiceMock.mockClear();
        stopVoiceMock.mockClear();
        capturedVoiceOptions = null;
        mockSearchParamsGoal = null;
        window.sessionStorage.clear();

        useCompany.mockReturnValue({ data: COMPANY, isLoading: false, error: null });
        useCompanyCallMode.mockReturnValue({ data: CALL_MODE, isLoading: false, error: null });
    });

    it("reads the goal from sessionStorage over the query param and builds companyContext for useVoice", () => {
        window.sessionStorage.setItem("company-call-goal:c1", "Договориться о встрече");
        mockSearchParamsGoal = "Fallback goal from URL";

        renderPage();

        expect(capturedVoiceOptions).toMatchObject({
            bundleId: "b1",
            modeId: "m1",
            companyContext: {
                companyName: "Ромашка",
                companyDescription: "Продаёт цветы оптом",
                callGoal: "Договориться о встрече",
            },
        });
    });

    it("falls back to the query param when sessionStorage has no stored goal", () => {
        mockSearchParamsGoal = "Fallback goal from URL";

        renderPage();

        expect(capturedVoiceOptions).toMatchObject({
            companyContext: expect.objectContaining({ callGoal: "Fallback goal from URL" }),
        });
    });

    it("omits callGoal from companyContext when no goal is available", () => {
        renderPage();

        expect(capturedVoiceOptions?.companyContext).toEqual({
            companyName: "Ромашка",
            companyDescription: "Продаёт цветы оптом",
        });
    });

    it("truncates the goal passed to companyContext.callGoal to 500 chars but keeps the full goal for the practice-call record", async () => {
        const longGoal = "a".repeat(600);
        window.sessionStorage.setItem("company-call-goal:c1", longGoal);

        renderPage();
        expect((capturedVoiceOptions?.companyContext as { callGoal: string }).callGoal).toHaveLength(500);

        fireEvent.click(screen.getByRole("button", { name: "Позвонить" }));
        await waitFor(() => expect(mutateCreatePracticeCall).toHaveBeenCalledOnce());
        expect(mutateCreatePracticeCall).toHaveBeenCalledWith({ dialogSessionId: "sess-1", goal: longGoal });
    });

    it("registers the practice call with the created session id and the full goal once the session is created", async () => {
        window.sessionStorage.setItem("company-call-goal:c1", "Узнать бюджет на Q3");

        renderPage();
        fireEvent.click(screen.getByRole("button", { name: "Позвонить" }));

        await waitFor(() => expect(mutateCreatePracticeCall).toHaveBeenCalledOnce());
        expect(mutateCreatePracticeCall).toHaveBeenCalledWith({
            dialogSessionId: "sess-1",
            goal: "Узнать бюджет на Q3",
        });
    });

    it("navigates back to /companies/[id] on the back link (not /dialog)", () => {
        renderPage();
        fireEvent.click(screen.getByLabelText("Назад к компании"));
        expect(mockPush).toHaveBeenCalledWith("/companies/c1");
    });

    it("navigates back to /companies/[id] after closing the feedback modal", async () => {
        completeDialogSession.mockResolvedValue({
            summary: "Хорошо",
            content: "Разбор звонка",
            generatedAt: "2026-07-09T00:00:00Z",
            xpEarned: 10,
        });

        renderPage();
        fireEvent.click(screen.getByRole("button", { name: "Позвонить" }));
        await waitFor(() => expect(mutateCreatePracticeCall).toHaveBeenCalledOnce());

        fireEvent.click(screen.getByRole("button", { name: "Завершить звонок" }));
        await waitFor(() => expect(completeDialogSession).toHaveBeenCalledWith("sess-1"));

        await waitFor(() => expect(screen.getByText("Закрыть разбор")).toBeTruthy());
        fireEvent.click(screen.getByText("Закрыть разбор"));

        expect(mockPush).toHaveBeenCalledWith("/companies/c1");
    });

    it("redirects to /companies when the company is not found", async () => {
        const { ApiError } = await import("@/shared/api/api-client");
        useCompany.mockReturnValue({ data: undefined, isLoading: false, error: new ApiError(404, {}) });

        renderPage();

        await waitFor(() => expect(mockReplace).toHaveBeenCalledWith("/companies"));
    });

    it("shows a graceful error state with a back link when the company-call mode is unavailable (503/404)", () => {
        const notConfiguredError = new Error("Service unavailable");
        useCompanyCallMode.mockReturnValue({ data: undefined, isLoading: false, error: notConfiguredError });

        renderPage();

        expect(screen.getByText("Тренировочные звонки недоступны")).toBeTruthy();
        expect(screen.getByText("← К компании")).toBeTruthy();
    });
});
