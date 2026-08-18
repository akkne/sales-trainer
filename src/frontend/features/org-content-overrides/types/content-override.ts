/**
 * Wire shapes for the two override families the organization panel shows in one table
 * (docs/TENANCY/ADMIN_UI_DESIGN.md O14/O15, docs/TENANCY/CONTENT_MODEL.md §2.6).
 *
 * Three of the four kinds live in learning-service (`ContentOverrideDto`), the fourth in ai-service
 * (`DialogModeOverrideDto`). They are separate services and separate DTOs on purpose; the screen
 * merges them because from the customer's side it is one question.
 */

/** `Sellevate.Learning.Features.Content.Models.ContentOverrideKinds`. */
export const LEARNING_OVERRIDE_KINDS = ["lessons", "techniques", "reference-materials"] as const;

export type LearningOverrideKind = (typeof LEARNING_OVERRIDE_KINDS)[number];

/** The fourth kind is not a learning-service kind at all — it is the ai-service dialog mode. */
export const DIALOG_MODE_OVERRIDE_KIND = "modes";

export const OVERRIDE_KINDS = [...LEARNING_OVERRIDE_KINDS, DIALOG_MODE_OVERRIDE_KIND] as const;

export type OverrideKind = (typeof OVERRIDE_KINDS)[number];

export function isOverrideKind(value: string): value is OverrideKind {
    return (OVERRIDE_KINDS as readonly string[]).includes(value);
}

export function isLearningOverrideKind(value: string): value is LearningOverrideKind {
    return (LEARNING_OVERRIDE_KINDS as readonly string[]).includes(value);
}

/**
 * `ContentOverrideDto`. `isStale` is computed by the server on every read and stored nowhere — the
 * client neither caches it nor derives one of its own.
 */
export interface ContentOverrideSummary {
    kind: LearningOverrideKind;
    overrideId: string;
    baseId: string;
    title: string;
    isStale: boolean;
    /** Lesson: the forked `LessonVersion` id. Technique/reference: the base's content hash. */
    forkedFrom: string | null;
    /** The same pointer for the base as it stands now. Null when the base has never been published. */
    baseCurrent: string | null;
}

/**
 * `ContentOverrideReviewDto`. Three raw documents and nothing pre-merged: the API returns no diff,
 * deliberately, and neither does this client (docs/TENANCY/ADMIN_UI_DESIGN.md §7).
 */
export interface ContentOverrideReview {
    summary: ContentOverrideSummary;
    override: unknown;
    /** Populated for lessons only — the other families have no version table to point back at. */
    baseAtFork: unknown;
    baseCurrent: unknown;
}

/** `DialogModeOverrideDto` (ai-service). */
export interface DialogModeOverrideSummary {
    overrideId: string;
    baseModeId: string;
    bundleId: string;
    key: string;
    title: string;
    isStale: boolean;
    forkedFromHash: string | null;
    baseCurrentHash: string | null;
}

/** `DialogModeOverrideReviewDto` (ai-service). Two prompts per side, unmerged. */
export interface DialogModeOverrideReview {
    summary: DialogModeOverrideSummary;
    overrideChatSystemPrompt: string;
    overrideFeedbackSystemPrompt: string;
    baseChatSystemPrompt: string | null;
    baseFeedbackSystemPrompt: string | null;
}

/** `AdminDialogModeDto` — what `PUT /admin/dialog/overrides/modes/{id}` answers with. */
export interface AdminDialogMode {
    id: string;
    key: string;
    title: string;
    chatSystemPrompt: string | null;
    feedbackSystemPrompt: string | null;
}
