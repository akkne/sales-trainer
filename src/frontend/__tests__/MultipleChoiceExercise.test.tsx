import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MultipleChoiceExercise } from "@/features/exercise/components/multiple-choice-exercise";

const CONTENT = {
    situation: "Клиент говорит: «Слишком дорого»",
    question: "Как лучше ответить?",
    options: ["Понял, снизим цену", "Давайте разберём ценность", "Ок, до свидания"],
    correctOptionIndex: 1,
};

describe("MultipleChoiceExercise", () => {
    it("renders situation bubble and question", () => {
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
            />
        );
        expect(screen.getByText(CONTENT.situation)).toBeTruthy();
        expect(screen.getByText(CONTENT.question)).toBeTruthy();
    });

    it("renders all options", () => {
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
            />
        );
        CONTENT.options.forEach((opt) => {
            expect(screen.getByText(opt)).toBeTruthy();
        });
    });

    it("check button is disabled until an option is selected", () => {
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
            />
        );
        const checkBtn = screen.getByText("Проверить");
        expect((checkBtn as HTMLButtonElement).disabled).toBe(true);
    });

    it("check button becomes enabled after selecting an option", () => {
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
            />
        );
        fireEvent.click(screen.getByText(CONTENT.options[0]));
        const checkBtn = screen.getByText("Проверить");
        expect((checkBtn as HTMLButtonElement).disabled).toBe(false);
    });

    it("calls onSubmit with correct index when check is clicked", () => {
        const onSubmit = vi.fn();
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={onSubmit}
                isSubmitting={false}
            />
        );
        fireEvent.click(screen.getByText(CONTENT.options[1]));
        fireEvent.click(screen.getByText("Проверить"));
        expect(onSubmit).toHaveBeenCalledWith({ selectedOptionIndex: 1 });
    });

    it("does NOT render skip button when onSkip is not provided", () => {
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
            />
        );
        expect(screen.queryByText("Пропустить")).toBeNull();
    });

    it("renders skip button when onSkip is provided", () => {
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                onSkip={vi.fn()}
                isSubmitting={false}
            />
        );
        expect(screen.getByText("Пропустить")).toBeTruthy();
    });

    it("calls onSkip when skip button is clicked", () => {
        const onSkip = vi.fn();
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                onSkip={onSkip}
                isSubmitting={false}
            />
        );
        fireEvent.click(screen.getByText("Пропустить"));
        expect(onSkip).toHaveBeenCalledOnce();
    });

    it("shows loading text when isSubmitting", () => {
        render(
            <MultipleChoiceExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={true}
            />
        );
        expect(screen.getByText("Проверяем...")).toBeTruthy();
    });
});
