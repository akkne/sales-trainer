import { describe, expect, it } from "vitest";
import { ApiError } from "@/shared/api/api-client";
import {
    CONTENT_REVIEW_FINDING_TITLES,
    describeFindingCode,
    describeFindingSeverity,
    describeItemStatus,
    describeJobStatus,
    findingSeverityTone,
    itemStatusTone,
} from "@/features/org-content-adaptation/constants/adaptation-dictionary";
import type {
    ContentAdaptationItemSummary,
    ContentAdaptationJobSummary,
} from "@/features/org-content-adaptation/types/adaptation";
import {
    describeItemActionFailure,
    describeStartFailure,
} from "@/features/org-content-adaptation/utils/adaptation-failure";
import {
    canRetryJob,
    collectLessonsWithAcceptedItems,
    countItemsAnsweredByModel,
    describeItemActions,
    describeResolvedBatch,
    findInitialItemId,
    findNextAwaitingItemId,
    groupQueueByLesson,
    shouldPollJob,
    sortQueueItems,
} from "@/features/org-content-adaptation/utils/proposal-queue";

/**
 * O12/O13 — batch adaptation and AI review (slice 6).
 *
 * The two things this file exists to pin: the proposal state machine (which item admits which verb,
 * and the three different reasons «Принять» can be impossible), and the closed vocabulary of seven
 * review codes.
 */

function buildItem(
    overrides: Partial<ContentAdaptationItemSummary> = {}
): ContentAdaptationItemSummary {
    return {
        id: "item-1",
        exerciseId: "exercise-1",
        lessonId: "lesson-a",
        lessonTitle: "Работа с ценой",
        exerciseType: "choose_option",
        orderInLesson: 1,
        status: "proposed",
        changeSummary: "Заменил абстрактную выгоду на срок внедрения.",
        findingCount: 0,
        hasBlockingFinding: false,
        changedFieldCount: 4,
        failureReason: null,
        resolvedAt: null,
        ...overrides,
    };
}

function buildSummary(
    overrides: Partial<ContentAdaptationJobSummary> = {}
): ContentAdaptationJobSummary {
    return {
        id: "job-1",
        mode: "tone_rewrite",
        stageKey: "closing",
        status: "awaiting_review",
        itemCount: 23,
        pendingCount: 0,
        awaitingReviewCount: 9,
        acceptedCount: 11,
        rejectedCount: 2,
        unchangedCount: 1,
        failedCount: 0,
        failureReason: null,
        createdAt: "2026-08-18T09:00:00Z",
        updatedAt: "2026-08-18T09:30:00Z",
        completedAt: null,
        ...overrides,
    };
}

describe("the proposal state machine", () => {
    it("lets a person accept a fresh rewrite", () => {
        const actions = describeItemActions("proposed", "tone_rewrite", false);

        expect(actions.canAccept).toBe(true);
        expect(actions.canReject).toBe(true);
        expect(actions.acceptBlockedReason).toBeNull();
    });

    it("never allows accepting a review finding — a diagnosis is not a patch", () => {
        const actions = describeItemActions("proposed", "quality_review", false);

        expect(actions.canAccept).toBe(false);
        expect(actions.canReject).toBe(true);
        expect(actions.acceptBlockedReason).toContain("диагноз");
    });

    it("refuses a stale rewrite with the re-run instruction, not with a merge", () => {
        const actions = describeItemActions("proposed", "tone_rewrite", true);

        expect(actions.canAccept).toBe(false);
        expect(actions.canReject).toBe(true);
        expect(actions.acceptBlockedReason).toContain("Запустите пакет заново");
    });

    it("offers no verb at all on an item somebody already answered", () => {
        for (const status of ["accepted", "rejected", "unchanged", "pending", "failed"]) {
            const actions = describeItemActions(status, "tone_rewrite", false);

            expect(actions.canAccept).toBe(false);
            expect(actions.canReject).toBe(false);
        }
    });

    it("keeps the three refusals distinct so a greyed-out button is never ambiguous", () => {
        const reasons = new Set([
            describeItemActions("accepted", "tone_rewrite", false).acceptBlockedReason,
            describeItemActions("proposed", "quality_review", false).acceptBlockedReason,
            describeItemActions("proposed", "tone_rewrite", true).acceptBlockedReason,
        ]);

        expect(reasons.size).toBe(3);
    });
});

