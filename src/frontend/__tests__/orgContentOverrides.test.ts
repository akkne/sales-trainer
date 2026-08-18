import { describe, expect, it } from "vitest";

import {
    NO_AUTO_MERGE_NOTICE,
    OVERRIDE_KIND_LABELS,
    OVERRIDE_STATE_COPY,
    PUBLISH_SCOPE_OPTIONS,
    describeLessonVersionStatus,
    describeOverrideKind,
    describeUnpublishedDraft,
    resolveOverrideState,
} from "@/features/org-content-overrides/constants/override-dictionary";
import {
    mergeOverrideRows,
    selectStaleRows,
    toDialogModeOverrideRow,
    toLearningOverrideRow,
} from "@/features/org-content-overrides/utils/override-rows";
import {
    alignComparisonBlocks,
    buildComparisonBlocks,
} from "@/features/org-content-overrides/utils/comparison-blocks";
import {
    buildAccuracyChartModel,
    describeAccuracyPoint,
    describeUnversionedAttempts,
    toAccuracyPercent,
} from "@/features/org-content-overrides/utils/accuracy-series";
import {
    describeLessonVersionState,
    resolveLessonVersionState,
} from "@/features/org-content-overrides/utils/lesson-draft";
import {
    describeExerciseType,
    moveExerciseInList,
    summarizeExerciseContent,
} from "@/features/org-content-overrides/utils/exercise-summary";
import type {
    ContentOverrideSummary,
    DialogModeOverrideSummary,
} from "@/features/org-content-overrides/types/content-override";
import type {
    LessonAccuracySegment,
    LessonAccuracySeries,
    LessonAttemptStatistics,
    LessonVersionSummary,
} from "@/features/org-content-overrides/types/lesson-editor";

/**
 * Slice 7 — O14/O15/O19 (docs/TENANCY/ADMIN_UI_DESIGN.md). What is checked here is the vocabulary
 * the screens speak and the two pieces of arithmetic they are allowed to do: which state an
 * override is in, and how an accuracy series turns into segments. Everything else — diffs, merges,
 * staleness — belongs to the server on purpose, and the tests below assert that the client does not
 * quietly grow its own.
 */

function buildLearningOverride(
    overrides: Partial<ContentOverrideSummary> = {}
): ContentOverrideSummary {
    return {
        kind: "lessons",
        overrideId: "override-1",
        baseId: "base-1",
        title: "Работа с ценой",
        isStale: false,
        forkedFrom: "version-3",
        baseCurrent: "version-3",
        ...overrides,
    };
}

function buildDialogModeOverride(
    overrides: Partial<DialogModeOverrideSummary> = {}
): DialogModeOverrideSummary {
    return {
        overrideId: "mode-override-1",
        baseModeId: "mode-1",
        bundleId: "bundle-1",
        key: "tough-buyer",
        title: "«Жёсткий закупщик»",
        isStale: false,
        forkedFromHash: "hash-a",
        baseCurrentHash: "hash-a",
        ...overrides,
    };
}

describe("resolveOverrideState", () => {
    it("reads a stale override with a known fork point as «оригинал обновился»", () => {
        const state = resolveOverrideState({ isStale: true, forkedFrom: "v3", baseCurrent: "v5" });

        expect(state).toBe("base_moved");
        expect(OVERRIDE_STATE_COPY[state].label).toBe("оригинал обновился");
        expect(OVERRIDE_STATE_COPY[state].needsReview).toBe(true);
    });

    it("keeps «основа неизвестна» a separate answer from «оригинал обновился»", () => {
        const state = resolveOverrideState({ isStale: true, forkedFrom: null, baseCurrent: "v5" });

        expect(state).toBe("base_unknown");
        expect(OVERRIDE_STATE_COPY[state].label).toBe("основа неизвестна");
        expect(OVERRIDE_STATE_COPY[state].needsReview).toBe(true);
    });

    it("reads a fresh override as «совпадает с базой», which is not work to do", () => {
        const state = resolveOverrideState({ isStale: false, forkedFrom: "v3", baseCurrent: "v3" });

        expect(state).toBe("in_sync");
        expect(OVERRIDE_STATE_COPY[state].needsReview).toBe(false);
    });

    it("does not claim a copy matches a base that has never been published", () => {
        const state = resolveOverrideState({ isStale: false, forkedFrom: null, baseCurrent: null });

        expect(state).toBe("base_unpublished");
        expect(OVERRIDE_STATE_COPY[state].label).not.toBe("совпадает с базой");
    });
});

