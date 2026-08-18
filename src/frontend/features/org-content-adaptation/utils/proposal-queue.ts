import { STALE_ITEM_EXPLANATION } from "@/features/org-content-adaptation/constants/adaptation-dictionary";
import type {
    ContentAdaptationItemSummary,
    ContentAdaptationJobSummary,
} from "@/features/org-content-adaptation/types/adaptation";

/**
 * The queue's rules, kept out of the components that draw it (O13,
 * docs/TENANCY/ADMIN_UI_DESIGN.md §2; docs/CONTENT_PIPELINE.md §6a).
 *
 * <b>Nothing here decides anything the server has not already decided.</b> The state machine below
 * only says which button a person may press; whether a proposal may be applied is settled on the
 * accept route, which re-hashes the exercise and answers 409 when it has moved. Disabling the
 * button early is a courtesy, not the guard.
 *
 * <b>And there is no "answer everything" function in this file.</b> «Применить всё» is auto-apply
 * with a person's name attached: the whole value of a batch is that somebody read each rewrite
 * before it became their team's content, so the client offers no verb the backend refuses to serve.
 */

/** The one item status that is still waiting for a person. Mirrors `ContentAdaptationItemStatuses.Unresolved`. */
export const AWAITING_ANSWER_ITEM_STATUS = "proposed";

/** The one job status a background worker still owns, and therefore the one worth polling. */
export const WORKER_OWNED_JOB_STATUS = "preparing";

export interface QueueLessonGroup {
    lessonId: string;
    lessonTitle: string;
    items: ContentAdaptationItemSummary[];
}

/** Whether this row still needs an answer. Everything else is resolved, by a person or by the model. */
export function isAwaitingAnswer(item: ContentAdaptationItemSummary): boolean {
    return item.status === AWAITING_ANSWER_ITEM_STATUS;
}

/**
 * Reading order: by lesson, then by position inside the lesson — the order the lesson is played in,
 * so a person reviewing it reads it the way a manager will meet it.
 *
 * In `quality_review` a blocking finding is lifted to the top of **its own lesson group** and no
 * further: a queue of sixty advisory notes must not bury the one saying that the correct answer
 * teaches a forbidden promise, but hoisting it out of its lesson would break the reading order that
 * makes the rest of the queue answerable.
 */
export function sortQueueItems(
    items: readonly ContentAdaptationItemSummary[],
    mode: string
): ContentAdaptationItemSummary[] {
    const liftsBlockingFindings = mode === "quality_review";

    return [...items].sort((left, right) => {
        const byLessonTitle = left.lessonTitle.localeCompare(right.lessonTitle, "ru");
        if (byLessonTitle !== 0) return byLessonTitle;

        if (left.lessonId !== right.lessonId) return left.lessonId.localeCompare(right.lessonId);

        if (liftsBlockingFindings && left.hasBlockingFinding !== right.hasBlockingFinding) {
            return left.hasBlockingFinding ? -1 : 1;
        }

        return left.orderInLesson - right.orderInLesson;
    });
}

/** The sorted queue cut into the lessons it came from — the left column's headings. */
export function groupQueueByLesson(
    items: readonly ContentAdaptationItemSummary[],
    mode: string
): QueueLessonGroup[] {
    const groups: QueueLessonGroup[] = [];

    for (const item of sortQueueItems(items, mode)) {
        const lastGroup = groups[groups.length - 1];
        if (lastGroup !== undefined && lastGroup.lessonId === item.lessonId) {
            lastGroup.items.push(item);
            continue;
        }

        groups.push({ lessonId: item.lessonId, lessonTitle: item.lessonTitle, items: [item] });
    }

    return groups;
}

export interface ItemActionAvailability {
    canAccept: boolean;
    canReject: boolean;
    /** Why accepting is impossible, when it is — printed above the button, never swallowed. */
    acceptBlockedReason: string | null;
}

const NOT_AWAITING_REASON = "Это предложение уже разобрано.";
const REVIEW_MODE_REASON =
    "Замечание нельзя применить: это диагноз, а не правка. Исправление — обычное редактирование упражнения.";
const STALE_REASON = STALE_ITEM_EXPLANATION;

/**
 * Which of the two verbs this item admits.
 *
 * Three separate refusals, and they are not interchangeable: a review finding has nothing to apply
 * **ever** (the database refuses it too), a stale rewrite would be refused with 409 by the accept
 * route, and an already-answered item is simply done. Collapsing them into one greyed-out button
 * would leave a person guessing which of the three they are looking at.
 */
export function describeItemActions(
    itemStatus: string,
    mode: string,
    isStale: boolean
): ItemActionAvailability {
    if (itemStatus !== AWAITING_ANSWER_ITEM_STATUS) {
        return { canAccept: false, canReject: false, acceptBlockedReason: NOT_AWAITING_REASON };
    }

    if (mode === "quality_review") {
        return { canAccept: false, canReject: true, acceptBlockedReason: REVIEW_MODE_REASON };
    }

    if (isStale) {
        return { canAccept: false, canReject: true, acceptBlockedReason: STALE_REASON };
    }

    return { canAccept: true, canReject: true, acceptBlockedReason: null };
}

