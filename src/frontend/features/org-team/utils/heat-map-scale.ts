export type HeatMapTone = "critical" | "weak" | "plain" | "strong" | "unmeasured";

interface HeatMapToneStyle {
    background: string;
    color: string;
}

const CRITICAL_ACCURACY_CEILING_PERCENT = 50;
const WEAK_ACCURACY_CEILING_PERCENT = 65;
const STRONG_ACCURACY_FLOOR_PERCENT = 80;

/// Four steps, one scale for the whole screen, and no lime anywhere in it
/// (ADMIN_UI_DESIGN.md O1 «Цвет ячейки»): `--primary` reads both as «хорошо» and as «бренд», and
/// on a matrix of sixty cells that ambiguity is what makes the map unreadable. «Готово» is
/// `--success`, the emerald, and it is a different colour from the brand on purpose.
export const HEAT_MAP_TONE_STYLES: Record<HeatMapTone, HeatMapToneStyle> = {
    critical: { background: "var(--heart-soft)", color: "var(--heart)" },
    weak: { background: "var(--amber-soft)", color: "var(--amber)" },
    plain: { background: "var(--surface-2)", color: "var(--ink-2)" },
    strong: { background: "var(--success-soft)", color: "var(--success)" },
    unmeasured: { background: "transparent", color: "var(--ink-4)" },
};

/// Which of the four steps a cell falls into. `null` is not zero and never paints as one — below
/// `minimumAttemptsForAccuracy` the server withholds the percentage because two right answers out
/// of two is not a fact about anybody.
export function resolveHeatMapTone(accuracyPercent: number | null): HeatMapTone {
    if (accuracyPercent === null) return "unmeasured";
    if (accuracyPercent < CRITICAL_ACCURACY_CEILING_PERCENT) return "critical";
    if (accuracyPercent < WEAK_ACCURACY_CEILING_PERCENT) return "weak";
    if (accuracyPercent < STRONG_ACCURACY_FLOOR_PERCENT) return "plain";
    return "strong";
}

export const UNMEASURED_CELL_TEXT = "—";

/// What a cell prints: the percentage, or the dash that stands for «слишком мало попыток».
export function formatHeatMapCellValue(accuracyPercent: number | null): string {
    return accuracyPercent === null ? UNMEASURED_CELL_TEXT : `${accuracyPercent}%`;
}

/// The sentence behind a dash, built from the threshold the server echoed back rather than from a
/// constant of ours — two copies of the same number are two numbers that eventually disagree.
export function describeUnmeasuredCell(minimumAttemptsForAccuracy: number): string {
    return `меньше ${minimumAttemptsForAccuracy} попыток`;
}
