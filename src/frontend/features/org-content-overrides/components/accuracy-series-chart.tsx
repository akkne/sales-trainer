"use client";

import { useMemo } from "react";
import {
    buildAccuracyChartModel,
    describeAccuracyPoint,
    describeUnversionedAttempts,
} from "../utils/accuracy-series";
import type { LessonAccuracySeries } from "../types/lesson-editor";

interface AccuracySeriesChartProps {
    series: LessonAccuracySeries;
}

const CHART_HEIGHT = 160;
const CHART_WIDTH = 640;
const PADDING_LEFT = 40;
const PADDING_RIGHT = 12;
const PADDING_TOP = 12;
const PADDING_BOTTOM = 28;
const GRID_PERCENTS = [0, 50, 100] as const;

/**
 * Accuracy per lesson version (docs/TENANCY/ADMIN_UI_DESIGN.md O19).
 *
 * Every segment is its own polyline with a visible break between them. Joining them would average
 * two populations that answered two different questions — the failure `is_breaking` exists to
 * prevent (docs/TENANCY/CONTENT_MODEL.md §2.4). `unversionedAttempts` is a footnote, never a first
 * point, and a segment nobody has answered draws a hollow point rather than disappearing.
 */
export function AccuracySeriesChart({ series }: AccuracySeriesChartProps) {
    const model = useMemo(() => buildAccuracyChartModel(series), [series]);
    const footnote = describeUnversionedAttempts(model.unversionedAttemptCount);

    if (model.segments.length === 0) {
        return (
            <div>
                <p className="text-sm text-ink-3">
                    У урока ещё нет опубликованных версий, поэтому точность не по чему считать.
                </p>
                {footnote && <p className="mt-2 text-xs text-ink-3">{footnote}</p>}
            </div>
        );
    }

    const minimumVersion = model.versionNumbers[0];
    const maximumVersion = model.versionNumbers[model.versionNumbers.length - 1];
    const versionSpan = Math.max(maximumVersion - minimumVersion, 1);

    const plotWidth = CHART_WIDTH - PADDING_LEFT - PADDING_RIGHT;
    const plotHeight = CHART_HEIGHT - PADDING_TOP - PADDING_BOTTOM;

    const xForVersion = (versionNumber: number) =>
        PADDING_LEFT + ((versionNumber - minimumVersion) / versionSpan) * plotWidth;
    const yForPercent = (percent: number) => PADDING_TOP + (1 - percent / 100) * plotHeight;

    return (
        <div>
            <svg
                viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`}
                width="100%"
                height={CHART_HEIGHT}
                role="img"
                aria-label="Точность по версиям урока"
            >
                {GRID_PERCENTS.map((percent) => (
                    <g key={percent}>
                        <line
                            x1={PADDING_LEFT}
                            x2={CHART_WIDTH - PADDING_RIGHT}
                            y1={yForPercent(percent)}
                            y2={yForPercent(percent)}
                            stroke="var(--line)"
                            strokeWidth={1}
                        />
                        <text
                            x={PADDING_LEFT - 8}
                            y={yForPercent(percent) + 4}
                            textAnchor="end"
                            fontSize={10}
                            fill="var(--ink-3)"
                            style={{ fontFamily: "var(--font-mono)" }}
                        >
                            {percent}%
                        </text>
                    </g>
                ))}

                {model.segments.map((segment, segmentIndex) => {
                    const measured = segment.points.filter((point) => point.accuracyPercent !== null);
                    const polyline = measured
                        .map(
                            (point) =>
                                `${xForVersion(point.versionNumber)},${yForPercent(point.accuracyPercent ?? 0)}`
                        )
                        .join(" ");

                    const breakX = xForVersion(segment.points[0].versionNumber) - 6;

                    return (
                        <g key={segment.key}>
                            {segment.startsAtBreakingChange && segmentIndex > 0 && (
                                <line
                                    x1={breakX}
                                    x2={breakX}
                                    y1={PADDING_TOP}
                                    y2={PADDING_TOP + plotHeight}
                                    stroke="var(--ink-3)"
                                    strokeWidth={1}
                                    strokeDasharray="3 3"
                                />
                            )}
                            {measured.length > 1 && (
                                <polyline
                                    points={polyline}
                                    fill="none"
                                    stroke="var(--indigo)"
                                    strokeWidth={2}
                                />
                            )}
                            {segment.points.map((point) => (
                                <circle
                                    key={`${segment.key}-${point.versionNumber}`}
                                    cx={xForVersion(point.versionNumber)}
                                    cy={yForPercent(point.accuracyPercent ?? 0)}
                                    r={4}
                                    fill={point.accuracyPercent === null ? "var(--bg)" : "var(--indigo)"}
                                    stroke="var(--indigo)"
                                    strokeWidth={1.5}
                                >
                                    <title>{`v${point.versionNumber} · ${describeAccuracyPoint(point)}`}</title>
                                </circle>
                            ))}
                        </g>
                    );
                })}

                {model.versionNumbers.map((versionNumber) => (
                    <text
                        key={versionNumber}
                        x={xForVersion(versionNumber)}
                        y={CHART_HEIGHT - 8}
                        textAnchor="middle"
                        fontSize={10}
                        fill="var(--ink-3)"
                        style={{ fontFamily: "var(--font-mono)" }}
                    >
                        v{versionNumber}
                    </text>
                ))}
            </svg>

            {!model.hasAnyAttempt && (
                <p className="mt-2 text-xs text-ink-3">
                    На эти версии ещё никто не отвечал — точки нарисованы без значения, потому что
                    «никто не отвечал» и «версии нет» — разные ответы.
                </p>
            )}
            {footnote && <p className="mt-2 text-xs text-ink-3">{footnote}</p>}
        </div>
    );
}
