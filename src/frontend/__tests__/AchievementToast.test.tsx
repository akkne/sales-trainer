import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, act } from "@testing-library/react";
import { AchievementToast, AchievementToastQueue } from "../components/ui/AchievementToast";

const ACHIEVEMENT = {
    key: "first_blood",
    iconEmoji: "🏆",
    title: "Первая победа",
    description: "Ответь правильно на первое упражнение",
};

describe("AchievementToast", () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });
    afterEach(() => {
        vi.useRealTimers();
    });

    it("renders achievement info", () => {
        render(<AchievementToast achievement={ACHIEVEMENT} onDismiss={vi.fn()} />);
        expect(screen.getByText(ACHIEVEMENT.title)).toBeTruthy();
        expect(screen.getByText(ACHIEVEMENT.description)).toBeTruthy();
        expect(screen.getByText(ACHIEVEMENT.iconEmoji)).toBeTruthy();
        expect(screen.getByText("Достижение разблокировано!")).toBeTruthy();
    });

    it("calls onDismiss after 4s auto-dismiss", async () => {
        const onDismiss = vi.fn();
        render(<AchievementToast achievement={ACHIEVEMENT} onDismiss={onDismiss} />);

        // advance 4s (auto-dismiss fires) + 350ms (slide-out animation)
        act(() => {
            vi.advanceTimersByTime(4000 + 350 + 50);
        });

        expect(onDismiss).toHaveBeenCalledOnce();
    });
});

describe("AchievementToastQueue", () => {
    it("renders nothing when queue is empty", () => {
        render(<AchievementToastQueue queue={[]} onDismiss={vi.fn()} />);
        expect(screen.queryByText("Достижение разблокировано!")).toBeNull();
    });

    it("renders only the first item in queue", () => {
        const queue = [
            { ...ACHIEVEMENT, key: "a1", title: "Achievement 1" },
            { ...ACHIEVEMENT, key: "a2", title: "Achievement 2" },
        ];
        render(<AchievementToastQueue queue={queue} onDismiss={vi.fn()} />);
        expect(screen.getByText("Achievement 1")).toBeTruthy();
        expect(screen.queryByText("Achievement 2")).toBeNull();
    });

    it("calls onDismiss with correct key", () => {
        vi.useFakeTimers();
        const onDismiss = vi.fn();
        const queue = [{ ...ACHIEVEMENT, key: "first_blood" }];
        render(<AchievementToastQueue queue={queue} onDismiss={onDismiss} />);

        act(() => {
            vi.advanceTimersByTime(4000 + 350 + 50);
        });

        expect(onDismiss).toHaveBeenCalledWith("first_blood");
        vi.useRealTimers();
    });
});
