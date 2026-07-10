import { describe, it, expect } from "vitest";
import { getFollowUpTone } from "@/features/companies/lib/company-followup";

const NOW = new Date("2026-07-10T12:00:00Z");

describe("getFollowUpTone", () => {
    it("returns null when there is no follow-up scheduled", () => {
        expect(getFollowUpTone(null, NOW)).toBeNull();
        expect(getFollowUpTone(undefined, NOW)).toBeNull();
    });

    it("returns 'overdue' for a past due date", () => {
        expect(getFollowUpTone("2026-07-09T12:00:00Z", NOW)).toBe("overdue");
    });

    it("returns 'due' for a date within the next 24 hours", () => {
        expect(getFollowUpTone("2026-07-11T00:00:00Z", NOW)).toBe("due");
    });

    it("returns null for a date more than 24 hours away", () => {
        expect(getFollowUpTone("2026-07-15T12:00:00Z", NOW)).toBeNull();
    });

    it("returns 'due' for a due date exactly now", () => {
        expect(getFollowUpTone("2026-07-10T12:00:00Z", NOW)).toBe("due");
    });

    it("returns null for an invalid date string", () => {
        expect(getFollowUpTone("not-a-date", NOW)).toBeNull();
    });
});
