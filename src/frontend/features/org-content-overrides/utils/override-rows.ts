/**
 * Two lists from two services, one table (docs/TENANCY/ADMIN_UI_DESIGN.md O14).
 *
 * Splitting them into tabs was refused in the design: to the customer «что я поменял под себя и что
 * из этого отстало» is one question, and which service stores the row is our internal geography.
 * The merge is here rather than in the page so that both the table and the «только устаревшие»
 * counter read the same rows.
 */

import {
    OVERRIDE_KIND_ORDER,
    resolveOverrideState,
    type OverrideState,
} from "../constants/override-dictionary";
import type {
    ContentOverrideSummary,
    DialogModeOverrideSummary,
    OverrideKind,
} from "../types/content-override";

export interface OverrideRow {
    /** Unique across the merged table — an override id is only unique inside its own service. */
    rowId: string;
    kind: OverrideKind;
    overrideId: string;
    baseId: string;
    title: string;
    isStale: boolean;
    forkedFrom: string | null;
    baseCurrent: string | null;
    state: OverrideState;
    href: string;
}

export function buildOverrideHref(kind: OverrideKind, overrideId: string): string {
    return `/org/content/overrides/${kind}/${overrideId}`;
}

export function toLearningOverrideRow(summary: ContentOverrideSummary): OverrideRow {
    return {
        rowId: `${summary.kind}:${summary.overrideId}`,
        kind: summary.kind,
        overrideId: summary.overrideId,
        baseId: summary.baseId,
        title: summary.title,
        isStale: summary.isStale,
        forkedFrom: summary.forkedFrom,
        baseCurrent: summary.baseCurrent,
        state: resolveOverrideState(summary),
        href: buildOverrideHref(summary.kind, summary.overrideId),
    };
}

/**
 * The ai-service row names its fork markers `forkedFromHash`/`baseCurrentHash` — a mode has no
 * version table, so the marker is a fingerprint. The state vocabulary is the same one either way.
 */
export function toDialogModeOverrideRow(summary: DialogModeOverrideSummary): OverrideRow {
    return {
        rowId: `modes:${summary.overrideId}`,
        kind: "modes",
        overrideId: summary.overrideId,
        baseId: summary.baseModeId,
        title: summary.title,
        isStale: summary.isStale,
        forkedFrom: summary.forkedFromHash,
        baseCurrent: summary.baseCurrentHash,
        state: resolveOverrideState({
            isStale: summary.isStale,
            forkedFrom: summary.forkedFromHash,
            baseCurrent: summary.baseCurrentHash,
        }),
        href: buildOverrideHref("modes", summary.overrideId),
    };
}

/**
 * Stale rows first — that is the whole reason the screen exists. Inside each half, by kind and then
 * by title, so the order does not shuffle between reads the way an id order would.
 */
export function mergeOverrideRows(
    learningOverrides: readonly ContentOverrideSummary[],
    dialogModeOverrides: readonly DialogModeOverrideSummary[]
): OverrideRow[] {
    const rows = [
        ...learningOverrides.map(toLearningOverrideRow),
        ...dialogModeOverrides.map(toDialogModeOverrideRow),
    ];

    return rows.sort((left, right) => {
        if (left.isStale !== right.isStale) return left.isStale ? -1 : 1;

        const kindDifference = OVERRIDE_KIND_ORDER[left.kind] - OVERRIDE_KIND_ORDER[right.kind];
        if (kindDifference !== 0) return kindDifference;

        return left.title.localeCompare(right.title, "ru");
    });
}

export function selectStaleRows(rows: readonly OverrideRow[]): OverrideRow[] {
    return rows.filter((row) => row.isStale);
}
