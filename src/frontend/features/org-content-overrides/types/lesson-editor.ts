/**
 * Wire shapes the organization's lesson editor reads (docs/TENANCY/ADMIN_UI_DESIGN.md O19).
 *
 * Versions are immutable and there is exactly one draft at a time — both facts belong to the
 * backend (`LessonVersionStatuses`, a partial unique index), and the editor renders them rather
 * than re-deciding them.
 */

import type { ExerciseType } from "@/features/admin/components/exercise-editors";

/** `Sellevate.Learning.Common.Constants.LessonVersionStatuses`. */
export type LessonVersionStatus = "draft" | "published" | "archived";

/** `LessonVersionSummaryDto`. The body is deliberately absent — history is read far more often. */
export interface LessonVersionSummary {
    id: string;
    lessonId: string;
    versionNumber: number;
    status: string;
    contentHash: string;
    baseVersionId: string | null;
    isBreaking: boolean;
    createdBy: string | null;
    createdAt: string;
    publishedAt: string | null;
}

/** `LessonVersionDto` — a summary plus the frozen snapshot. */
export interface LessonVersion extends LessonVersionSummary {
    content: unknown;
}

/** `PublishLessonVersionResultDto`. */
export interface PublishLessonVersionResult {
    version: LessonVersion;
    /** False when the content hash matched the last published version: nothing was frozen. */
    createdNewVersion: boolean;
}

/** `LessonAttemptStatisticsDto`. `accuracy` is a fraction in 0..1, not a percentage. */
export interface LessonAttemptStatistics {
    attemptCount: number;
    correctAttemptCount: number;
    accuracy: number;
    averageScore: number;
    firstAttemptAt: string | null;
    lastAttemptAt: string | null;
}

/** `LessonAccuracySegmentDto` — one continuous run between two breaking publishes. */
export interface LessonAccuracySegment {
    startVersionNumber: number;
    endVersionNumber: number;
    versionNumbers: number[];
    versionIds: string[];
    startsAtBreakingChange: boolean;
    statistics: LessonAttemptStatistics;
}

/** `LessonAccuracySeriesDto`. */
export interface LessonAccuracySeries {
    lessonId: string;
    segments: LessonAccuracySegment[];
    /** Attempts recorded before versions existed. Never folded into version 1. */
    unversionedAttempts: LessonAttemptStatistics;
}

/** `AdminLessonWithTopicDto` — what `GET /admin/lessons` returns. No owner field, see O19 notes. */
export interface AdminLessonWithTopic {
    id: string;
    topicId: string;
    topicIconicName: string;
    topicTitle: string;
    title: string;
    orderInTopic: number;
}

/** `AdminExerciseDto`. */
export interface AdminExercise {
    id: string;
    lessonId: string;
    type: string;
    orderInLesson: number;
    content: unknown;
    customAiPrompt: string | null;
}

/** `CreateExerciseRequestDto` — the same body for create and update. */
export interface WriteExerciseRequest {
    type: ExerciseType;
    orderInLesson: number;
    content: unknown;
    customAiPrompt: string | null;
}
