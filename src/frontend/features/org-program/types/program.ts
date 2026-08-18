/**
 * Phase 40.17 programme versioning, as `AdminProgramController` returns it
 * (docs/TENANCY/CONTENT_MODEL.md §2.5, docs/API_CONTRACTS.md → «Programme versions and
 * enrollment»). Names and nullability mirror the C# records one-for-one; nothing here is derived.
 */

export interface ProgramVersionSummary {
    id: string;
    versionNumber: number;
    /** `draft` | `published` | `archived` — `ProgramVersionStatuses`, never translated in code. */
    status: string;
    itemCount: number;
    enrollmentCount: number;
    createdBy: string | null;
    createdAt: string;
    publishedAt: string | null;
}

/**
 * `lessonTitle` and `lessonVersionNumber` are read from the pinned snapshot, and are `null` when
 * that snapshot is no longer visible. The screen must say «урок недоступен» rather than substitute
 * the live lesson's title — that substitution is the thing this whole phase exists to prevent.
 */
export interface ProgramItem {
    id: string;
    skillId: string;
    lessonId: string;
    lessonVersionId: string;
    lessonVersionNumber: number | null;
    lessonTitle: string | null;
    orderIndex: number;
}

export interface ProgramVersion {
    id: string;
    versionNumber: number;
    status: string;
    createdBy: string | null;
    createdAt: string;
    publishedAt: string | null;
    items: ProgramItem[];
}

export interface ProgramDiffLesson {
    lessonId: string;
    skillId: string;
    lessonVersionId: string;
    lessonVersionNumber: number | null;
    lessonTitle: string | null;
    orderIndex: number;
}

export interface ProgramDiffVersionChange {
    lessonId: string;
    skillId: string;
    lessonTitle: string | null;
    fromLessonVersionId: string;
    fromLessonVersionNumber: number | null;
    toLessonVersionId: string;
    toLessonVersionNumber: number | null;
    isBreaking: boolean;
}

export interface ProgramDiffMove {
    lessonId: string;
    lessonTitle: string | null;
    fromSkillId: string;
    toSkillId: string;
    fromOrderIndex: number;
    toOrderIndex: number;
}

export interface ProgramDiff {
    fromProgramVersionId: string;
    fromVersionNumber: number;
    toProgramVersionId: string;
    toVersionNumber: number;
    addedLessons: ProgramDiffLesson[];
    removedLessons: ProgramDiffLesson[];
    changedLessons: ProgramDiffVersionChange[];
    movedLessons: ProgramDiffMove[];
    hasBreakingChanges: boolean;
}

/**
 * One learner's pin. `userId` is the only identity the endpoint carries — learning-service holds no
 * replica of a person's name, so the screen resolves names elsewhere and shows an id fragment for
 * anybody it cannot resolve.
 */
export interface ProgramEnrollment {
    userId: string;
    programVersionId: string;
    programVersionNumber: number;
    previousProgramVersionId: string | null;
    enrolledAt: string;
    switchedAt: string | null;
}

/** `createdNewVersion: false` means the draft matched the last published version and was discarded. */
export interface PublishProgramVersionResult {
    version: ProgramVersion;
    createdNewVersion: boolean;
}
