import type { ChipTone } from "@/shared/components/chip";
import type { ContentGenerationJobStatus } from "@/features/org-content-generation/types/content-generation";

/**
 * O9–O11 (docs/TENANCY/ADMIN_UI_DESIGN.md §1.4, §2). Every backend value these three screens show
 * to a human is translated exactly once, here — otherwise the same `insufficient` becomes
 * «материала мало» in the list and «недостаточно материала» on the run, and support cannot tell
 * whether those are one state or two.
 */

export const CONTENT_GENERATION_JOB_STATUSES = {
    structuring: "structuring",
    awaitingReview: "awaiting_review",
    insufficient: "insufficient",
    generating: "generating",
    completed: "completed",
    failed: "failed",
} as const;

/** The order the status filter offers them in: the two that need a person first. */
export const CONTENT_GENERATION_STATUS_FILTER_ORDER: ContentGenerationJobStatus[] = [
    CONTENT_GENERATION_JOB_STATUSES.awaitingReview,
    CONTENT_GENERATION_JOB_STATUSES.insufficient,
    CONTENT_GENERATION_JOB_STATUSES.structuring,
    CONTENT_GENERATION_JOB_STATUSES.generating,
    CONTENT_GENERATION_JOB_STATUSES.completed,
    CONTENT_GENERATION_JOB_STATUSES.failed,
];

/** The fixed dictionary of ADMIN_UI_DESIGN.md §1.4, used verbatim. */
export const CONTENT_GENERATION_STATUS_LABELS: Record<ContentGenerationJobStatus, string> = {
    structuring: "Разбираем материал",
    awaiting_review: "Ждёт проверки",
    insufficient: "Материала не хватает",
    generating: "Генерируем",
    completed: "Готово",
    failed: "Ошибка",
};

/**
 * `insufficient` is amber and not red on purpose: nothing broke, the material was thin, and the run
 * says what to bring. Red would make an answerable refusal look like a fault of the product.
 */
export const CONTENT_GENERATION_STATUS_TONES: Record<ContentGenerationJobStatus, ChipTone> = {
    structuring: "neutral",
    awaiting_review: "indigo",
    insufficient: "warn",
    generating: "neutral",
    completed: "good",
    failed: "bad",
};

/** A status this build does not know is shown as itself rather than as a guess. */
export function describeJobStatus(status: string): string {
    return CONTENT_GENERATION_STATUS_LABELS[status as ContentGenerationJobStatus] ?? status;
}

export function jobStatusTone(status: string): ChipTone {
    return CONTENT_GENERATION_STATUS_TONES[status as ContentGenerationJobStatus] ?? "neutral";
}

/** «ОТКУДА» in the list: a run either answers a measured failure or somebody pasted material. */
export const RUN_ORIGIN_FROM_DASHBOARD_LABEL = "с дашборда";
export const RUN_ORIGIN_MANUAL_LABEL = "вручную";

export function describeRunOrigin(gapSourceRef: string | null): string {
    return gapSourceRef ? RUN_ORIGIN_FROM_DASHBOARD_LABEL : RUN_ORIGIN_MANUAL_LABEL;
}

/**
 * `ContentSufficiencyCodes` — the closed vocabulary of seven. The sentence the РОП reads travels on
 * the gap itself (it is resolved when the refusal is recorded, so a run refused last month still
 * explains itself in the words it was refused with), so these short labels are only the fallback
 * for a gap that somehow arrives with an empty message.
 */
export const CONTENT_SUFFICIENCY_CODE_FALLBACK_LABELS: Record<string, string> = {
    off_topic: "Материал не про продажи.",
    too_short: "Материала слишком мало.",
    no_product: "Не описан продукт.",
    no_icp: "Не описано, кому вы продаёте.",
    no_objections: "Нет ни одного возражения клиента.",
    no_script: "Нет этапов разговора.",
    no_examples: "Только общие формулировки, без живых примеров.",
};

/**
 * What a gap renders as. The server's sentence wins; a gap with neither a sentence nor a code this
 * build knows is dropped by the caller rather than shown as an empty bullet — the same rule the
 * backend applies to a code a model invented.
 */
export function describeSufficiencyGapMessage(code: string, message: string): string | null {
    const trimmedMessage = message.trim();
    if (trimmedMessage.length > 0) return trimmedMessage;

    return CONTENT_SUFFICIENCY_CODE_FALLBACK_LABELS[code] ?? null;
}

/** `ContentInsufficiencyDto.Stage`. */
export const CONTENT_SUFFICIENCY_STAGES = {
    material: "material",
    structure: "structure",
} as const;

/**
 * `ContentGenerationJobService.MaximumMaterialLength`. The only two client-side validations the
 * start form is allowed: emptiness and this ceiling. Thin material is **not** a form error — it
 * becomes a run in `insufficient` that O11 argues with.
 */
export const MATERIAL_MAXIMUM_LENGTH = 60000;

/**
 * `ContentStructureDocumentSerializer`'s caps, mirrored so the editor can disable «+ добавить» at
 * the limit instead of letting the server silently drop the eleventh objection.
 */
export const STRUCTURE_VALUE_MAXIMUM_LENGTH = 2000;
export const STRUCTURE_MAXIMUM_OBJECTION_COUNT = 10;
export const STRUCTURE_MAXIMUM_SCRIPT_STAGE_COUNT = 12;
export const STRUCTURE_MAXIMUM_GLOSSARY_TERM_COUNT = 30;
export const STRUCTURE_MAXIMUM_BANNED_CLAIM_COUNT = 20;

/** How long the checkpoint waits after the last keystroke before saving. */
export const STRUCTURE_AUTOSAVE_DEBOUNCE_MILLISECONDS = 1500;

/** `refetchInterval` while a worker owns the run. */
export const JOB_POLL_INTERVAL_MILLISECONDS = 3000;
