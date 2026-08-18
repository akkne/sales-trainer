/**
 * Wire shapes of the batch adaptation family (Phase 40.32,
 * `Sellevate.Learning.Features.ContentAdaptation.Models`). Mirrors the DTOs field for field — the
 * screens O12/O13 render what the server sends and compute no third document of their own
 * (docs/CONTENT_PIPELINE.md §6a, docs/TENANCY/ADMIN_UI_DESIGN.md §7).
 */

/** `ContentAdaptationModes`. A batch is one of exactly these two questions. */
export type ContentAdaptationMode = "tone_rewrite" | "quality_review";

/** `ContentAdaptationStatuses`. Derived from the items on every write, never counted by the client. */
export type ContentAdaptationJobStatus =
    | "preparing"
    | "awaiting_review"
    | "completed"
    | "failed";

/**
 * `ContentAdaptationItemStatuses`. `accepted` and `rejected` are the only two a background worker
 * can never write: they exist because a person clicked.
 */
export type ContentAdaptationItemStatus =
    | "pending"
    | "proposed"
    | "unchanged"
    | "accepted"
    | "rejected"
    | "failed";

/** `ContentReviewFindingCodes.SeverityFor` — decided by the code, never by the model. */
export type ContentReviewFindingSeverity = "blocking" | "advisory";

/** `ContentAdaptationJobSummaryDto`. */
export interface ContentAdaptationJobSummary {
    id: string;
    mode: string;
    stageKey: string;
    status: string;
    itemCount: number;
    /** Items the model has not answered yet — the only part of a batch that still costs money. */
    pendingCount: number;
    /** Items waiting for a person. The number both screens are actually about. */
    awaitingReviewCount: number;
    acceptedCount: number;
    rejectedCount: number;
    unchangedCount: number;
    failedCount: number;
    failureReason: string | null;
    createdAt: string;
    updatedAt: string;
    completedAt: string | null;
}

/** `ContentAdaptationItemSummaryDto` — one queue row, without either document. */
export interface ContentAdaptationItemSummary {
    id: string;
    exerciseId: string;
    lessonId: string;
    lessonTitle: string;
    exerciseType: string;
    orderInLesson: number;
    status: string;
    /** The model's own sentence about what it changed. Null in review mode. */
    changeSummary: string | null;
    findingCount: number;
    /** Review mode: this exercise is actively harmful, not merely weak. Lifts the row in the queue. */
    hasBlockingFinding: boolean;
    changedFieldCount: number;
    failureReason: string | null;
    resolvedAt: string | null;
}

/** `ContentAdaptationJobDto` — the batch with its queue attached. */
export interface ContentAdaptationJob {
    summary: ContentAdaptationJobSummary;
    items: ContentAdaptationItemSummary[];
}

/** `ContentFieldChangeDto` — one JSON leaf that moved. An enumeration, never a merge. */
export interface ContentFieldChange {
    path: string;
    before: string | null;
    after: string | null;
}

/** `ContentReviewFindingDto` — a code from the closed vocabulary plus the server's fixed sentence. */
export interface ContentReviewFinding {
    code: string;
    severity: string;
    /** Written on the server from the code, never by the model. Rendered verbatim. */
    message: string;
    /** The offending fragment in the exercise's own words, when the model quoted one. */
    detail: string | null;
}

/**
 * `ContentAdaptationItemDto` — everything needed to answer yes or no about one exercise.
 *
 * The two documents arrive side by side and unmerged; `changes` says which leaves differ. Nothing
 * on this type is combined by the client, which is the same refusal 40.18 made one level up.
 */
export interface ContentAdaptationItem {
    summary: ContentAdaptationItemSummary;
    currentContent: unknown;
    proposedContent: unknown;
    changes: ContentFieldChange[];
    findings: ContentReviewFinding[];
    /** The exercise was edited after the proposal was computed. Accept will answer 409. */
    isStale: boolean;
}

/** `StartContentAdaptationRequestDto`. Two fields, and neither is a list of exercises. */
export interface StartContentAdaptationRequest {
    mode: ContentAdaptationMode;
    stageKey: string;
}
