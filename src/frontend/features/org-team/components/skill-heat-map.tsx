"use client";

import Link from "next/link";
import { useMemo } from "react";
import type { TeamSkillMap } from "@/features/org-shell/hooks/use-team-directory";
import {
    buildHeatMapColumns,
    indexMemberCells,
    readMemberCell,
    type HeatMapAxis,
} from "@/features/org-team/utils/heat-map-matrix";
import {
    HEAT_MAP_TONE_STYLES,
    UNMEASURED_CELL_TEXT,
    describeUnmeasuredCell,
    formatHeatMapCellValue,
    resolveHeatMapTone,
} from "@/features/org-team/utils/heat-map-scale";
import type { TeamHeatMapRow } from "@/features/org-team/utils/team-roster";
import {
    ATTEMPT_PLURAL_FORMS,
    formatCountWithNoun,
} from "@/features/org-team/utils/team-summary";

const DEPARTED_MEMBER_MARK = "†";
const NO_WEAKEST_STAGE_TEXT = "нет данных";

const AXIS_OPTIONS: { key: HeatMapAxis; label: string }[] = [
    { key: "stages", label: "по этапам" },
    { key: "skills", label: "по навыкам" },
];

const LEGEND_STEPS: { tone: keyof typeof HEAT_MAP_TONE_STYLES; label: string }[] = [
    { tone: "critical", label: "меньше 50%" },
    { tone: "weak", label: "50–64%" },
    { tone: "plain", label: "65–79%" },
    { tone: "strong", label: "80% и выше" },
];

interface HeatMapCellProps {
    accuracyPercent: number | null;
    attemptCount: number;
    minimumAttemptsForAccuracy: number;
    label: string;
}

function HeatMapCell({
    accuracyPercent,
    attemptCount,
    minimumAttemptsForAccuracy,
    label,
}: HeatMapCellProps) {
    const tone = resolveHeatMapTone(accuracyPercent);
    const toneStyle = HEAT_MAP_TONE_STYLES[tone];
    const description =
        accuracyPercent === null
            ? `${label}: ${describeUnmeasuredCell(minimumAttemptsForAccuracy)}`
            : `${label}: ${accuracyPercent}%, ${formatCountWithNoun(attemptCount, ATTEMPT_PLURAL_FORMS)}`;

    return (
        <td className="p-1">
            <div
                className="mono grid place-items-center h-8 min-w-16 rounded-lg text-sm"
                style={{ background: toneStyle.background, color: toneStyle.color }}
                title={description}
                aria-label={description}
            >
                {formatHeatMapCellValue(accuracyPercent)}
            </div>
        </td>
    );
}

interface SkillHeatMapProps {
    skillMap: TeamSkillMap;
    rows: TeamHeatMapRow[];
    /// False only when neither learning-service nor identity-service could say who works here.
    isRosterKnown: boolean;
    axis: HeatMapAxis;
    onAxisChange: (axis: HeatMapAxis) => void;
}

