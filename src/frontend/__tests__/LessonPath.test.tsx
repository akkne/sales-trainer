import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { LessonPath } from "@/components/ui/LessonPath";
import type { LessonSummary } from "@/lib/hooks/useLesson";

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
            makeLesson({ lessonId: "l1", title: "Урок 1", status: "completed" }),
            makeLesson({ lessonId: "l2", title: "Урок 2", status: "available" }),
            makeLesson({ lessonId: "l3", title: "Урок 3", status: "locked" }),
        ];
        render(<LessonPath lessons={lessons} />);
        expect(screen.getByText("Урок 1")).toBeTruthy();
        expect(screen.getByText("Урок 2")).toBeTruthy();
        expect(screen.getByText("Урок 3")).toBeTruthy();
    });

    it("popover is hidden initially", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "Урок 1", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        expect(screen.queryByText("Приступить к прохождению")).toBeNull();
    });

    it("clicking an active node shows popover", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "Мой урок", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        // Click the node circle (role=button)
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        expect(screen.getByText("Приступить к прохождению")).toBeTruthy();
    });

    it("popover contains link to /session/[lessonId]", () => {
        const lessons = [
            makeLesson({ lessonId: "abc123", title: "Урок", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        const link = screen.getByText("Приступить к прохождению").closest("a");
        expect(link?.getAttribute("href")).toBe("/session/abc123");
    });

    it("clicking the same node again closes the popover", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", status: "available" }),
        ];
        render(<LessonPath lessons={lessons} />);
        const nodeBtn = screen.getAllByRole("button")[0];
        fireEvent.click(nodeBtn);
        expect(screen.getByText("Приступить к прохождению")).toBeTruthy();
        fireEvent.click(nodeBtn);
        expect(screen.queryByText("Приступить к прохождению")).toBeNull();
    });

    it("locked nodes do not show popover on click", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", status: "locked" }),
        ];
        render(<LessonPath lessons={lessons} />);
        // There should be no button role on a locked node
        const btns = screen.queryAllByRole("button");
        expect(btns.length).toBe(0);
        expect(screen.queryByText("Приступить к прохождению")).toBeNull();
    });

    it("only one popover open at a time", () => {
        const lessons = [
            makeLesson({ lessonId: "l1", title: "Урок 1", status: "available", sortOrder: 1 }),
            makeLesson({ lessonId: "l2", title: "Урок 2", status: "available", sortOrder: 2 }),
        ];
        render(<LessonPath lessons={lessons} />);
        const btns = screen.getAllByRole("button");
        fireEvent.click(btns[0]);
        expect(screen.queryAllByText("Приступить к прохождению")).toHaveLength(1);
        fireEvent.click(btns[1]);
        // First closes, second opens — still only 1
        expect(screen.queryAllByText("Приступить к прохождению")).toHaveLength(1);
    });
});
