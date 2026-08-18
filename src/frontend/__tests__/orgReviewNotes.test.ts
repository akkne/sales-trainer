import { describe, expect, it } from "vitest";
import type { DialogReviewNote } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import {
    buildReviewNoteThread,
    isAwaitingVerdict,
    type ReviewThreadContext,
} from "@/features/org-dialogs/lib/review-note-thread";
import {
    buildVisibleReviewNotes,
    countOpenDisputes,
    selectReviewQueue,
} from "@/features/org-dialogs/lib/review-queues";

const ADMINISTRATOR_ID = "aaaa1111-0000-0000-0000-000000000000";
const OTHER_ADMINISTRATOR_ID = "bbbb2222-0000-0000-0000-000000000000";
const MANAGER_ID = "cccc3333-0000-0000-0000-000000000000";

function note(overrides: Partial<DialogReviewNote>): DialogReviewNote {
    return {
        id: "note-1",
        kind: "score_dispute",
        status: "open",
        sessionId: "65f0c2b3a1112223334444a5",
        dialogModeKey: "tough-buyer",
        subjectUserId: MANAGER_ID,
        subjectDisplayName: "Иванов А.",
        authorUserId: MANAGER_ID,
        authorDisplayName: "Иванов А.",
        quotedFromMessageIndex: 5,
        quotedToMessageIndex: 6,
        quotedText: "Хорошо, могу дать 15% скидки.",
        comment: "Клиент сам предложил скидку, я её не отдавал.",
        disputedScore: 40,
        resolution: null,
        adjustedScore: null,
        resolvedBy: null,
        resolvedAt: null,
        createdAt: "2026-08-20T09:00:00Z",
        updatedAt: "2026-08-20T09:00:00Z",
        ...overrides,
    };
}

const CONTEXT: ReviewThreadContext = {
    currentUserId: ADMINISTRATOR_ID,
    memberNamesByUserId: new Map([[OTHER_ADMINISTRATOR_ID, "Петрова М."]]),
};

/**
 * Block 40.20, screen O7. A disputed score is an argument between two people, and the row that
 * stores it keeps one side as prose and the other scattered across four columns. This is where it
 * becomes two readable turns again.
 */
describe("a disputed score read as a conversation", () => {
    it("opens with the manager's own words, on the manager's side", () => {
        const [opening] = buildReviewNoteThread(note({}), CONTEXT);

        expect(opening.side).toBe("manager");
        expect(opening.authorLabel).toBe("Иванов А.");
        expect(opening.isCurrentUser).toBe(false);
        expect(opening.caption).toBe("оспаривает 40 баллов");
        expect(opening.body).toBe("Клиент сам предложил скидку, я её не отдавал.");
    });

    it("shows only the complaint while the dispute is open", () => {
        expect(buildReviewNoteThread(note({}), CONTEXT)).toHaveLength(1);
        expect(isAwaitingVerdict(note({}))).toBe(true);
    });

    it("adds the verdict as a second turn once it is given", () => {
        const resolved = note({
            status: "upheld",
            resolution: "Согласен, вопрос об объёме был задан.",
            adjustedScore: 62,
            resolvedBy: ADMINISTRATOR_ID,
            resolvedAt: "2026-08-21T10:00:00Z",
        });

        const [, verdict] = buildReviewNoteThread(resolved, CONTEXT);

        expect(verdict.side).toBe("organization");
        expect(verdict.caption).toBe("Согласиться · справедливая оценка 62");
        expect(verdict.body).toBe("Согласен, вопрос об объёме был задан.");
        expect(isAwaitingVerdict(resolved)).toBe(false);
    });

    it("marks the reader's own turn, and does not mark another administrator's", () => {
        const ruledByMe = note({
            status: "rejected",
            resolution: "Оценка выставлена верно.",
            resolvedBy: ADMINISTRATOR_ID,
            resolvedAt: "2026-08-21T10:00:00Z",
        });
        const ruledBySomebodyElse = note({
            status: "rejected",
            resolution: "Оценка выставлена верно.",
            resolvedBy: OTHER_ADMINISTRATOR_ID,
            resolvedAt: "2026-08-21T10:00:00Z",
        });

        expect(buildReviewNoteThread(ruledByMe, CONTEXT)[1]).toMatchObject({
            authorLabel: "Вы",
            isCurrentUser: true,
        });
        expect(buildReviewNoteThread(ruledBySomebodyElse, CONTEXT)[1]).toMatchObject({
            authorLabel: "Петрова М.",
            isCurrentUser: false,
        });
    });

    it("says «оспаривает оценку» when the frozen grade is missing rather than «оспаривает null»", () => {
        expect(buildReviewNoteThread(note({ disputedScore: null }), CONTEXT)[0].caption).toBe(
            "оспаривает оценку"
        );
    });
});

