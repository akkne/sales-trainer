/**
 * The wire shapes of the 40.27/40.28 pipeline, mirroring learning-service's
 * `Sellevate.Learning.Features.ContentGeneration.Models` record for record
 * (docs/CONTENT_PIPELINE.md §3). Nothing here is invented: a field that is not on the DTO is a
 * field this screen may not render.
 */

/** `ContentGenerationJobStatuses`. Six states, and every one of them is a layout on O11. */
export type ContentGenerationJobStatus =
    | "structuring"
    | "awaiting_review"
    | "insufficient"
    | "generating"
    | "completed"
    | "failed";

/**
 * `ContentInsufficiencyDto.Stage`. `material` — refused from the raw text before anything was paid
 * for, so there is no structure to open; `structure` — refused from what could be read out of it,
 * so there is.
 */
export type ContentSufficiencyStage = "material" | "structure";

/** One item of the refusal. `code` is from a closed vocabulary of seven; `message` is what is read. */
export interface ContentSufficiencyGap {
    code: string;
    message: string;
}

/**
 * `ContentInsufficiencyDto`. Non-null exactly when the run is `insufficient`.
 *
 * `note` is the model's own reasoning and is deliberately **not rendered** — it is a diagnostic for
 * a developer looking at a run that was refused unexpectedly, and the customer's text is in `gaps`.
 */
export interface ContentInsufficiency {
    stage: string;
    gaps: ContentSufficiencyGap[];
    note: string | null;
}

export interface ContentStructureObjection {
    text: string;
    bestResponse: string | null;
}

/**
 * `ContentStructureDto` — the artefact at the checkpoint, field for field the organization profile
 * of docs/TENANCY/CONTENT_MODEL.md §3. Every field is optional and a gap stays a gap: `null` for a
 * scalar, `[]` for a list, never an invented value.
 */
export interface ContentStructure {
    product: string | null;
    icp: string | null;
    tone: string | null;
    objections: ContentStructureObjection[];
    scriptStages: string[];
    glossary: Record<string, string>;
    bannedClaims: string[];
}

/**
 * `ContentGenerationJobSummaryDto` — a run in a list. Carries neither the material nor the
 * structure (both are documents) but **does** carry the refusal, which is why O10 can print the
 * first gap in the row rather than sending somebody inside for every refused run.
 */
export interface ContentGenerationJobSummary {
    id: string;
    title: string;
    status: string;
    /** `skill-gap:<stage>@<yyyy-MM-dd>` when the dashboard started this run, null when a person did. */
    gapSourceRef: string | null;
    insufficiency: ContentInsufficiency | null;
    producedLessonId: string | null;
    producedExerciseCount: number;
    failureReason: string | null;
    createdAt: string;
    updatedAt: string;
}

/** `ContentGenerationJobDto` — one run with its structure and the material it came from. */
export interface ContentGenerationJob {
    id: string;
    title: string;
    status: string;
    gapSourceRef: string | null;
    sourceMaterial: string;
    structure: ContentStructure | null;
    insufficiency: ContentInsufficiency | null;
    structuredAt: string | null;
    approvedAt: string | null;
    producedLessonId: string | null;
    producedLessonVersionId: string | null;
    producedExerciseCount: number;
    generatedAt: string | null;
    failureReason: string | null;
    createdAt: string;
    updatedAt: string;
}

/** `StartContentGenerationRequestDto`. */
export interface StartContentGenerationRequest {
    title: string;
    material: string;
}

/** `SupplementContentMaterialRequestDto` — appended, never replacing. */
export interface SupplementContentMaterialRequest {
    material: string;
}

/** `ContentOverrideDto`, read by O9 only for its two counters. `isStale` is computed on read. */
export interface ContentOverrideSummary {
    kind: string;
    overrideId: string;
    baseId: string;
    title: string;
    isStale: boolean;
}

/** `ContentAdaptationJobSummaryDto`, read by O9 only for `awaitingReviewCount`. */
export interface ContentAdaptationJobSummary {
    id: string;
    mode: string;
    stageKey: string;
    status: string;
    itemCount: number;
    pendingCount: number;
    awaitingReviewCount: number;
    acceptedCount: number;
    rejectedCount: number;
    unchangedCount: number;
    failedCount: number;
}
