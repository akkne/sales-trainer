import { describe, it, expect } from "vitest";

// Mirrors formatSessionDuration from app/session/[lessonId]/page.tsx
function formatSessionDuration(totalSeconds: number): string {
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    if (minutes === 0) return `${seconds} сек`;
    return `${minutes} мин ${seconds} сек`;
}

describe("formatSessionDuration", () => {
    it("returns seconds only when under a minute", () => {
        expect(formatSessionDuration(45)).toBe("45 сек");
    });

    it("returns '0 сек' for zero duration", () => {
        expect(formatSessionDuration(0)).toBe("0 сек");
    });

    it("returns minutes and seconds for exactly one minute", () => {
        expect(formatSessionDuration(60)).toBe("1 мин 0 сек");
    });

    it("returns minutes and seconds for 90 seconds", () => {
        expect(formatSessionDuration(90)).toBe("1 мин 30 сек");
    });

    it("returns minutes and seconds for 3 minutes 5 seconds", () => {
        expect(formatSessionDuration(185)).toBe("3 мин 5 сек");
    });

    it("returns minutes and seconds for exactly 10 minutes", () => {
        expect(formatSessionDuration(600)).toBe("10 мин 0 сек");
    });
});
