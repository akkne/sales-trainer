import {
    CONTENT_GENERATION_JOB_STATUSES,
    CONTENT_SUFFICIENCY_STAGES,
    JOB_POLL_INTERVAL_MILLISECONDS,
    describeSufficiencyGapMessage,
} from "@/features/org-content-generation/constants/generation-dictionary";
import type {
    ContentGenerationJob,
    ContentInsufficiency,
    ContentSufficiencyGap,
} from "@/features/org-content-generation/types/content-generation";

/**
 * O11's state machine (docs/CONTENT_PIPELINE.md §2, ADMIN_UI_DESIGN.md O11). Six statuses, five
 * layouts — `structuring` and `generating` share one, because from the outside both are «идёт
 * работа, можно уйти».
 *
 * It lives here rather than in the page because it is the part that has to be provably right: the
 * two transitions this pipeline exists for — the checkpoint and the refusal — are decided by these
 * predicates, and a screen that offers «Сгенерировать» one state early spends the customer's money
 * on a structure nobody confirmed.
 */
export type JobLayout =
    | "in_progress"
    | "insufficient"
    | "checkpoint"
    | "completed"
    | "failed"
    | "unknown";

export function resolveJobLayout(status: string): JobLayout {
    switch (status) {
        case CONTENT_GENERATION_JOB_STATUSES.structuring:
        case CONTENT_GENERATION_JOB_STATUSES.generating:
            return "in_progress";
        case CONTENT_GENERATION_JOB_STATUSES.insufficient:
            return "insufficient";
        case CONTENT_GENERATION_JOB_STATUSES.awaitingReview:
            return "checkpoint";
        case CONTENT_GENERATION_JOB_STATUSES.completed:
            return "completed";
        case CONTENT_GENERATION_JOB_STATUSES.failed:
            return "failed";
        default:
            return "unknown";
    }
}

/** The two states a background worker owns. Every other state is waiting for a person. */
export function isWorkerOwnedStatus(status: string): boolean {
    return (
        status === CONTENT_GENERATION_JOB_STATUSES.structuring ||
        status === CONTENT_GENERATION_JOB_STATUSES.generating
    );
}

/**
 * There is no SSE and no websocket in the contract, so the run is polled — but only while somebody
 * else is working on it, and never behind a hidden tab. A run left open overnight on a finished
 * status must not keep asking.
 */
export function resolveJobPollInterval(
    status: string | undefined,
    isDocumentHidden: boolean
): number | false {
    if (status === undefined) return false;
    if (isDocumentHidden) return false;

    return isWorkerOwnedStatus(status) ? JOB_POLL_INTERVAL_MILLISECONDS : false;
}

export interface JobProgressCopy {
    title: string;
    description: string;
}

/**
 * The two in-progress screens differ in one sentence, and the difference is money: structuring can
 * still be abandoned cheaply, generation has already been paid for, so only the first is worth
 * telling somebody they may walk away from.
 */
export function describeJobProgress(status: string): JobProgressCopy {
    if (status === CONTENT_GENERATION_JOB_STATUSES.generating) {
        return {
            title: "Собираем упражнения…",
            description:
                "Обычно это занимает от полуминуты до пары минут. Прогон уже идёт — можно закрыть страницу, результат будет ждать вас в списке.",
        };
    }

    return {
        title: "Разбираем материал…",
        description:
            "Обычно это занимает от полуминуты до пары минут. Можно закрыть страницу — прогон продолжится, он будет ждать вас в списке.",
    };
}

/** «Всё верно» is only offered at the checkpoint, and it is the only door into the paid half. */
export function canApproveStructure(status: string): boolean {
    return status === CONTENT_GENERATION_JOB_STATUSES.awaitingReview;
}

/**
 * `PUT …/structure` is open at the checkpoint **and** on a refused run: somebody who knows their
 * four objections may simply type them, and that beats sending them to find a document containing
 * them. The edit is re-inspected, so the threshold stays answerable without becoming waivable.
 */
export function canEditStructure(status: string): boolean {
    return (
        status === CONTENT_GENERATION_JOB_STATUSES.awaitingReview ||
        status === CONTENT_GENERATION_JOB_STATUSES.insufficient
    );
}

/** «Вот ещё материал» — 409 on anything but a refused run, so it is offered nowhere else. */
export function canSupplementMaterial(status: string): boolean {
    return status === CONTENT_GENERATION_JOB_STATUSES.insufficient;
}

export function canRetryRun(status: string): boolean {
    return status === CONTENT_GENERATION_JOB_STATUSES.failed;
}

/**
 * «Открыть структуру» on a refusal. At `stage: "material"` the run was refused from the raw text
 * before anything was sent to a model, so there is no structure to open and the only way forward is
 * to add material — offering an editor over `null` would be offering to invent the reading.
 */
export function canOpenStructureFromRefusal(job: ContentGenerationJob): boolean {
    if (job.status !== CONTENT_GENERATION_JOB_STATUSES.insufficient) return false;
    if (job.structure === null) return false;

    return job.insufficiency?.stage === CONTENT_SUFFICIENCY_STAGES.structure;
}

/**
 * The gaps worth rendering. A gap with neither a sentence nor a code this build knows is dropped
 * rather than shown as an empty bullet — an unactionable refusal is the one thing this block must
 * never produce.
 */
export function readableSufficiencyGaps(
    insufficiency: ContentInsufficiency | null | undefined
): ContentSufficiencyGap[] {
    if (!insufficiency) return [];

    return insufficiency.gaps.filter(
        (gap) => describeSufficiencyGapMessage(gap.code, gap.message) !== null
    );
}

/** What O10 prints under a refused row: the first gap, because that is usually the actionable one. */
export function firstSufficiencyGapMessage(
    insufficiency: ContentInsufficiency | null | undefined
): string | null {
    const [firstGap] = readableSufficiencyGaps(insufficiency);
    if (!firstGap) return null;

    return describeSufficiencyGapMessage(firstGap.code, firstGap.message);
}
