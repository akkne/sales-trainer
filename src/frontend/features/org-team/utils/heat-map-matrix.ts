import type {
    TeamSkillMap,
    TeamSkillMapCell,
} from "@/features/org-shell/hooks/use-team-directory";

export type HeatMapAxis = "stages" | "skills";

export interface HeatMapColumn {
    /// Matches `TeamSkillMapCell.key` — the stage key on the stage axis, the skill identifier on
    /// the skill axis. The whole alignment of the matrix hangs on this equality.
    key: string;
    label: string;
    /// The stage a skill belongs to, so a column of thirty skills stays readable. Null on the
    /// stage axis, where the column already is the stage.
    stageLabel: string | null;
    attemptCount: number;
    accuracyPercent: number | null;
}

/// The columns of the heat map plus the team-wide number in each — one array, so the «Команда»
/// row and the manager rows can never end up describing different columns.
///
/// Both axes come out of the same response (ADMIN_UI_DESIGN.md O1): switching «по этапам /
/// по навыкам» re-reads what is already in memory and never issues a second request.
export function buildHeatMapColumns(skillMap: TeamSkillMap, axis: HeatMapAxis): HeatMapColumn[] {
    if (axis === "stages") {
        return skillMap.stages.map((stage) => ({
            key: stage.key,
            label: stage.label,
            stageLabel: null,
            attemptCount: stage.attemptCount,
            accuracyPercent: stage.accuracyPercent,
        }));
    }

    const stageLabelsByKey = new Map(skillMap.stages.map((stage) => [stage.key, stage.label]));

    return skillMap.skills.map((skill) => ({
        key: skill.skillId,
        label: skill.title,
        stageLabel: stageLabelsByKey.get(skill.stageKey) ?? skill.stageKey,
        attemptCount: skill.attemptCount,
        accuracyPercent: skill.accuracyPercent,
    }));
}

interface CellBearingRow {
    stages: TeamSkillMapCell[];
    skills: TeamSkillMapCell[];
}

/// One manager's cells, by column key.
///
/// A member carries a cell only for what they actually practised, so a missing key is a real
/// state — «этот человек не трогал этот навык» — and not an error. Reading the arrays positionally
/// instead would silently shift a whole row sideways the first time somebody skipped a stage.
export function indexMemberCells(
    row: CellBearingRow,
    axis: HeatMapAxis
): Map<string, TeamSkillMapCell> {
    const cells = axis === "stages" ? row.stages : row.skills;
    return new Map(cells.map((cell) => [cell.key, cell]));
}

export function readMemberCell(
    cellsByKey: Map<string, TeamSkillMapCell>,
    columnKey: string
): TeamSkillMapCell | null {
    return cellsByKey.get(columnKey) ?? null;
}
