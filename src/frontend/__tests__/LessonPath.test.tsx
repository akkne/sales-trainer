import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { LessonPath } from "@/shared/components/lesson-path";
import type { LessonSummary } from "@/features/exercise/hooks/use-lesson";

// Next.js Link mock
vi.mock("next/link", () => ({
    default: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) => (
        <a href={href} {...rest}>
            {children}
        </a>
    ),
}));

function makeLesson(
    overrides: Partial<LessonSummary> & { lessonId: string }
): LessonSummary {
    return {
        title: "Test Lesson",
        sortOrder: 1,
        difficultyLevel: 1,
        xpReward: 10,
        status: "locked",
        bestScore: 0,
        ...overrides,
    };
}

describe("LessonPath", () => {
    it("renders lesson titles", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "Первый звонок", status: "completed" }),
            makeLesson({ lessonId: "l2", title: "Второй звонок", status: "available" }),
            makeLesson({ lessonId: "l3", title: "Третий звонок", status: "locked" }),
        ];
        render(<LessonPath lessons={lessons} />);
        expect(screen.getByText("Первый звонок")).toBeTruthy();
        expect(screen.getByText("Второй звонок")).toBeTruthy();
        expect(screen.getByText("Третий звонок")).toBeTruthy();
    });

    it("popover is hidden initially", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "Первый звонок", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        expect(screen.queryByText("Продолжить")).toBeNull();
    });

    it("clicking an active node shows popover", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "Мой урок", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        // Click the node circle (role=button)
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        expect(screen.getByText("Продолжить")).toBeTruthy();
    });

    it("popover contains link to /session/[lessonId]", () => {
        const lessons = [
            makeLesson({ lessonId: "abc123", title: "Урок", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        const link = screen.getByText("Продолжить").closest("a");
        expect(link?.getAttribute("href")).toBe("/session/abc123");
    });

    it("clicking the same node again closes the popover", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        expect(screen.getByText("Продолжить")).toBeTruthy();
        fireEvent.click(nodeBtn);
        expect(screen.queryByText("Продолжить")).toBeNull();
    });

    it("locked nodes do not show popover on click", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", status: "locked" }),
        ];
        render(<LessonPath lessons={lessons} />);
        // Locked node renders a disabled button — clicking it must not open a popover
        const btns = screen.getAllByRole("button");
        expect(btns[0]).toHaveProperty("disabled", true);
        fireEvent.click(btns[0]);
        expect(screen.queryByText("Продолжить")).toBeNull();
    });

    it("only one popover open at a time", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "Первый звонок", status: "available", sortOrder: 1 }),
            makeLesson({ lessonId: "l2", title: "Урок 2", status: "available", sortOrder: 2 }),
        ];
        render(<LessonPath lessons={lessons} />);
        const btns = screen.getAllByRole("button");
        fireEvent.click(btns[0]);
        expect(screen.queryAllByText("Продолжить")).toHaveLength(1);
        fireEvent.click(btns[1]);
        // First closes, second opens — still only 1
        expect(screen.queryAllByText("Продолжить")).toHaveLength(1);
    });
});
