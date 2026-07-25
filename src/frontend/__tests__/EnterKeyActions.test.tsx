import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ExerciseActionFooter } from "@/features/exercise/components/exercise-action-footer";
import { ExerciseResultBanner } from "@/features/exercise/components/exercise-result-banner";
import { TheoryLessonPlayer } from "@/features/exercise/components/theory-lesson-player";
import type { TheoryCardContent } from "@/features/exercise/types/theory-card";

function pressEnter(target: Element | Window = document.body, extra: object = {}) {
    fireEvent.keyDown(target, { key: "Enter", ...extra });
}

describe("ExerciseActionFooter — Enter submits", () => {
    it("calls onSubmit on Enter when submission is allowed", () => {
        const onSubmit = vi.fn();
        render(<ExerciseActionFooter onSubmit={onSubmit} canSubmit={true} isSubmitting={false} />);
        pressEnter();
        expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    it("ignores Enter while submission is not allowed or already in flight", () => {
        const onSubmit = vi.fn();
        const { rerender } = render(
            <ExerciseActionFooter onSubmit={onSubmit} canSubmit={false} isSubmitting={false} />
        );
        pressEnter();
        rerender(<ExerciseActionFooter onSubmit={onSubmit} canSubmit={true} isSubmitting={true} />);
        pressEnter();
        expect(onSubmit).not.toHaveBeenCalled();
    });

    it("ignores held-key repeats", () => {
        const onSubmit = vi.fn();
        render(<ExerciseActionFooter onSubmit={onSubmit} canSubmit={true} isSubmitting={false} />);
        pressEnter(document.body, { repeat: true });
        expect(onSubmit).not.toHaveBeenCalled();
    });

    it("ignores plain Enter inside a textarea but accepts Ctrl+Enter", () => {
        const onSubmit = vi.fn();
        render(
            <div>
                <textarea aria-label="answer" />
                <ExerciseActionFooter onSubmit={onSubmit} canSubmit={true} isSubmitting={false} />
            </div>
        );
        const textarea = screen.getByLabelText("answer");
        pressEnter(textarea);
        expect(onSubmit).not.toHaveBeenCalled();
        pressEnter(textarea, { ctrlKey: true });
        expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    it("ignores Enter when a button is focused (the browser clicks it natively)", () => {
        const onSubmit = vi.fn();
        render(<ExerciseActionFooter onSubmit={onSubmit} canSubmit={true} isSubmitting={false} />);
        const submitButton = screen.getByText("Проверить").closest("button")!;
        submitButton.focus();
        pressEnter(submitButton);
        expect(onSubmit).not.toHaveBeenCalled();
    });
});

describe("ExerciseResultBanner — Enter continues", () => {
    it("calls onContinue on Enter", () => {
        const onContinue = vi.fn();
        render(
            <ExerciseResultBanner
                isCorrect={true}
                score={100}
                explanation={null}
                aiFeedback={null}
                xpEarned={10}
                onContinue={onContinue}
            />
        );
        pressEnter();
        expect(onContinue).toHaveBeenCalledTimes(1);
    });
});

describe("TheoryLessonPlayer — keyboard navigation", () => {
    const cards: TheoryCardContent[] = [
        { layout: "text", title: "Card one", body: "First body" },
        { layout: "text", title: "Card two", body: "Second body" },
    ];

    it("Enter advances to the next card, then completes on the last one", () => {
        const onComplete = vi.fn();
        render(
            <TheoryLessonPlayer cards={cards} onComplete={onComplete} isCompleting={false} onExit={vi.fn()} />
        );
        expect(screen.getByText("Card one")).toBeTruthy();

        pressEnter();
        expect(screen.getByText("Card two")).toBeTruthy();
        expect(onComplete).not.toHaveBeenCalled();

        pressEnter();
        expect(onComplete).toHaveBeenCalledTimes(1);
    });

    it("arrow keys page between cards without completing the lesson", () => {
        const onComplete = vi.fn();
        render(
            <TheoryLessonPlayer cards={cards} onComplete={onComplete} isCompleting={false} onExit={vi.fn()} />
        );

        fireEvent.keyDown(document.body, { key: "ArrowRight" });
        expect(screen.getByText("Card two")).toBeTruthy();

        // On the last card → must NOT fire completion.
        fireEvent.keyDown(document.body, { key: "ArrowRight" });
        expect(onComplete).not.toHaveBeenCalled();

        fireEvent.keyDown(document.body, { key: "ArrowLeft" });
        expect(screen.getByText("Card one")).toBeTruthy();
    });

    it("Enter is inert while completion is in flight", () => {
        const onComplete = vi.fn();
        render(
            <TheoryLessonPlayer
                cards={[cards[0]]}
                onComplete={onComplete}
                isCompleting={true}
                onExit={vi.fn()}
            />
        );
        pressEnter();
        expect(onComplete).not.toHaveBeenCalled();
    });
});
