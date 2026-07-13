import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ChooseOptionExercise } from "@/features/exercise/components/choose-option-exercise";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";

const CONTENT = {
    situation: "Client says: \"That's too expensive\"",
    options: [
        { text: "Understood, we'll lower the price", is_correct: false },
        { text: "Let's walk through the value", is_correct: true },
        { text: "OK, goodbye", is_correct: false },
    ],
    explanation: "A price objection is an opportunity to reveal value.",
};

function makeResult(overrides: Partial<ExerciseSubmissionResult> = {}): ExerciseSubmissionResult {
    return {
        isCorrect: true,
        score: 10,
        explanation: null,
        aiFeedback: null,
        xpEarned: 10,
        ...overrides,
    } as ExerciseSubmissionResult;
}

describe("ChooseOptionExercise", () => {
    it("renders situation bubble and all options", () => {
        render(
            <ChooseOptionExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
            />
        );
        expect(screen.getByText(CONTENT.situation)).toBeTruthy();
        for (const option of CONTENT.options) {
            expect(screen.getByText(option.text)).toBeTruthy();
        }
    });

    it("submit button is disabled until an option is selected", () => {
        render(
            <ChooseOptionExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
            />
        );
        const submitButton = screen.getByText("Проверить").closest("button")!;
        expect(submitButton.disabled).toBe(true);

        fireEvent.click(screen.getByText(CONTENT.options[1].text));
        expect(submitButton.disabled).toBe(false);
    });

    it("calls onSubmit with the selected option index", () => {
        const onSubmit = vi.fn();
        render(
            <ChooseOptionExercise
                content={CONTENT}
                onSubmit={onSubmit}
                isSubmitting={false}
            />
        );
        fireEvent.click(screen.getByText(CONTENT.options[2].text));
        fireEvent.click(screen.getByText("Проверить"));
        expect(onSubmit).toHaveBeenCalledWith({ selectedOptionIndex: 2 });
    });

    it("shows skip button only when onSkip provided", () => {
        const { rerender } = render(
            <ChooseOptionExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
            />
        );
        expect(screen.queryByText("Пропустить")).toBeNull();

        rerender(
            <ChooseOptionExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                onSkip={vi.fn()}
                isSubmitting={false}
            />
        );
        expect(screen.getByText("Пропустить")).toBeTruthy();
    });

    it("locks options after a result is submitted", () => {
        render(
            <ChooseOptionExercise
                content={CONTENT}
                onSubmit={vi.fn()}
                isSubmitting={false}
                submittedResult={makeResult()}
            />
        );
        const optionButton = screen.getByText(CONTENT.options[0].text).closest("button")!;
        expect(optionButton.disabled).toBe(true);
    });
});
