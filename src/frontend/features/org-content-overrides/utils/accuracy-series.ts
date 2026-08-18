/**
 * Geometry for the accuracy chart in O19 (docs/TENANCY/ADMIN_UI_DESIGN.md O19,
 * docs/TENANCY/CONTENT_MODEL.md §2.4).
 *
 * Three rules the chart exists to obey, and each of them is a way the number stops being believed
 * if it is broken:
 *
 * 1. **A segment is a segment.** Averaging across a breaking publish mixes two populations that
 *    answered two different questions. Segments are drawn as separate polylines with a visible gap.
 * 2. **`unversionedAttempts` is not version 1.** Nobody can prove which content those answers were
 *    graded against, so they are a footnote and never a point.
 * 3. **A segment with no attempts is drawn, not skipped.** «На эту версию ещё никто не отвечал» and
 *    «этой версии нет» are different answers.
 */

import type { LessonAccuracySegment, LessonAccuracySeries } from "../types/lesson-editor";

export interface AccuracyPoint {
    versionNumber: number;
    /** 0..100, rounded for display. Null when nobody has answered this segment yet. */
    accuracyPercent: number | null;
    attemptCount: number;
}

export interface AccuracySegmentGeometry {
    key: string;
    startsAtBreakingChange: boolean;
    points: AccuracyPoint[];
}

export interface AccuracyChartModel {
    segments: AccuracySegmentGeometry[];
    /** Every version number on the axis, ascending, across all segments. */
    versionNumbers: number[];
    hasAnyAttempt: boolean;
    unversionedAttemptCount: number;
}

/** `accuracy` arrives as a fraction in 0..1 and is 0 when there are no attempts at all. */
export function toAccuracyPercent(accuracy: number, attemptCount: number): number | null {
    if (attemptCount === 0) return null;

    return Math.round(accuracy * 100);
}

function buildSegmentGeometry(segment: LessonAccuracySegment, index: number): AccuracySegmentGeometry {
    const { attemptCount, accuracy } = segment.statistics;

    // The server aggregates a whole segment into one statistic, so every version inside it carries
    // that segment's number. Drawing one point per version keeps the axis honest about how many
    // versions the run covers without inventing per-version data the API does not return.
    const versionNumbers =
        segment.versionNumbers.length > 0
            ? [...segment.versionNumbers]
            : [segment.startVersionNumber, segment.endVersionNumber].filter(
                  (value, position, all) => all.indexOf(value) === position
              );

    return {
        key: `${segment.startVersionNumber}-${segment.endVersionNumber}-${index}`,
        startsAtBreakingChange: segment.startsAtBreakingChange,
        points: versionNumbers.map((versionNumber) => ({
            versionNumber,
            accuracyPercent: toAccuracyPercent(accuracy, attemptCount),
            attemptCount,
        })),
    };
}

export function buildAccuracyChartModel(series: LessonAccuracySeries): AccuracyChartModel {
    const segments = series.segments.map(buildSegmentGeometry);
    const versionNumbers = segments
        .flatMap((segment) => segment.points.map((point) => point.versionNumber))
        .sort((left, right) => left - right);

    return {
        segments,
        versionNumbers,
        hasAnyAttempt: series.segments.some((segment) => segment.statistics.attemptCount > 0),
        unversionedAttemptCount: series.unversionedAttempts.attemptCount,
    };
}

const ATTEMPT_PLURAL_FORMS = ["попытка", "попытки", "попыток"] as const;

/** Russian plural agreement for the footnote and the point tooltips. */
export function pluralizeAttempts(count: number): string {
    const absolute = Math.abs(count) % 100;
    const lastDigit = absolute % 10;

    if (absolute > 10 && absolute < 20) return ATTEMPT_PLURAL_FORMS[2];
    if (lastDigit > 1 && lastDigit < 5) return ATTEMPT_PLURAL_FORMS[1];
    if (lastDigit === 1) return ATTEMPT_PLURAL_FORMS[0];

    return ATTEMPT_PLURAL_FORMS[2];
}

/**
 * The footnote under the chart. Returns null when there is nothing to explain — an empty footnote
 * reads as a broken one.
 */
export function describeUnversionedAttempts(count: number): string | null {
    if (count <= 0) return null;

    return `${count} ${pluralizeAttempts(count)} записаны до появления версий — их нет ни в одном отрезке.`;
}

/** The label for one point: percent, or the reason there is no percent. */
export function describeAccuracyPoint(point: AccuracyPoint): string {
    if (point.accuracyPercent === null) return "на эту версию ещё никто не отвечал";

    return `${point.accuracyPercent}% · ${point.attemptCount} ${pluralizeAttempts(point.attemptCount)}`;
}