/**
 * The item «Следующее →» opens: the next one still waiting, in reading order, wrapping past the end
 * of the queue once. Returns `null` when the current item is the last unanswered one — the button
 * disappears rather than pointing back at itself.
 */
export function findNextAwaitingItemId(
    items: readonly ContentAdaptationItemSummary[],
    mode: string,
    currentItemId: string | null
): string | null {
    const sortedItems = sortQueueItems(items, mode);
    const currentIndex = sortedItems.findIndex((item) => item.id === currentItemId);
    const startIndex = currentIndex >= 0 ? currentIndex + 1 : 0;

    for (let offset = 0; offset < sortedItems.length; offset += 1) {
        const candidate = sortedItems[(startIndex + offset) % sortedItems.length];
        if (candidate.id !== currentItemId && isAwaitingAnswer(candidate)) return candidate.id;
    }

    return null;
}

/** The item the screen opens on: the first one waiting, or the first row when the queue is answered. */
export function findInitialItemId(
    items: readonly ContentAdaptationItemSummary[],
    mode: string
): string | null {
    const sortedItems = sortQueueItems(items, mode);

    return sortedItems.find(isAwaitingAnswer)?.id ?? sortedItems[0]?.id ?? null;
}

/**
 * How far the sweep has got: items the model has answered, out of the items in the batch. A sweep
 * still running is the normal state of this screen — one model call per exercise, four per tick —
 * so the progress is the content, not a spinner over it.
 */
export function countItemsAnsweredByModel(summary: ContentAdaptationJobSummary): number {
    return Math.max(0, summary.itemCount - summary.pendingCount);
}

/** Poll only while a worker still owns the batch; every other state waits for a person. */
export function shouldPollJob(status: string): boolean {
    return status === WORKER_OWNED_JOB_STATUS;
}

/**
 * `POST …/retry` re-queues failed items and answers 409 when nothing failed, so the button exists
 * only when there is something for it to do.
 */
export function canRetryJob(summary: ContentAdaptationJobSummary): boolean {
    return summary.failedCount > 0;
}

function pickRussianForm(count: number, forms: [string, string, string]): string {
    const absoluteCount = Math.abs(count) % 100;
    if (absoluteCount >= 11 && absoluteCount <= 14) return forms[2];

    switch (absoluteCount % 10) {
        case 1:
            return forms[0];
        case 2:
        case 3:
        case 4:
            return forms[1];
        default:
            return forms[2];
    }
}

export function formatProposalCount(count: number): string {
    return `${count} ${pickRussianForm(count, ["предложение", "предложения", "предложений"])}`;
}

export function formatExerciseCount(count: number): string {
    return `${count} ${pickRussianForm(count, ["упражнение", "упражнения", "упражнений"])}`;
}

/**
 * The line an answered batch ends on. Zero buckets are left out: «отклонено 0» is an answer to a
 * question nobody asked, and a batch the model resolved by itself gets a sentence of its own
 * instead of three zeroes.
 */
export function describeResolvedBatch(summary: ContentAdaptationJobSummary): string {
    const parts: string[] = [];
    if (summary.acceptedCount > 0) parts.push(`принято ${summary.acceptedCount}`);
    if (summary.rejectedCount > 0) parts.push(`отклонено ${summary.rejectedCount}`);
    if (summary.unchangedCount > 0) parts.push(`без изменений ${summary.unchangedCount}`);

    if (parts.length === 0) {
        return "Разбирать нечего: модель не предложила ни одной правки.";
    }

    return `Все предложения разобраны: ${parts.join(", ")}.`;
}

/**
 * The lessons an accepted rewrite has already been written into — the ones whose new wording reaches
 * the team only when somebody publishes a version. Nothing in this block publishes anything.
 */
export function collectLessonsWithAcceptedItems(
    items: readonly ContentAdaptationItemSummary[]
): { lessonId: string; lessonTitle: string; acceptedCount: number }[] {
    const lessonsById = new Map<string, { lessonId: string; lessonTitle: string; acceptedCount: number }>();

    for (const item of items) {
        if (item.status !== "accepted") continue;

        const existing = lessonsById.get(item.lessonId);
        if (existing) {
            existing.acceptedCount += 1;
            continue;
        }

        lessonsById.set(item.lessonId, {
            lessonId: item.lessonId,
            lessonTitle: item.lessonTitle,
            acceptedCount: 1,
        });
    }

    return [...lessonsById.values()].sort((left, right) =>
        left.lessonTitle.localeCompare(right.lessonTitle, "ru")
    );
}