describe("a coaching note read as a conversation", () => {
    const coachingNote = note({
        id: "note-2",
        kind: "coaching_note",
        status: "open",
        authorUserId: ADMINISTRATOR_ID,
        authorDisplayName: "Сидорова Е.",
        comment: "Скидка отдана до того, как выяснили объём.",
        disputedScore: 40,
    });

    it("starts on the organization's side and names the reader as themselves", () => {
        const [opening] = buildReviewNoteThread(coachingNote, CONTEXT);

        expect(opening.side).toBe("organization");
        expect(opening.authorLabel).toBe("Вы");
        expect(opening.isCurrentUser).toBe(true);
        expect(opening.caption).toBe("прислал заметку по разговору");
    });

    it("gains the manager's turn when they have read it", () => {
        const turns = buildReviewNoteThread(
            { ...coachingNote, status: "acknowledged", resolvedAt: "2026-08-21T08:00:00Z" },
            CONTEXT
        );

        expect(turns).toHaveLength(2);
        expect(turns[1]).toMatchObject({
            side: "manager",
            authorLabel: "Иванов А.",
            caption: "прочитал заметку",
            body: null,
        });
    });

    it("is never waiting on a verdict — only a dispute is", () => {
        expect(isAwaitingVerdict(coachingNote)).toBe(false);
    });
});

describe("the three queues of O7", () => {
    const openDispute = note({ id: "open-dispute" });
    const ruledDispute = note({
        id: "ruled-dispute",
        status: "rejected",
        resolution: "Оценка выставлена верно.",
        resolvedBy: ADMINISTRATOR_ID,
        resolvedAt: "2026-08-21T10:00:00Z",
    });
    const coachingNote = note({ id: "coaching-note", kind: "coaching_note" });
    const allNotes = [openDispute, ruledDispute, coachingNote];

    it("splits the notes the way the tabs promise", () => {
        expect(selectReviewQueue(allNotes, "open").map((row) => row.id)).toEqual(["open-dispute"]);
        expect(selectReviewQueue(allNotes, "disputes").map((row) => row.id)).toEqual([
            "open-dispute",
            "ruled-dispute",
        ]);
        expect(selectReviewQueue(allNotes, "notes").map((row) => row.id)).toEqual([
            "coaching-note",
        ]);
        expect(countOpenDisputes(allNotes)).toBe(1);
    });

    /**
     * The notification `DialogReviewDisputed` mints `/admin/dialog-reviews?note={id}`, slice 0
     * redirects it to `/org/reviews?note={id}`, and this is where that id stops being a query
     * parameter and becomes the card the person came to read.
     */
    it("pins the note a notification named to the top of whatever queue is open", () => {
        expect(
            buildVisibleReviewNotes(allNotes, "open", "coaching-note").map((row) => row.id)
        ).toEqual(["coaching-note", "open-dispute"]);
    });

    it("does not show the pinned note twice when the open queue already contains it", () => {
        expect(
            buildVisibleReviewNotes(allNotes, "open", "open-dispute").map((row) => row.id)
        ).toEqual(["open-dispute"]);
    });

    it("still finds a dispute that was ruled on since the notification was sent", () => {
        expect(
            buildVisibleReviewNotes(allNotes, "open", "ruled-dispute").map((row) => row.id)
        ).toEqual(["ruled-dispute", "open-dispute"]);
    });

    it("opens the queue, not an error, for a note this organization does not have", () => {
        expect(buildVisibleReviewNotes(allNotes, "open", "someone-elses-note").map((row) => row.id))
            .toEqual(["open-dispute"]);
        expect(buildVisibleReviewNotes(allNotes, "open", null).map((row) => row.id)).toEqual([
            "open-dispute",
        ]);
    });
});