/// The matrix: managers down, funnel stages or individual skills across, one four-step colour scale
/// for the whole screen.
///
/// Cells do not click. There is no «покажи попытки по этому навыку» filter in the API, and a cell
/// that looks clickable and goes nowhere is worse than one that plainly does not. Names do click —
/// to that person's conversations, which is a route that exists.
export function SkillHeatMap({
    skillMap,
    rows,
    isRosterKnown,
    axis,
    onAxisChange,
}: SkillHeatMapProps) {
    const columns = useMemo(() => buildHeatMapColumns(skillMap, axis), [skillMap, axis]);

    const stageLabelsByKey = useMemo(
        () => new Map(skillMap.stages.map((stage) => [stage.key, stage.label])),
        [skillMap.stages]
    );

    const hasDepartedMembers = rows.some((row) => row.isActiveMember === false);

    return (
        <section aria-labelledby="team-heat-map-heading">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                <h2
                    id="team-heat-map-heading"
                    className="text-xs font-semibold tracking-wide uppercase text-ink-3"
                >
                    Тепловая карта
                </h2>
                <div
                    role="radiogroup"
                    aria-label="Колонки карты"
                    className="inline-flex items-center gap-1 p-1 rounded-xl border border-line bg-bg-2"
                >
                    {AXIS_OPTIONS.map((axisOption) => (
                        <button
                            key={axisOption.key}
                            type="button"
                            role="radio"
                            aria-checked={axis === axisOption.key}
                            onClick={() => onAxisChange(axisOption.key)}
                            className="px-3 h-7 rounded-lg text-xs font-medium transition-colors"
                            style={{
                                background: axis === axisOption.key ? "var(--ink)" : "transparent",
                                color: axis === axisOption.key ? "var(--bg)" : "var(--ink-2)",
                            }}
                        >
                            {axisOption.label}
                        </button>
                    ))}
                </div>
            </div>

            {!isRosterKnown && (
                <p
                    className="mb-3 px-3 py-2 rounded-lg text-xs"
                    style={{ background: "var(--amber-soft)", color: "var(--amber)" }}
                    role="status"
                >
                    Не удалось проверить, кто ещё работает в компании. Пометки «уже не работает» в
                    этом списке не показаны.
                </p>
            )}

            <div className="overflow-x-auto rounded-xl border border-line bg-surface">
                <table className="w-full border-collapse text-sm">
                    <thead>
                        <tr className="border-b border-line">
                            <th
                                scope="col"
                                className="sticky left-0 z-10 bg-surface px-3 py-2 text-left text-xs font-medium text-ink-3"
                            >
                                Менеджер
                            </th>
                            {columns.map((column) => (
                                <th
                                    key={column.key}
                                    scope="col"
                                    className="px-1 py-2 text-center text-xs font-medium text-ink-2 align-bottom"
                                    title={column.stageLabel ?? column.label}
                                >
                                    <span className="block max-w-24 truncate">{column.label}</span>
                                    {column.stageLabel && (
                                        <span className="block max-w-24 truncate text-ink-4 font-normal">
                                            {column.stageLabel}
                                        </span>
                                    )}
                                </th>
                            ))}
                            <th
                                scope="col"
                                className="px-3 py-2 text-center text-xs font-medium text-ink-3 border-l border-line"
                            >
                                Диалоги
                            </th>
                            <th
                                scope="col"
                                className="px-3 py-2 text-left text-xs font-medium text-ink-3"
                            >
                                Слабее всего
                            </th>
                        </tr>
                    </thead>

                    <tbody>
                        <tr className="border-b border-line-2">
                            <th
                                scope="row"
                                className="sticky left-0 z-10 bg-surface px-3 py-2 text-left font-medium text-ink"
                            >
                                Команда
                            </th>
                            {columns.map((column) => (
                                <HeatMapCell
                                    key={column.key}
                                    accuracyPercent={column.accuracyPercent}
                                    attemptCount={column.attemptCount}
                                    minimumAttemptsForAccuracy={skillMap.minimumAttemptsForAccuracy}
                                    label={`Команда · ${column.label}`}
                                />
                            ))}
                            <td className="px-3 py-2 text-center text-ink-3 border-l border-line">
                                {UNMEASURED_CELL_TEXT}
                            </td>
                            <td className="px-3 py-2 text-ink-3">{UNMEASURED_CELL_TEXT}</td>
                        </tr>

                        {rows.map((row) => {
                            const cellsByKey = indexMemberCells(row, axis);
                            const weakestStageLabel = row.weakestStageKey
                                ? (stageLabelsByKey.get(row.weakestStageKey) ?? row.weakestStageKey)
                                : null;

                            return (
                                <tr key={row.userId} className="border-b border-line last:border-0">
                                    <th
                                        scope="row"
                                        className="sticky left-0 z-10 bg-surface px-3 py-2 text-left font-normal"
                                    >
                                        <Link
                                            href={`/org/dialogs?userId=${row.userId}`}
                                            className="text-ink hover:text-primary-ink transition-colors"
                                        >
                                            {row.displayName}
                                        </Link>
                                        {row.isActiveMember === false && (
                                            <span
                                                className="ml-1 text-ink-3"
                                                title="уже не работает в компании"
                                            >
                                                {DEPARTED_MEMBER_MARK}
                                            </span>
                                        )}
                                    </th>

                                    {columns.map((column) => {
                                        const cell = readMemberCell(cellsByKey, column.key);
                                        return (
                                            <HeatMapCell
                                                key={column.key}
                                                accuracyPercent={cell?.accuracyPercent ?? null}
                                                attemptCount={cell?.attemptCount ?? 0}
                                                minimumAttemptsForAccuracy={
                                                    skillMap.minimumAttemptsForAccuracy
                                                }
                                                label={`${row.displayName} · ${column.label}`}
                                            />
                                        );
                                    })}

                                    <td className="mono px-3 py-2 text-center text-ink-2 border-l border-line">
                                        {row.dialogAverageScore ?? UNMEASURED_CELL_TEXT}
                                    </td>
                                    <td className="px-3 py-2 text-ink-2">
                                        {weakestStageLabel ?? (
                                            <span className="text-ink-3">
                                                {NO_WEAKEST_STAGE_TEXT}
                                            </span>
                                        )}
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>

            <div className="mt-3 flex flex-wrap items-center gap-3">
                {LEGEND_STEPS.map((step) => (
                    <span key={step.tone} className="inline-flex items-center gap-1.5 text-xs">
                        <span
                            aria-hidden
                            className="inline-block w-4 h-4 rounded"
                            style={{
                                background: HEAT_MAP_TONE_STYLES[step.tone].background,
                                border: "1px solid var(--line)",
                            }}
                        />
                        <span className="text-ink-3">{step.label}</span>
                    </span>
                ))}
            </div>

            <ul className="mt-2 flex flex-col gap-1 text-xs text-ink-3">
                {hasDepartedMembers && (
                    <li>{DEPARTED_MEMBER_MARK} уже не работает в компании</li>
                )}
                <li>
                    «{UNMEASURED_CELL_TEXT}» —{" "}
                    {describeUnmeasuredCell(skillMap.minimumAttemptsForAccuracy)}, процент не
                    считаем
                </li>
                {skillMap.unattributedAttemptCount > 0 && (
                    <li>
                        {formatCountWithNoun(
                            skillMap.unattributedAttemptCount,
                            ATTEMPT_PLURAL_FORMS
                        )}{" "}
                        не отнесены к навыку: упражнение удалено из библиотеки
                    </li>
                )}
            </ul>
        </section>
    );
}
