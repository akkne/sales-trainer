import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CompanyBriefingCard } from "@/features/companies/components/company-briefing-card";

describe("CompanyBriefingCard", () => {
    const onGenerate = vi.fn();

    beforeEach(() => {
        onGenerate.mockReset();
    });

    it("shows a loading state", () => {
        render(
            <CompanyBriefingCard
                briefing={undefined}
                isLoading={true}
                isGenerating={false}
                errorMessage={null}
                onGenerate={onGenerate}
            />
        );

        expect(screen.getByText("Загрузка…")).toBeTruthy();
    });

    it("shows the empty state with a generate button when no briefing exists yet", () => {
        render(
            <CompanyBriefingCard
                briefing={undefined}
                isLoading={false}
                isGenerating={false}
                errorMessage={null}
                onGenerate={onGenerate}
            />
        );

        expect(screen.getByText("Сгенерировать")).toBeTruthy();
        fireEvent.click(screen.getByText("Сгенерировать"));
        expect(onGenerate).toHaveBeenCalledTimes(1);
    });

    it("renders the markdown content and generated-at timestamp when a briefing exists", () => {
        render(
            <CompanyBriefingCard
                briefing={{ content: "## Кто они\n- Тестовая компания", generatedAt: "2026-07-10T12:00:00Z" }}
                isLoading={false}
                isGenerating={false}
                errorMessage={null}
                onGenerate={onGenerate}
            />
        );

        expect(screen.getByText("Кто они")).toBeTruthy();
        expect(screen.getByText("Тестовая компания")).toBeTruthy();
        expect(screen.getByText("Обновить")).toBeTruthy();
    });

    it("calls onGenerate when the update button is clicked", () => {
        render(
            <CompanyBriefingCard
                briefing={{ content: "## Кто они", generatedAt: "2026-07-10T12:00:00Z" }}
                isLoading={false}
                isGenerating={false}
                errorMessage={null}
                onGenerate={onGenerate}
            />
        );

        fireEvent.click(screen.getByText("Обновить"));
        expect(onGenerate).toHaveBeenCalledTimes(1);
    });

    it("shows the generating state and disables the button", () => {
        render(
            <CompanyBriefingCard
                briefing={{ content: "## Кто они", generatedAt: "2026-07-10T12:00:00Z" }}
                isLoading={false}
                isGenerating={true}
                errorMessage={null}
                onGenerate={onGenerate}
            />
        );

        const button = screen.getByText("Генерируем…") as HTMLButtonElement;
        expect(button.disabled).toBe(true);
    });

    it("shows an error message alongside existing content", () => {
        render(
            <CompanyBriefingCard
                briefing={{ content: "## Кто они", generatedAt: "2026-07-10T12:00:00Z" }}
                isLoading={false}
                isGenerating={false}
                errorMessage="Не удалось сгенерировать шпаргалку"
                onGenerate={onGenerate}
            />
        );

        expect(screen.getByText("Не удалось сгенерировать шпаргалку")).toBeTruthy();
        expect(screen.getByText("Кто они")).toBeTruthy();
    });

    it("shows an error message in the empty state", () => {
        render(
            <CompanyBriefingCard
                briefing={undefined}
                isLoading={false}
                isGenerating={false}
                errorMessage="Не удалось сгенерировать шпаргалку"
                onGenerate={onGenerate}
            />
        );

        expect(screen.getByText("Не удалось сгенерировать шпаргалку")).toBeTruthy();
        expect(screen.getByText("Сгенерировать")).toBeTruthy();
    });
});