describe("queue order", () => {
    const items = [
        buildItem({ id: "b-2", lessonId: "lesson-b", lessonTitle: "Дожим", orderInLesson: 2 }),
        buildItem({ id: "a-2", lessonId: "lesson-a", lessonTitle: "Аренда", orderInLesson: 2 }),
        buildItem({ id: "a-1", lessonId: "lesson-a", lessonTitle: "Аренда", orderInLesson: 1 }),
        buildItem({
            id: "b-1",
            lessonId: "lesson-b",
            lessonTitle: "Дожим",
            orderInLesson: 1,
        }),
    ];

    it("reads by lesson and then by position inside the lesson", () => {
        expect(sortQueueItems(items, "tone_rewrite").map((item) => item.id)).toEqual([
            "a-1",
            "a-2",
            "b-1",
            "b-2",
        ]);
    });

    it("lifts a blocking finding to the top of its own lesson, and no further", () => {
        const reviewItems = [
            ...items.filter((item) => item.id !== "b-2"),
            buildItem({
                id: "b-2",
                lessonId: "lesson-b",
                lessonTitle: "Дожим",
                orderInLesson: 2,
                hasBlockingFinding: true,
                findingCount: 2,
            }),
        ];

        expect(sortQueueItems(reviewItems, "quality_review").map((item) => item.id)).toEqual([
            "a-1",
            "a-2",
            "b-2",
            "b-1",
        ]);
    });

    it("does not lift blocking findings in rewrite mode, where the flag is never set", () => {
        const rewriteItems = [
            buildItem({ id: "a-1", orderInLesson: 1 }),
            buildItem({ id: "a-2", orderInLesson: 2, hasBlockingFinding: true }),
        ];

        expect(sortQueueItems(rewriteItems, "tone_rewrite").map((item) => item.id)).toEqual([
            "a-1",
            "a-2",
        ]);
    });

    it("cuts the sorted queue into its lessons", () => {
        const groups = groupQueueByLesson(items, "tone_rewrite");

        expect(groups.map((group) => group.lessonTitle)).toEqual(["Аренда", "Дожим"]);
        expect(groups[0].items.map((item) => item.id)).toEqual(["a-1", "a-2"]);
    });
});

describe("walking the queue", () => {
    const items = [
        buildItem({ id: "one", orderInLesson: 1, status: "accepted" }),
        buildItem({ id: "two", orderInLesson: 2, status: "proposed" }),
        buildItem({ id: "three", orderInLesson: 3, status: "unchanged" }),
        buildItem({ id: "four", orderInLesson: 4, status: "proposed" }),
    ];

    it("opens on the first item still waiting for an answer", () => {
        expect(findInitialItemId(items, "tone_rewrite")).toBe("two");
    });

    it("falls back to the first row when everything is answered", () => {
        const answered = items.map((item) => ({ ...item, status: "accepted" }));

        expect(findInitialItemId(answered, "tone_rewrite")).toBe("one");
    });

    it("skips resolved items when moving to the next one", () => {
        expect(findNextAwaitingItemId(items, "tone_rewrite", "two")).toBe("four");
    });

    it("wraps past the end of the queue once", () => {
        expect(findNextAwaitingItemId(items, "tone_rewrite", "four")).toBe("two");
    });

    it("returns null on the last unanswered item rather than pointing at itself", () => {
        const oneLeft = [
            buildItem({ id: "one", orderInLesson: 1, status: "accepted" }),
            buildItem({ id: "two", orderInLesson: 2, status: "proposed" }),
        ];

        expect(findNextAwaitingItemId(oneLeft, "tone_rewrite", "two")).toBeNull();
    });

    it("returns null for an empty queue", () => {
        expect(findInitialItemId([], "tone_rewrite")).toBeNull();
        expect(findNextAwaitingItemId([], "tone_rewrite", null)).toBeNull();
    });
});

describe("the batch as a whole", () => {
    it("counts what the model has answered as itemCount minus pendingCount", () => {
        expect(countItemsAnsweredByModel(buildSummary({ itemCount: 23, pendingCount: 11 }))).toBe(12);
    });

    it("never reports a negative progress if the counts disagree mid-sweep", () => {
        expect(countItemsAnsweredByModel(buildSummary({ itemCount: 2, pendingCount: 5 }))).toBe(0);
    });

    it("polls only while a worker still owns the batch", () => {
        expect(shouldPollJob("preparing")).toBe(true);
        for (const status of ["awaiting_review", "completed", "failed"]) {
            expect(shouldPollJob(status)).toBe(false);
        }
    });

    it("offers a retry only when something actually failed — retry answers 409 otherwise", () => {
        expect(canRetryJob(buildSummary({ failedCount: 0 }))).toBe(false);
        expect(canRetryJob(buildSummary({ failedCount: 3 }))).toBe(true);
    });

    it("names the three buckets of an answered batch", () => {
        expect(describeResolvedBatch(buildSummary())).toBe(
            "Все предложения разобраны: принято 11, отклонено 2, без изменений 1."
        );
    });

    it("leaves out the buckets that are zero", () => {
        expect(
            describeResolvedBatch(buildSummary({ acceptedCount: 4, rejectedCount: 0, unchangedCount: 0 }))
        ).toBe("Все предложения разобраны: принято 4.");
    });

    it("says so plainly when the model proposed nothing at all", () => {
        const summary = buildSummary({ acceptedCount: 0, rejectedCount: 0, unchangedCount: 0 });

        expect(describeResolvedBatch(summary)).toContain("не предложила ни одной правки");
    });

    it("collects the lessons an accepted rewrite has been written into, for publishing", () => {
        const lessons = collectLessonsWithAcceptedItems([
            buildItem({ id: "1", status: "accepted", lessonId: "lesson-a", lessonTitle: "Аренда" }),
            buildItem({ id: "2", status: "accepted", lessonId: "lesson-a", lessonTitle: "Аренда" }),
            buildItem({ id: "3", status: "rejected", lessonId: "lesson-b", lessonTitle: "Дожим" }),
        ]);

        expect(lessons).toEqual([{ lessonId: "lesson-a", lessonTitle: "Аренда", acceptedCount: 2 }]);
    });
});

