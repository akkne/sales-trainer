// Skill stages — group skills on the path page by funnel stage.
// Backend stores `stage` as a free string; this file is the single source of truth
// for the displayed label, ordering, and accent color.

export interface SkillStageMeta {
    key: string;
    label: string;
    order: number;
    accent: string; // CSS color
}

export const SKILL_STAGES: readonly SkillStageMeta[] = [
    { key: "preparation", label: "Подготовка", order: 1, accent: "var(--indigo)" },
    { key: "discovery", label: "Выявление и оффер", order: 2, accent: "#7C3AED" },
    { key: "engagement", label: "Контент и коммуникация", order: 3, accent: "#0EA5E9" },
    { key: "closing", label: "Закрытие сделки", order: 4, accent: "#F97316" },
    { key: "retention", label: "Удержание клиента", order: 5, accent: "#10B981" },
];

const FALLBACK_STAGE: SkillStageMeta = {
    key: "general",
    label: "Другое",
    order: 99,
    accent: "var(--ink-3)",
};

export function getStageMeta(stageKey: string | undefined | null): SkillStageMeta {
    if (!stageKey) return FALLBACK_STAGE;
    return SKILL_STAGES.find((s) => s.key === stageKey) ?? { ...FALLBACK_STAGE, key: stageKey, label: stageKey };
}
