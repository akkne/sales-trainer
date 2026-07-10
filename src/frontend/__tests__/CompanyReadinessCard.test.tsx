import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CompanyReadinessCard } from "@/features/companies/components/company-readiness-card";

describe("CompanyReadinessCard", () => {
    const onRefresh = vi.fn();

    beforeEach(() => {
        onRefresh.mockReset();
    });

    it("shows a loading state", () => {
        render(
            <CompanyReadinessCard
                readiness={undefined}
                isLoading={true}
                errorMessage={null}
                onRefresh={onRefresh}
            />
        );

        expect(screen.getByText("Загрузка…")).toBeTruthy();
    });

    it("shows the empty state when no readiness data exists yet", () => {
        render(
            <CompanyReadinessCard
                readiness={{ score: null, strengths: null, gaps: null, recommendation: null, generatedAt: null }}
                isLoading={false}
                errorMessage={null}
                onRefresh={onRefresh}
            />
        );

        expect(screen.getByText("Проведите тренировку, чтобы получить оценку готовности.")).toBeTruthy();
    });

    it("renders the score, strengths, gaps, and recommendation when data exists", () => {
        render(
            <CompanyReadinessCard
                readiness={{
                    score: 72,
                    strengths: ["Уверенный тон"],
                    gaps: ["Работа с ценой"],
                    recommendation: "Потренируйте возражения по цене.",
                    generatedAt: "2026-07-10T12:00:00Z",
                }}
                isLoading={false}
                errorMessage={null}
                onRefresh={onRefresh}
            />
        );

        expect(screen.getByRole("img", { name: "Готовность к звонку: 72 из 100" })).toBeTruthy();
        expect(screen.getByText("Уверенный тон")).toBeTruthy();
        expect(screen.getByText("Работа с ценой")).toBeTruthy();
        expect(screen.getByText("Потренируйте возражения по цене.")).toBeTruthy();
        expect(screen.getByText("Обновить")).toBeTruthy();
    });

    it("calls onRefresh when the update button is clicked", () => {
        render(
            <CompanyReadinessCard
                readiness={{ score: 50, strengths: [], gaps: [], recommendation: "Ок.", generatedAt: "2026-07-10T12:00:00Z" }}
                isLoading={false}
                errorMessage={null}
                onRefresh={onRefresh}
            />
        );

        fireEvent.click(screen.getByText("Обновить"));
        expect(onRefresh).toHaveBeenCalledTimes(1);
    });

    it("shows an error message alongside existing content", () => {
        render(
            <CompanyReadinessCard
                readiness={{ score: 50, strengths: [], gaps: [], recommendation: "Ок.", generatedAt: "2026-07-10T12:00:00Z" }}
                isLoading={false}
                errorMessage="AI service unavailable"
                onRefresh={onRefresh}
            />
        );

        expect(screen.getByText("AI service unavailable")).toBeTruthy();
    });

    it("shows an error message in the empty state", () => {
        render(
            <CompanyReadinessCard
                readiness={{ score: null, strengths: null, gaps: null, recommendation: null, generatedAt: null }}
                isLoading={false}
                errorMessage="AI service unavailable"
                onRefresh={onRefresh}
            />
        );

        expect(screen.getByText("AI service unavailable")).toBeTruthy();
        expect(screen.getByText("Проведите тренировку, чтобы получить оценку готовности.")).toBeTruthy();
    });
});
