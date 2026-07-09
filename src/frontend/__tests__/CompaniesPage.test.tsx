import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";

const mockPush = vi.fn();
vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: mockPush }),
}));

const useCompanies = vi.fn();
const mutateCreate = vi.fn();
vi.mock("@/features/companies/hooks/use-companies", () => ({
    useCompanies: (...args: unknown[]) => useCompanies(...args),
    useCreateCompany: () => ({ mutate: mutateCreate, isPending: false }),
}));

import CompaniesPage from "@/app/(main)/companies/page";

const COMPANIES = [
    {
        id: "1",
        name: "Ромашка",
        descriptionExcerpt: "",
        callLogCount: 2,
        practiceCallCount: 3,
        createdAt: "2026-07-01T00:00:00Z",
        updatedAt: "2026-07-08T00:00:00Z",
    },
    {
        id: "2",
        name: "Вектор",
        descriptionExcerpt: "",
        callLogCount: 0,
        practiceCallCount: 0,
        createdAt: "2026-07-01T00:00:00Z",
        updatedAt: "2026-07-08T00:00:00Z",
    },
];

describe("CompaniesPage", () => {
    beforeEach(() => {
        useCompanies.mockReset();
        mutateCreate.mockReset();
        mockPush.mockReset();
    });

    it("renders a row per company with name and meta facts", () => {
        useCompanies.mockReturnValue({ data: COMPANIES, isLoading: false, error: null, refetch: vi.fn() });
        render(<CompaniesPage />);

        expect(screen.getByText("Ромашка")).toBeTruthy();
        expect(screen.getByText("Вектор")).toBeTruthy();
        expect(screen.getByText(/3 тренировки/)).toBeTruthy();
        expect(screen.getByText(/2 звонка/)).toBeTruthy();
    });

    it("shows the empty state with a CTA when there are no companies", () => {
        useCompanies.mockReturnValue({ data: [], isLoading: false, error: null, refetch: vi.fn() });
        render(<CompaniesPage />);

        expect(screen.getByText("Пока нет ни одной компании")).toBeTruthy();
        expect(screen.getByText("Добавить первую компанию")).toBeTruthy();
    });

    it("shows loading skeletons while fetching", () => {
        useCompanies.mockReturnValue({ data: undefined, isLoading: true, error: null, refetch: vi.fn() });
        const { container } = render(<CompaniesPage />);
        expect(container.querySelector(".co-list")).toBeTruthy();
    });

    it("shows an error state with retry on failure", () => {
        const refetch = vi.fn();
        useCompanies.mockReturnValue({ data: undefined, isLoading: false, error: new Error("boom"), refetch });
        render(<CompaniesPage />);

        expect(screen.getByText("Не удалось загрузить")).toBeTruthy();
        fireEvent.click(screen.getByRole("button", { name: /retry/i }));
        expect(refetch).toHaveBeenCalledOnce();
    });

    it("passes the typed search text to useCompanies for client-side filtering", () => {
        useCompanies.mockReturnValue({ data: COMPANIES, isLoading: false, error: null, refetch: vi.fn() });
        render(<CompaniesPage />);

        const searchInput = screen.getByPlaceholderText("Поиск по названию…");
        fireEvent.change(searchInput, { target: { value: "вект" } });

        expect(useCompanies).toHaveBeenLastCalledWith("вект");
    });

    it("opens the create modal and navigates to the new company on success", () => {
        useCompanies.mockReturnValue({ data: COMPANIES, isLoading: false, error: null, refetch: vi.fn() });
        render(<CompaniesPage />);

        fireEvent.click(screen.getByText("Добавить компанию"));
        expect(screen.getByText("Новая компания")).toBeTruthy();

        const nameInput = screen.getByPlaceholderText("Напр. ООО «Ромашка»");
        fireEvent.change(nameInput, { target: { value: "Тест ООО" } });
        fireEvent.click(screen.getByText("Создать"));

        expect(mutateCreate).toHaveBeenCalledWith(
            { name: "Тест ООО", description: "" },
            expect.objectContaining({ onSuccess: expect.any(Function) })
        );

        const options = mutateCreate.mock.calls[0][1];
        act(() => {
            options.onSuccess({ id: "new-id" });
        });
        expect(mockPush).toHaveBeenCalledWith("/companies/new-id");
    });
});
