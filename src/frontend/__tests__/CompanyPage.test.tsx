import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

const mockPush = vi.fn();
vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: mockPush }),
    useParams: () => ({ id: "c1" }),
}));

const useCompany = vi.fn();
const mutateUpdate = vi.fn();
const mutateUpdateStatus = vi.fn();
const mutateUpdateFollowUp = vi.fn();
const mutateDelete = vi.fn();
vi.mock("@/features/companies/hooks/use-companies", () => ({
    useCompany: (...args: unknown[]) => useCompany(...args),
    useUpdateCompany: () => ({ mutate: mutateUpdate, isPending: false }),
    useUpdateCompanyStatus: () => ({ mutate: mutateUpdateStatus, isPending: false }),
    useUpdateCompanyFollowUp: () => ({ mutate: mutateUpdateFollowUp, isPending: false }),
    useDeleteCompany: () => ({ mutate: mutateDelete, isPending: false }),
}));

const useCompanyLogs = vi.fn();
vi.mock("@/features/companies/hooks/use-company-logs", () => ({
    useCompanyLogs: (...args: unknown[]) => useCompanyLogs(...args),
    useAddCallLog: () => ({ mutate: vi.fn(), isPending: false }),
    useUpdateCallLog: () => ({ mutate: vi.fn(), isPending: false }),
    useDeleteCallLog: () => ({ mutate: vi.fn(), isPending: false }),
}));

const useCompanyPracticeCalls = vi.fn();
const useRecentGoals = vi.fn();
vi.mock("@/features/companies/hooks/use-practice-calls", () => ({
    useCompanyPracticeCalls: (...args: unknown[]) => useCompanyPracticeCalls(...args),
    useRecentGoals: (...args: unknown[]) => useRecentGoals(...args),
}));

const useCompanyContacts = vi.fn();
vi.mock("@/features/companies/hooks/use-company-contacts", () => ({
    useCompanyContacts: (...args: unknown[]) => useCompanyContacts(...args),
    useAddCompanyContact: () => ({ mutate: vi.fn(), isPending: false }),
    useUpdateCompanyContact: () => ({ mutate: vi.fn(), isPending: false }),
    useDeleteCompanyContact: () => ({ mutate: vi.fn(), isPending: false }),
}));

