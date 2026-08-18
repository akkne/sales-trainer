/**
 * The profile's vocabulary in one place: the seven field names as the backend spells them, the
 * three priority tiers, the four merge decisions, and the Russian each of them reads as.
 *
 * One dictionary per feature is the panel's localization rule (docs/LOCALIZATION.md,
 * ADMIN_UI_DESIGN.md §1.4) — a `banned_claims` translated inline in JSX becomes «запрещённые
 * обещания» on one screen and «запреты» on the next.
 */

export const ORGANIZATION_PROFILE_FIELDS = {
    product: "product",
    icp: "icp",
    objections: "objections",
    scriptStages: "script_stages",
    tone: "tone",
    bannedClaims: "banned_claims",
    glossary: "glossary",
} as const;

export type OrganizationProfileFieldCode =
    (typeof ORGANIZATION_PROFILE_FIELDS)[keyof typeof ORGANIZATION_PROFILE_FIELDS];

/**
 * The interview's order, mirroring `OrganizationProfileGapCodes.All`. The order is the feature:
 * it runs from the answer that changes the most of what the customer sees to the one that changes
 * the least.
 */
export const ORGANIZATION_PROFILE_FIELD_ORDER: readonly string[] = [
    ORGANIZATION_PROFILE_FIELDS.product,
    ORGANIZATION_PROFILE_FIELDS.icp,
    ORGANIZATION_PROFILE_FIELDS.objections,
    ORGANIZATION_PROFILE_FIELDS.scriptStages,
    ORGANIZATION_PROFILE_FIELDS.tone,
    ORGANIZATION_PROFILE_FIELDS.bannedClaims,
    ORGANIZATION_PROFILE_FIELDS.glossary,
];

/**
 * The two fields whose honest answer may be «таких нет». They are never shown while a blocking gap
 * is still open, and they never hold the profile's readiness hostage — the schema has no «answered:
 * nothing» marker, so a screen that counted them would show a profile that can never be finished.
 */
export const OPTIONAL_ANSWER_FIELDS: readonly string[] = [
    ORGANIZATION_PROFILE_FIELDS.bannedClaims,
    ORGANIZATION_PROFILE_FIELDS.glossary,
];

/**
 * The only field names `acceptedFields` accepts. `banned_claims` is deliberately absent: the merge
 * is add-only, so no draft, no stale second tab and no client bug can delete a compliance rule.
 */
export const OVERWRITABLE_PROFILE_FIELDS: readonly string[] = [
    ORGANIZATION_PROFILE_FIELDS.product,
    ORGANIZATION_PROFILE_FIELDS.icp,
    ORGANIZATION_PROFILE_FIELDS.tone,
    ORGANIZATION_PROFILE_FIELDS.scriptStages,
];

export const PROFILE_GAP_PRIORITIES = {
    blocking: "blocking",
    important: "important",
    optional: "optional",
} as const;

export const PROFILE_DRAFT_DECISIONS = {
    unchanged: "unchanged",
    fill: "fill",
    conflict: "conflict",
    extend: "extend",
} as const;

export const PROFILE_FIELD_LABELS: Record<string, string> = {
    [ORGANIZATION_PROFILE_FIELDS.product]: "Продукт",
    [ORGANIZATION_PROFILE_FIELDS.icp]: "Кому продаём",
    [ORGANIZATION_PROFILE_FIELDS.objections]: "Возражения",
    [ORGANIZATION_PROFILE_FIELDS.scriptStages]: "Этапы скрипта",
    [ORGANIZATION_PROFILE_FIELDS.tone]: "Тон общения",
    [ORGANIZATION_PROFILE_FIELDS.bannedClaims]: "Запрещённые обещания",
    [ORGANIZATION_PROFILE_FIELDS.glossary]: "Глоссарий",
};

/** «обязательно» / «желательно» / nothing — `optional` carries no label by design. */
export const PROFILE_GAP_PRIORITY_LABELS: Record<string, string | null> = {
    [PROFILE_GAP_PRIORITIES.blocking]: "обязательно",
    [PROFILE_GAP_PRIORITIES.important]: "желательно",
    [PROFILE_GAP_PRIORITIES.optional]: null,
};

export const BLOCKING_PRIORITY_HINT =
    "Пока не ответите, уроки будут говорить «ваш продукт» вместо названия вашего продукта.";

/**
 * Three suggestions under the tone field. They insert text and leave the field free — `tone` has no
 * enum in the contract, and a select would invent one the backend does not have.
 */
export const TONE_SUGGESTIONS = [
    "формальный",
    "на равных",
    "консультативный",
];

export const PROFILE_QUESTION_LIMIT = 3;

/** Objections and script stages below these counts are still gaps on the server. */
export const MINIMUM_OBJECTION_COUNT = 3;
export const MINIMUM_SCRIPT_STAGE_COUNT = 3;

/**
 * Where a 40.27 checkpoint leaves the extracted structure on its way to «Что ИИ прочитал в ваших
 * материалах». `sessionStorage`, not a query parameter: the draft is a whole document, and a
 * document in an address bar is a document in a server log.
 */
export const PROFILE_DRAFT_HANDOFF_STORAGE_KEY = "sellevate:org-profile-draft";
