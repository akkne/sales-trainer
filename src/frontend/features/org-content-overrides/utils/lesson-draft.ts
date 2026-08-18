/**
 * What the lesson editor knows about a lesson's version history, derived from
 * `GET /admin/lessons/{id}/versions` alone (docs/TENANCY/ADMIN_UI_DESIGN.md O19).
 *
 * The backend guarantees at most one draft per lesson with a partial unique index; this reads that
 * guarantee rather than re-deciding it, and picks the newest row by version number because the list
 * order is the server's business.
 */

import type { LessonVersionSummary } from "../types/lesson-editor";

export interface LessonVersionState {
    /** The live draft, if the lesson has one. Publication is what closes it. */
    draft: LessonVersionSummary | null;
    /** The newest frozen version — the one the team is answering right now. */
    latestPublished: LessonVersionSummary | null;
    /** True while edits are invisible to the team: the reason the sticky banner exists. */
    hasUnpublishedDraft: boolean;
}

function newestFirst(versions: readonly LessonVersionSummary[]): LessonVersionSummary[] {
    return [...versions].sort((left, right) => right.versionNumber - left.versionNumber);
}

export function resolveLessonVersionState(
    versions: readonly LessonVersionSummary[]
): LessonVersionState {
    const ordered = newestFirst(versions);
    const draft = ordered.find((version) => version.status === "draft") ?? null;
    const latestPublished = ordered.find((version) => version.status === "published") ?? null;

    return { draft, latestPublished, hasUnpublishedDraft: draft !== null };
}

/**
 * The line beside the lesson title: «ваша версия · черновик поверх v4». `isOwnCopy` is what the
 * panel could establish about ownership — see the O19 notes in docs/TESTING/ORG_PANEL.md, the
 * lesson read endpoints do not return an owner.
 */
export function describeLessonVersionState(state: LessonVersionState, isOwnCopy: boolean): string {
    const ownership = isOwnCopy ? "ваша версия" : "общая библиотека";

    if (state.hasUnpublishedDraft) {
        return state.latestPublished === null
            ? `${ownership} · черновик, ещё не публиковался`
            : `${ownership} · черновик поверх v${state.latestPublished.versionNumber}`;
    }

    if (state.latestPublished === null) return `${ownership} · версий пока нет`;

    return `${ownership} · опубликована v${state.latestPublished.versionNumber}`;
}
