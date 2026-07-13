// Skill stages — group skills on the path page by funnel stage.
// Stages are now DB-driven and admin-editable (see /admin/skill-stages and the
// public GET /skills/stages endpoint). This file holds the BUILT-IN DEFAULTS,
// used as a fallback while the API list loads, if it is empty, or on error.
// Fetch the live list with `useSkillStages()` (features/skills/hooks/use-skill-tree).

export interface SkillStageMeta {
    key: string;
    label: string;
    order: number;
    accent: string; // CSS color
}

export const SKILL_STAGES: readonly SkillStageMeta[] = [
    { key: "preparation", label: "Подготовка", order: 1, accent: "var(--indigo)" },
    { key: "discovery", label: "Выявление потребности и оффер", order: 2, accent: "#7C3AED" },
    { key: "engagement", label: "Контент и коммуникация", order: 3, accent: "#0EA5E9" },
    { key: "closing", label: "Закрытие сделки", order: 4, accent: "#F97316" },
    { key: "retention", label: "Удержание", order: 5, accent: "#10B981" },
];

const FALLBACK_STAGE: SkillStageMeta = {
    key: "general",
    label: "Другое",
    order: 99,
    accent: "var(--ink-3)",
};

/**
 * Resolve display metadata for a stage key against the given stage list
 * (defaults to the built-in {@link SKILL_STAGES}). Unknown keys fall back to a
 * generic "Other" bucket that preserves the original key.
 */
export function getStageMeta(
    stageKey: string | undefined | null,
    stages: readonly SkillStageMeta[] = SKILL_STAGES
): SkillStageMeta {
    if (!stageKey) return FALLBACK_STAGE;
    return stages.find((s) => s.key === stageKey) ?? { ...FALLBACK_STAGE, key: stageKey, label: stageKey };
}