describe("the §1.4 dictionary", () => {
    it("names all four kinds exactly as the design fixes them", () => {
        expect(OVERRIDE_KIND_LABELS).toEqual({
            lessons: "урок",
            techniques: "техника",
            "reference-materials": "справка",
            modes: "режим диалога",
        });
    });

    it("falls back to the raw value for a kind it does not know, never to a guess", () => {
        expect(describeOverrideKind("quotes")).toBe("quotes");
    });

    it("translates the three lesson version statuses and passes an unknown one through", () => {
        expect(describeLessonVersionStatus("draft")).toBe("черновик");
        expect(describeLessonVersionStatus("published")).toBe("опубликована");
        expect(describeLessonVersionStatus("archived")).toBe("в архиве");
        expect(describeLessonVersionStatus("frozen")).toBe("frozen");
    });

    it("offers exactly two publish scopes and neither of them is a default", () => {
        expect(PUBLISH_SCOPE_OPTIONS).toHaveLength(2);
        expect(PUBLISH_SCOPE_OPTIONS.map((option) => option.isBreaking)).toEqual([false, true]);
    });

    it("states that nothing is merged automatically", () => {
        expect(NO_AUTO_MERGE_NOTICE).toContain("не сливаем");
    });

    it("names the version the team is still answering, or says there is none", () => {
        expect(describeUnpublishedDraft(4)).toContain("версию 4");
        expect(describeUnpublishedDraft(null)).toContain("не видит этот урок");
    });
});

describe("mergeOverrideRows", () => {
    it("puts both services into one table and marks the fourth kind as «режим диалога»", () => {
        const rows = mergeOverrideRows([buildLearningOverride()], [buildDialogModeOverride()]);

        expect(rows).toHaveLength(2);
        expect(rows.map((row) => row.kind)).toContain("modes");
    });

    it("gives every row a key unique across services", () => {
        const sharedId = "same-id";
        const rows = mergeOverrideRows(
            [buildLearningOverride({ overrideId: sharedId })],
            [buildDialogModeOverride({ overrideId: sharedId })]
        );

        expect(new Set(rows.map((row) => row.rowId)).size).toBe(2);
    });

    it("sorts stale rows first — that is what the screen exists for", () => {
        const rows = mergeOverrideRows(
            [
                buildLearningOverride({ overrideId: "fresh", title: "Аа", isStale: false }),
                buildLearningOverride({
                    overrideId: "stale",
                    title: "Яя",
                    isStale: true,
                    baseCurrent: "version-5",
                }),
            ],
            []
        );

        expect(rows[0].overrideId).toBe("stale");
    });

    it("links each row at its own review screen, kind included in the path", () => {
        expect(toLearningOverrideRow(buildLearningOverride()).href).toBe(
            "/org/content/overrides/lessons/override-1"
        );
        expect(toDialogModeOverrideRow(buildDialogModeOverride()).href).toBe(
            "/org/content/overrides/modes/mode-override-1"
        );
    });

    it("reads the ai-service hash fields as the same fork markers the learning rows use", () => {
        const row = toDialogModeOverrideRow(
            buildDialogModeOverride({ isStale: true, forkedFromHash: null })
        );

        expect(row.state).toBe("base_unknown");
        expect(row.baseId).toBe("mode-1");
    });

    it("selects the stale queue without recomputing staleness", () => {
        const rows = mergeOverrideRows(
            [
                buildLearningOverride({ overrideId: "a", isStale: true }),
                buildLearningOverride({ overrideId: "b", isStale: false }),
            ],
            []
        );

        expect(selectStaleRows(rows).map((row) => row.overrideId)).toEqual(["a"]);
    });
});

describe("comparison blocks", () => {
    const lessonSnapshot = {
        title: "Работа с ценой",
        schemaVersion: 1,
        exercises: [
            { exerciseId: "ex-1", type: "choose_option", content: { situation: "Дорого" }, customAiPrompt: null },
        ],
    };

    it("turns a lesson snapshot into a title block plus one block per exercise", () => {
        const blocks = buildComparisonBlocks(lessonSnapshot);

        expect(blocks.map((block) => block.key)).toEqual(["title", "exercise:ex-1"]);
        expect(blocks[0].label).toBe("Заголовок");
    });

    it("never shows schemaVersion as content", () => {
        const blocks = buildComparisonBlocks({ schemaVersion: 1, title: "Т", body: "Б" });

        expect(blocks.map((block) => block.key)).not.toContain("schemaVersion");
    });

    it("reads a technique document generically, by its own top-level keys", () => {
        const blocks = buildComparisonBlocks({ name: "Три да", summary: "Кратко", schemaVersion: 1 });

        expect(blocks.map((block) => block.label)).toEqual(["Название", "Кратко"]);
    });

    it("returns nothing for a document that is not an object", () => {
        expect(buildComparisonBlocks(null)).toEqual([]);
        expect(buildComparisonBlocks("текст")).toEqual([]);
    });

    it("marks a block as differing only when its whole text is not identical", () => {
        const rows = alignComparisonBlocks([
            lessonSnapshot,
            { ...lessonSnapshot, title: "Работа с ценой" },
        ]);

        expect(rows.find((row) => row.key === "title")?.differs).toBe(false);

        const changed = alignComparisonBlocks([
            lessonSnapshot,
            { ...lessonSnapshot, title: "Работа с ценой (наша)" },
        ]);

        expect(changed.find((row) => row.key === "title")?.differs).toBe(true);
    });

    it("treats a block missing from one column as a difference rather than dropping it", () => {
        const rows = alignComparisonBlocks([
            lessonSnapshot,
            { title: "Работа с ценой", schemaVersion: 1, exercises: [] },
        ]);

        const exerciseRow = rows.find((row) => row.key === "exercise:ex-1");
        expect(exerciseRow?.cells[1]).toBeNull();
        expect(exerciseRow?.differs).toBe(true);
    });

    it("keeps a column per document, so a two-column review has two cells everywhere", () => {
        const rows = alignComparisonBlocks([{ chatSystemPrompt: "а" }, { chatSystemPrompt: "б" }]);

        expect(rows).toHaveLength(1);
        expect(rows[0].cells).toEqual(["а", "б"]);
    });
});