describe("the seven review codes", () => {
    it("knows exactly the seven of ContentReviewFindingCodes and no eighth", () => {
        expect(Object.keys(CONTENT_REVIEW_FINDING_TITLES).sort()).toEqual(
            [
                "ambiguous_correct_answer",
                "answer_given_away",
                "banned_claim_rewarded",
                "missing_explanation",
                "multiple_correct_answers",
                "obvious_distractors",
                "unmeasurable_criteria",
            ].sort()
        );
    });

    it("gives each of the seven a title of its own", () => {
        const titles = new Set(Object.values(CONTENT_REVIEW_FINDING_TITLES));

        expect(titles.size).toBe(7);
    });

    it("prints a code outside the vocabulary as the code itself, never blank", () => {
        expect(describeFindingCode("hallucinated_code")).toBe("hallucinated_code");
    });

    it("names the two severities and tones a blocking one as bad", () => {
        expect(describeFindingSeverity("blocking")).toBe("Критично");
        expect(describeFindingSeverity("advisory")).toBe("Совет");
        expect(findingSeverityTone("blocking")).toBe("bad");
        expect(findingSeverityTone("advisory")).toBe("neutral");
    });

    it("falls back to the raw severity rather than guessing", () => {
        expect(describeFindingSeverity("catastrophic")).toBe("catastrophic");
        expect(findingSeverityTone("catastrophic")).toBe("neutral");
    });
});

describe("the status dictionary", () => {
    it("translates the four job statuses", () => {
        expect(describeJobStatus("preparing")).toBe("Готовим предложения");
        expect(describeJobStatus("awaiting_review")).toBe("Ждёт вашего ответа");
        expect(describeJobStatus("completed")).toBe("Разобрано");
        expect(describeJobStatus("failed")).toBe("Ошибка");
    });

    it("translates the six item statuses", () => {
        expect(describeItemStatus("pending")).toBe("в очереди");
        expect(describeItemStatus("proposed")).toBe("ждёт");
        expect(describeItemStatus("unchanged")).toBe("без изменений");
        expect(describeItemStatus("accepted")).toBe("принято");
        expect(describeItemStatus("rejected")).toBe("отклонено");
        expect(describeItemStatus("failed")).toBe("ошибка");
    });

    it("shows an unknown status verbatim on both axes", () => {
        expect(describeJobStatus("quarantined")).toBe("quarantined");
        expect(describeItemStatus("quarantined")).toBe("quarantined");
        expect(itemStatusTone("quarantined")).toBe("neutral");
    });
});

describe("refusals from the start route", () => {
    it("turns the oversized-stage 400 into advice that keeps the count", () => {
        const failure = describeStartFailure(
            new ApiError(400, {
                message:
                    "Stage 'closing' holds 412 exercises, which is above the per-batch ceiling of 60.",
            })
        );

        expect(failure.message).toContain("412 упражнений");
        expect(failure.message).toContain("Выберите этап поуже");
        expect(failure.isLiveBatchConflict).toBe(false);
    });

    it("never leaks the server's English sentence to the customer", () => {
        const failure = describeStartFailure(
            new ApiError(400, { message: "Stage 'closing' has no exercises to adapt." })
        );

        expect(failure.message).toBe("В этом этапе нет упражнений — переписывать нечего.");
    });

    it("flags a 409 as a live batch so the screen can link to it", () => {
        const failure = describeStartFailure(
            new ApiError(409, { message: "A live 'tone_rewrite' batch for stage 'closing' already exists." })
        );

        expect(failure.isLiveBatchConflict).toBe(true);
        expect(failure.message).toContain("уже идёт пакет");
    });

    it("degrades to a Russian sentence for a refusal shape it has never seen", () => {
        const failure = describeStartFailure(new ApiError(400, { message: "Something new." }));

        expect(failure.message).toBe("Этап выбран неверно — запрос отклонён.");
    });

    it("handles a transport failure that is not an ApiError at all", () => {
        const failure = describeStartFailure(new TypeError("network down"));

        expect(failure.message).toContain("Проверьте подключение");
        expect(failure.isLiveBatchConflict).toBe(false);
    });
});

describe("refusals from accept and reject", () => {
    it("answers a 409 with a re-run, never with a merge", () => {
        const message = describeItemActionFailure(new ApiError(409, { message: "stale" }));

        expect(message).toContain("Запустите пакет заново");
        expect(message).not.toContain("merge");
    });

    it("says the proposal is gone on a 404", () => {
        expect(describeItemActionFailure(new ApiError(404, {}))).toContain("не найдено");
    });
});
