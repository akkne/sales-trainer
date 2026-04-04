import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";

// Extracted countdown logic for testing — mirrors the hook in league/page.tsx
function computeCountdown(weekEndDate: string, now: number): string {
    const endMs = new Date(weekEndDate).getTime();
    const diffMs = endMs - now;
    if (diffMs <= 0) return "Неделя завершена";
    const totalHours = Math.floor(diffMs / 3_600_000);
    const days = Math.floor(totalHours / 24);
    const hours = totalHours % 24;
    if (days > 0) return `${days}д ${hours}ч`;
    const minutes = Math.floor((diffMs % 3_600_000) / 60_000);
    return `${hours}ч ${minutes}м`;
}

describe("computeCountdown", () => {
    it("returns 'Неделя завершена' when end date is in the past", () => {
        const past = new Date(Date.now() - 3_600_000).toISOString();
        expect(computeCountdown(past, Date.now())).toBe("Неделя завершена");
    });

    it("returns 'Неделя завершена' when end date equals now", () => {
        const now = Date.now();
        expect(computeCountdown(new Date(now).toISOString(), now)).toBe("Неделя завершена");
    });

    it("returns days and hours when more than 24h remaining", () => {
        const now = Date.now();
        const future = new Date(now + 2 * 24 * 3_600_000 + 3 * 3_600_000).toISOString();
        expect(computeCountdown(future, now)).toBe("2д 3ч");
    });

    it("returns hours and minutes when less than 24h remaining", () => {
        const now = Date.now();
        const future = new Date(now + 5 * 3_600_000 + 30 * 60_000).toISOString();
        expect(computeCountdown(future, now)).toBe("5ч 30м");
    });

    it("returns 1д 0ч when exactly 24h remaining", () => {
        const now = Date.now();
        const future = new Date(now + 24 * 3_600_000).toISOString();
        expect(computeCountdown(future, now)).toBe("1д 0ч");
    });

    it("returns 0ч Xм for sub-hour remaining", () => {
        const now = Date.now();
        const future = new Date(now + 45 * 60_000).toISOString();
        expect(computeCountdown(future, now)).toBe("0ч 45м");
    });
});