const useCompanyPersonas = vi.fn();
vi.mock("@/features/companies/hooks/use-company-personas", () => ({
    useCompanyPersonas: (...args: unknown[]) => useCompanyPersonas(...args),
    useAddCompanyPersona: () => ({ mutate: vi.fn(), isPending: false }),
    useDeleteCompanyPersona: () => ({ mutate: vi.fn(), isPending: false }),
    useGenerateCompanyPersona: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock("@/features/companies/hooks/use-company-briefing", () => ({
    useCompanyBriefing: () => ({ data: undefined, isLoading: false }),
    useGenerateCompanyBriefing: () => ({ mutate: vi.fn(), isPending: false, isError: false, error: null }),
}));

const useDialogSession = vi.fn(() => ({ data: undefined }));
vi.mock("@/features/dialog/hooks/use-dialog", () => ({
    useDialogSession: (...args: unknown[]) => useDialogSession(...args),
}));

import { ApiError } from "@/shared/api/api-client";
import CompanyPage from "@/app/(main)/companies/[id]/page";

const COMPANY = {
    id: "c1",
    name: "Ромашка",
    description: "Продаёт цветы",
    status: "Lead" as const,
    callLogCount: 0,
    practiceCallCount: 0,
    contactCount: 0,
    nextActionAt: null,
    nextActionNote: null,
    followUpNotifiedAt: null,
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: "2026-07-08T00:00:00Z",
};

describe("CompanyPage", () => {
    beforeEach(() => {
        useCompany.mockReset();
        useCompanyLogs.mockReset();
        useCompanyPracticeCalls.mockReset();
        useRecentGoals.mockReset();
        useCompanyContacts.mockReset();
        useCompanyPersonas.mockReset();
        useDialogSession.mockClear();
        mockPush.mockReset();

        useCompanyLogs.mockReturnValue({ data: [] });
        useCompanyPracticeCalls.mockReturnValue({ data: [] });
        useRecentGoals.mockReturnValue({ data: [] });
        useCompanyContacts.mockReturnValue({ data: [] });
        useCompanyPersonas.mockReturnValue({ data: [] });
    });

    it("shows loading skeletons while fetching the company", () => {
        useCompany.mockReturnValue({ data: undefined, isLoading: true, error: null, refetch: vi.fn() });
        const { container } = render(<CompanyPage />);
        expect(container.querySelector(".co-page")).toBeTruthy();
    });

    it("shows the not-found state on a 404", () => {
        useCompany.mockReturnValue({
            data: undefined,
            isLoading: false,
            error: new ApiError(404, {}),
            refetch: vi.fn(),
        });
        render(<CompanyPage />);
        expect(screen.getByText("Компания не найдена")).toBeTruthy();
        expect(screen.getByText("← К списку")).toBeTruthy();
    });

    it("shows a generic error state with retry for non-404 failures", () => {
        const refetch = vi.fn();
        useCompany.mockReturnValue({
            data: undefined,
            isLoading: false,
            error: new Error("boom"),
            refetch,
        });
        render(<CompanyPage />);
        expect(screen.getByText("Не удалось загрузить")).toBeTruthy();
        fireEvent.click(screen.getByText("Повторить"));
        expect(refetch).toHaveBeenCalledOnce();
    });

    it("renders the identity header, description and pre-call panel once loaded", () => {
        useCompany.mockReturnValue({ data: COMPANY, isLoading: false, error: null, refetch: vi.fn() });
        render(<CompanyPage />);

        expect(screen.getByText("Ромашка")).toBeTruthy();
        expect(screen.getByText("Продаёт цветы")).toBeTruthy();
        expect(screen.getByText("ТРЕНИРОВКА ПЕРЕД ЗВОНКОМ")).toBeTruthy();
    });

    it("shows the empty description placeholder when description is blank", () => {
        useCompany.mockReturnValue({ data: { ...COMPANY, description: "" }, isLoading: false, error: null, refetch: vi.fn() });
        render(<CompanyPage />);
        expect(screen.getByText("Добавить описание")).toBeTruthy();
    });

    it("shows the empty timeline message when there is no history", () => {
        useCompany.mockReturnValue({ data: COMPANY, isLoading: false, error: null, refetch: vi.fn() });
        render(<CompanyPage />);
        expect(screen.getByText("Здесь появятся ваши тренировки и записи о реальных звонках")).toBeTruthy();
    });

    it("does not fetch dialog sessions for practice calls on initial render", () => {
        useCompany.mockReturnValue({ data: COMPANY, isLoading: false, error: null, refetch: vi.fn() });
        useCompanyPracticeCalls.mockReturnValue({
            data: [{ id: "p1", companyId: "c1", dialogSessionId: "d1", goal: "Цель", createdAt: "2026-07-01T00:00:00Z" }],
        });
        render(<CompanyPage />);

        expect(useDialogSession).toHaveBeenCalledWith(null);
    });

    it("navigates to the call route with the goal on 'Позвонить'", () => {
        useCompany.mockReturnValue({ data: COMPANY, isLoading: false, error: null, refetch: vi.fn() });
        render(<CompanyPage />);

        fireEvent.change(screen.getByPlaceholderText(/Цель звонка/), { target: { value: "Договориться о встрече" } });
        fireEvent.click(screen.getByText("Позвонить"));

        expect(mockPush).toHaveBeenCalledWith(
            "/companies/c1/call/voice?goal=" + encodeURIComponent("Договориться о встрече")
        );
    });

    it("opens the delete-company confirm and calls the delete mutation", () => {
        useCompany.mockReturnValue({ data: COMPANY, isLoading: false, error: null, refetch: vi.fn() });
        render(<CompanyPage />);

        fireEvent.click(screen.getByLabelText("Удалить компанию"));
        expect(screen.getByText("Удалить компанию?")).toBeTruthy();

        fireEvent.click(screen.getByRole("button", { name: "Удалить" }));
        expect(mutateDelete).toHaveBeenCalledWith("c1", expect.objectContaining({ onSuccess: expect.any(Function) }));
    });
});
