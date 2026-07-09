import { describe, it, expect } from "vitest";
import {
    pluralizeRu,
    pluralizeCompanies,
    companiesCountLabel,
    formatDateRu,
    relativeTimeRu,
} from "@/features/companies/lib/format";

describe("pluralizeRu", () => {
    const forms: [string, string, string] = ["звонок", "звонка", "звонков"];

    it("picks the 'one' form for 1, 21", () => {
        expect(pluralizeRu(1, forms)).toBe("звонок");
        expect(pluralizeRu(21, forms)).toBe("звонок");
    });

    it("picks the 'few' form for 2", () => {
        expect(pluralizeRu(2, forms)).toBe("звонка");
    });

    it("picks the 'many' form for 5", () => {
        expect(pluralizeRu(5, forms)).toBe("звонков");
    });

    it("picks the 'many' form for 11 (not 'one', despite ending in 1)", () => {
        expect(pluralizeRu(11, forms)).toBe("звонков");
    });

    it("picks the 'one' form for 21 (ends in 1, not 11)", () => {
        expect(pluralizeRu(21, forms)).toBe("звонок");
    });
});

describe("pluralizeCompanies", () => {
    it("uses компания for 1, 21, 31 (ends in 1, not 11)", () => {
        expect(pluralizeCompanies(1)).toBe("компания");
        expect(pluralizeCompanies(21)).toBe("компания");
        expect(pluralizeCompanies(31)).toBe("компания");
    });

    it("uses компании for 2-4, 22-24 (not 12-14)", () => {
        expect(pluralizeCompanies(2)).toBe("компании");
        expect(pluralizeCompanies(3)).toBe("компании");
        expect(pluralizeCompanies(4)).toBe("компании");
        expect(pluralizeCompanies(22)).toBe("компании");
    });

    it("uses компаний for 0, 5-20, 11-14, 25", () => {
        expect(pluralizeCompanies(0)).toBe("компаний");
        expect(pluralizeCompanies(5)).toBe("компаний");
        expect(pluralizeCompanies(11)).toBe("компаний");
        expect(pluralizeCompanies(12)).toBe("компаний");
        expect(pluralizeCompanies(14)).toBe("компаний");
        expect(pluralizeCompanies(25)).toBe("компаний");
    });
});

describe("companiesCountLabel", () => {
    it("composes count + plural form", () => {
        expect(companiesCountLabel(1)).toBe("1 компания");
        expect(companiesCountLabel(3)).toBe("3 компании");
        expect(companiesCountLabel(11)).toBe("11 компаний");
    });
});

describe("formatDateRu", () => {
    it("formats an ISO date as 'd MMM yyyy' in Russian", () => {
        expect(formatDateRu("2026-07-09T10:00:00.000Z")).toMatch(/^9 июл 2026$/);
    });
});

describe("relativeTimeRu", () => {
    it("returns 'только что' for a moment ago", () => {
        expect(relativeTimeRu(new Date().toISOString())).toBe("только что");
    });

    it("returns minutes-ago phrasing", () => {
        const tenMinAgo = new Date(Date.now() - 10 * 60_000).toISOString();
        expect(relativeTimeRu(tenMinAgo)).toBe("10 мин назад");
    });

    it("returns hours-ago phrasing", () => {
        const threeHoursAgo = new Date(Date.now() - 3 * 60 * 60_000).toISOString();
        expect(relativeTimeRu(threeHoursAgo)).toBe("3 ч назад");
    });

    it("returns days-ago phrasing", () => {
        const twoDaysAgo = new Date(Date.now() - 2 * 24 * 60 * 60_000).toISOString();
        expect(relativeTimeRu(twoDaysAgo)).toBe("2 д назад");
    });

    it("falls back to the absolute date beyond ~30 days", () => {
        const oldDate = new Date(Date.now() - 40 * 24 * 60 * 60_000);
        expect(relativeTimeRu(oldDate.toISOString())).toBe(formatDateRu(oldDate.toISOString()));
    });
});