describe("the accuracy series", () => {
    function buildStatistics(
        overrides: Partial<LessonAttemptStatistics> = {}
    ): LessonAttemptStatistics {
        return {
            attemptCount: 10,
            correctAttemptCount: 6,
            accuracy: 0.6,
            averageScore: 60,
            firstAttemptAt: null,
            lastAttemptAt: null,
            ...overrides,
        };
    }

    function buildSegment(overrides: Partial<LessonAccuracySegment> = {}): LessonAccuracySegment {
        return {
            startVersionNumber: 1,
            endVersionNumber: 2,
            versionNumbers: [1, 2],
            versionIds: ["v1", "v2"],
            startsAtBreakingChange: false,
            statistics: buildStatistics(),
            ...overrides,
        };
    }

    function buildSeries(overrides: Partial<LessonAccuracySeries> = {}): LessonAccuracySeries {
        return {
            lessonId: "lesson-1",
            segments: [buildSegment()],
            unversionedAttempts: buildStatistics({ attemptCount: 0, accuracy: 0 }),
            ...overrides,
        };
    }

    it("turns the 0..1 fraction into whole percents", () => {
        expect(toAccuracyPercent(0.6, 10)).toBe(60);
        expect(toAccuracyPercent(0.666, 3)).toBe(67);
    });

    it("reports «no percentage» rather than 0% for a segment nobody has answered", () => {
        expect(toAccuracyPercent(0, 0)).toBeNull();
        expect(describeAccuracyPoint({ versionNumber: 4, accuracyPercent: null, attemptCount: 0 })).toBe(
            "на эту версию ещё никто не отвечал"
        );
    });

    it("keeps every segment separate, so nothing is averaged across a breaking publish", () => {
        const model = buildAccuracyChartModel(
            buildSeries({
                segments: [
                    buildSegment({ versionNumbers: [1, 2], startVersionNumber: 1, endVersionNumber: 2 }),
                    buildSegment({
                        versionNumbers: [3, 4],
                        startVersionNumber: 3,
                        endVersionNumber: 4,
                        startsAtBreakingChange: true,
                        statistics: buildStatistics({ accuracy: 0.9 }),
                    }),
                ],
            })
        );

        expect(model.segments).toHaveLength(2);
        expect(model.segments[1].startsAtBreakingChange).toBe(true);
        expect(model.segments[0].points[0].accuracyPercent).toBe(60);
        expect(model.segments[1].points[0].accuracyPercent).toBe(90);
    });

    it("draws a segment with no attempts instead of skipping it", () => {
        const model = buildAccuracyChartModel(
            buildSeries({
                segments: [buildSegment({ statistics: buildStatistics({ attemptCount: 0, accuracy: 0 }) })],
            })
        );

        expect(model.segments[0].points).toHaveLength(2);
        expect(model.segments[0].points[0].accuracyPercent).toBeNull();
        expect(model.hasAnyAttempt).toBe(false);
    });

    it("keeps unversioned attempts out of every segment and puts them in a footnote", () => {
        const model = buildAccuracyChartModel(
            buildSeries({ unversionedAttempts: buildStatistics({ attemptCount: 340 }) })
        );

        expect(model.versionNumbers).not.toContain(0);
        expect(model.unversionedAttemptCount).toBe(340);
        expect(describeUnversionedAttempts(340)).toBe(
            "340 попыток записаны до появления версий — их нет ни в одном отрезке."
        );
    });

    it("writes no footnote when there are no unversioned attempts", () => {
        expect(describeUnversionedAttempts(0)).toBeNull();
    });

    it("agrees with Russian plurals on the footnote count", () => {
        expect(describeUnversionedAttempts(1)).toContain("1 попытка");
        expect(describeUnversionedAttempts(3)).toContain("3 попытки");
        expect(describeUnversionedAttempts(11)).toContain("11 попыток");
        expect(describeUnversionedAttempts(21)).toContain("21 попытка");
    });

    it("says the lesson has no versions rather than drawing an empty chart", () => {
        const model = buildAccuracyChartModel(buildSeries({ segments: [] }));

        expect(model.segments).toEqual([]);
        expect(model.versionNumbers).toEqual([]);
    });
});

