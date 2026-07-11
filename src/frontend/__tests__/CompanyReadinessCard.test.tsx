import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { CompanyReadinessCard } from "@/features/companies/components/company-readiness-card";

describe("CompanyReadinessCard", () => {
    it("shows a loading state", () => {
        render(
            <CompanyReadinessCard
                readiness={undefined}
                isLoading={true}
                errorMessage={null}
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
            />
        );

        expect(screen.getByRole("img", { name: "Готовность к звонку: 72 из 100" })).toBeTruthy();
        expect(screen.getByText("Уверенный тон")).toBeTruthy();
        expect(screen.getByText("Работа с ценой")).toBeTruthy();
        expect(screen.getByText("Потренируйте возражения по цене.")).toBeTruthy();
    });

    it("shows an error message alongside existing cached content", () => {
        render(
            <CompanyReadinessCard
                readiness={{ score: 50, strengths: [], gaps: [], recommendation: "Ок.", generatedAt: "2026-07-10T12:00:00Z" }}
                isLoading={false}
                errorMessage="AI service unavailable"
            />
        );

        expect(screen.getByText("AI service unavailable")).toBeTruthy();
        // Cached score is still shown even though a refetch failed.
        expect(screen.getByRole("img", { name: "Готовность к звонку: 50 из 100" })).toBeTruthy();
    });

    it("shows a distinct error state (not the empty state) when there is no cached score", () => {
        render(
            <CompanyReadinessCard
                readiness={undefined}
                isLoading={false}
                errorMessage="AI service unavailable"
            />
        );

        expect(screen.getByText("Не удалось получить оценку готовности. Попробуйте позже.")).toBeTruthy();
        expect(screen.getByText("AI service unavailable")).toBeTruthy();
        // Must NOT mislead the user into thinking they simply haven't practiced.
        expect(screen.queryByText("Проведите тренировку, чтобы получить оценку готовности.")).toBeNull();
    });
});
