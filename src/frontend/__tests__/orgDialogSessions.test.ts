import { describe, expect, it } from "vitest";
import {
    DEFAULT_PANEL_SCORE_CEILING,
    formatPanelScore,
    resolvePanelScoreTone,
    toApiMaximumScore,
    toPanelScore,
} from "@/features/org-dialogs/lib/dialog-score";
import {
    buildDialogSessionQueryPath,
    DIALOG_SESSION_PAGE_SIZE,
    isDialogSessionLimitAtMaximum,
    MAXIMUM_DIALOG_SESSION_PAGE_SIZE,
    nextDialogSessionLimit,
} from "@/features/org-dialogs/lib/dialog-session-query";
import {
    formatDialogMoment,
    formatDialogMomentInFull,
} from "@/features/org-dialogs/lib/format-dialog-moment";
import {
    buildMemberNamesByUserId,
    resolveMemberLabel,
} from "@/features/org-dialogs/lib/team-member-labels";

/**
 * Block 40.20, screen O5. The two scales of one grade are the thing most likely to be broken by a
 * later change here: ai-service grades 0–10 and filters on that, learning-service records the same
 * grade ×10 and every dispute argues about *that* number.
 */
describe("the panel's grade scale", () => {
    it("shows an ai-service grade on learning-service's scale", () => {
        expect(toPanelScore(4)).toBe(40);
        expect(toPanelScore(10)).toBe(100);
        expect(toPanelScore(0)).toBe(0);
    });

    it("keeps an ungraded conversation ungraded rather than calling it zero", () => {
        expect(toPanelScore(null)).toBeNull();
        expect(toPanelScore(undefined)).toBeNull();
        expect(formatPanelScore(null)).toBe("—");
        expect(formatPanelScore(0)).toBe("0");
    });

    it("sends the ceiling back on the scale the endpoint filters with", () => {
        expect(toApiMaximumScore(DEFAULT_PANEL_SCORE_CEILING)).toBe(6);
        expect(toApiMaximumScore(100)).toBe(10);
    });

    it("never lets a ceiling admit a conversation graded above it", () => {
        // 65 on the panel's scale is not expressible, and rounding up would return 70s.
        expect(toApiMaximumScore(65)).toBe(6);
    });

    it("colours a bad conversation as bad and a good one as good", () => {
        expect(resolvePanelScoreTone(30)).toBe("bad");
        expect(resolvePanelScoreTone(50)).toBe("warn");
        expect(resolvePanelScoreTone(90)).toBe("good");
        expect(resolvePanelScoreTone(null)).toBe("neutral");
    });
});

describe("the dialog-session request", () => {
    it("omits a filter that is not set rather than sending it empty", () => {
        expect(
            buildDialogSessionQueryPath({
                userId: null,
                modeId: null,
                maximumPanelScore: null,
                limit: 25,
            })
        ).toBe("/admin/dialog-sessions?limit=25");
    });

    it("carries every filter the endpoint actually has", () => {
        expect(
            buildDialogSessionQueryPath({
                userId: "8f0c2b3a-1111-2222-3333-444455556666",
                modeId: "1a2b3c4d-5555-6666-7777-888899990000",
                maximumPanelScore: 60,
                limit: 50,
            })
        ).toBe(
            "/admin/dialog-sessions?userId=8f0c2b3a-1111-2222-3333-444455556666" +
                "&modeId=1a2b3c4d-5555-6666-7777-888899990000&maxScore=6&limit=50"
        );
    });

    it("grows by a page and stops where the server stops", () => {
        expect(nextDialogSessionLimit(25)).toBe(25 + DIALOG_SESSION_PAGE_SIZE);
        expect(nextDialogSessionLimit(90)).toBe(MAXIMUM_DIALOG_SESSION_PAGE_SIZE);
        expect(nextDialogSessionLimit(MAXIMUM_DIALOG_SESSION_PAGE_SIZE)).toBe(
            MAXIMUM_DIALOG_SESSION_PAGE_SIZE
        );
        expect(isDialogSessionLimitAtMaximum(75)).toBe(false);
        expect(isDialogSessionLimitAtMaximum(MAXIMUM_DIALOG_SESSION_PAGE_SIZE)).toBe(true);
    });
});

describe("when a conversation happened", () => {
    const now = new Date(2026, 7, 19, 18, 0, 0);

    it("puts a clock on today and yesterday", () => {
        expect(formatDialogMoment(new Date(2026, 7, 19, 9, 5).toISOString(), now)).toBe(
            "сегодня, 09:05"
        );
        expect(formatDialogMoment(new Date(2026, 7, 18, 14, 20).toISOString(), now)).toBe(
            "вчера, 14:20"
        );
    });

    it("puts a date on anything older, and a year only when it is another year", () => {
        expect(formatDialogMoment(new Date(2026, 7, 11, 14, 20).toISOString(), now)).toBe("11 авг");
        expect(formatDialogMoment(new Date(2025, 7, 11, 14, 20).toISOString(), now)).toBe(
            "11 авг 2025"
        );
    });

    it("renders nothing at all for a timestamp it cannot parse", () => {
        expect(formatDialogMoment("not-a-date", now)).toBe("");
        expect(formatDialogMomentInFull("not-a-date")).toBe("");
    });
});

describe("naming the manager a conversation belongs to", () => {
    const memberNamesByUserId = buildMemberNamesByUserId([
        { userId: "user-1", displayName: "Иванов А.", isActiveMember: true },
        { userId: "user-2", displayName: "Кузьма О.", isActiveMember: false },
    ]);

    it("uses the team directory when it knows the person", () => {
        expect(resolveMemberLabel("user-1", memberNamesByUserId)).toBe("Иванов А.");
    });

    it("keeps somebody the directory has never seen distinguishable", () => {
        expect(resolveMemberLabel("abcdef0123456789", memberNamesByUserId)).toBe(
            "Без имени · abcdef01"
        );
    });
});