describe("the lesson's version state", () => {
    function buildVersion(overrides: Partial<LessonVersionSummary> = {}): LessonVersionSummary {
        return {
            id: "version-1",
            lessonId: "lesson-1",
            versionNumber: 1,
            status: "published",
            contentHash: "hash",
            baseVersionId: null,
            isBreaking: false,
            createdBy: null,
            createdAt: "2026-08-01T00:00:00Z",
            publishedAt: "2026-08-01T00:00:00Z",
            ...overrides,
        };
    }

    it("finds the live draft and the newest published version regardless of list order", () => {
        const state = resolveLessonVersionState([
            buildVersion({ id: "v5", versionNumber: 5, status: "draft", publishedAt: null }),
            buildVersion({ id: "v4", versionNumber: 4 }),
            buildVersion({ id: "v3", versionNumber: 3 }),
        ]);

        expect(state.draft?.id).toBe("v5");
        expect(state.latestPublished?.versionNumber).toBe(4);
        expect(state.hasUnpublishedDraft).toBe(true);
    });

    it("reports no draft for a lesson whose newest version is published", () => {
        const state = resolveLessonVersionState([buildVersion({ versionNumber: 2 })]);

        expect(state.hasUnpublishedDraft).toBe(false);
        expect(state.draft).toBeNull();
    });

    it("says what the header says: whose copy, and which version the draft sits on", () => {
        const withDraft = resolveLessonVersionState([
            buildVersion({ versionNumber: 4 }),
            buildVersion({ id: "v5", versionNumber: 5, status: "draft", publishedAt: null }),
        ]);

        expect(describeLessonVersionState(withDraft, true)).toBe("ваша версия · черновик поверх v4");
        expect(describeLessonVersionState(withDraft, false)).toContain("общая библиотека");
    });

    it("does not invent a version number for a lesson that has never been published", () => {
        const state = resolveLessonVersionState([]);

        expect(describeLessonVersionState(state, true)).toBe("ваша версия · версий пока нет");
    });
});

describe("the exercise list", () => {
    it("names the types in Russian for this panel", () => {
        expect(describeExerciseType("choose_option")).toBe("Выбор варианта");
        expect(describeExerciseType("free_text")).toBe("Свободный ответ");
    });

    it("passes an unknown type through instead of guessing at it", () => {
        expect(describeExerciseType("mystery_type")).toBe("mystery_type");
    });

    it("previews the authored sentence for a type it knows", () => {
        expect(summarizeExerciseContent("choose_option", { situation: "Клиент говорит: дорого" })).toBe(
            "Клиент говорит: дорого"
        );
    });

    it("renders no preview at all for an unknown type", () => {
        expect(summarizeExerciseContent("mystery_type", { situation: "…" })).toBe("");
    });

    it("renumbers positions 1..n after a move, with no holes", () => {
        const moved = moveExerciseInList(
            [
                { id: "a", orderInLesson: 1 },
                { id: "b", orderInLesson: 2 },
                { id: "c", orderInLesson: 3 },
            ],
            2,
            0
        );

        expect(moved.map((exercise) => exercise.id)).toEqual(["c", "a", "b"]);
        expect(moved.map((exercise) => exercise.orderInLesson)).toEqual([1, 2, 3]);
    });

    it("leaves an out-of-range move as a plain renumbering rather than throwing", () => {
        const unchanged = moveExerciseInList([{ id: "a", orderInLesson: 7 }], 0, 5);

        expect(unchanged).toEqual([{ id: "a", orderInLesson: 1 }]);
    });
});

describe("no gamification", () => {
    it("mentions neither XP nor streaks nor leagues anywhere in the slice's vocabulary", () => {
        const vocabulary = JSON.stringify([
            OVERRIDE_STATE_COPY,
            OVERRIDE_KIND_LABELS,
            PUBLISH_SCOPE_OPTIONS,
        ]).toLowerCase();

        for (const forbidden of ["xp", "стрик", "streak", "лига", "league"]) {
            expect(vocabulary).not.toContain(forbidden);
        }
    });
});
