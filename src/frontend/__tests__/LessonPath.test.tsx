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
        orderInTopic: 1,
        kind: "practice",
        status: "locked",
        bestScore: 0,
        ...overrides,
    };
}

describe("LessonPath", () => {
    it("renders lesson titles", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "First Call", status: "completed" }),
            makeLesson({ lessonId: "l2", title: "Second Call", status: "available" }),
            makeLesson({ lessonId: "l3", title: "Third Call", status: "locked" }),
        ];
        render(<LessonPath lessons={lessons} />);
        expect(screen.getByText("First Call")).toBeTruthy();
        expect(screen.getByText("Second Call")).toBeTruthy();
        expect(screen.getByText("Third Call")).toBeTruthy();
    });

    it("popover is hidden initially", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "First Call", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        expect(screen.queryByText("Continue")).toBeNull();
    });

    it("clicking an active node shows popover", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "My Lesson", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        // Click the node circle (role=button)
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        expect(screen.getByText("Continue")).toBeTruthy();
    });

    it("popover contains link to /session/[lessonId]", () => {
        const lessons = [
            makeLesson({ lessonId: "abc123", title: "My Lesson", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        const link = screen.getByText("Continue").closest("a");
        expect(link?.getAttribute("href")).toBe("/session/abc123");
    });

    it("clicking the same node again closes the popover", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        expect(screen.getByText("Continue")).toBeTruthy();
        fireEvent.click(nodeBtn);
        expect(screen.queryByText("Continue")).toBeNull();
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
        expect(screen.queryByText("Continue")).toBeNull();
    });

    it("only one popover open at a time", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "First Call", status: "available", orderInTopic: 1 }),
            makeLesson({ lessonId: "l2", title: "Lesson 2", status: "available", orderInTopic: 2 }),
        ];
        render(<LessonPath lessons={lessons} />);
        const btns = screen.getAllByRole("button");
        fireEvent.click(btns[0]);
        expect(screen.queryAllByText("Continue")).toHaveLength(1);
        fireEvent.click(btns[1]);
        // First closes, second opens — still only 1
        expect(screen.queryAllByText("Continue")).toHaveLength(1);
    });
});
