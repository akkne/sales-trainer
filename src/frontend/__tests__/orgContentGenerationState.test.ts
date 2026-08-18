import { describe, expect, it } from "vitest";

import { ApiError } from "@/shared/api/api-client";
import {
    CONTENT_GENERATION_STATUS_FILTER_ORDER,
    CONTENT_SUFFICIENCY_CODE_FALLBACK_LABELS,
    JOB_POLL_INTERVAL_MILLISECONDS,
    MATERIAL_MAXIMUM_LENGTH,
    describeJobStatus,
    describeRunOrigin,
    describeSufficiencyGapMessage,
    jobStatusTone,
} from "@/features/org-content-generation/constants/generation-dictionary";
import {
    canApproveStructure,
    canEditStructure,
    canOpenStructureFromRefusal,
    canRetryRun,
    canSupplementMaterial,
    describeJobProgress,
    firstSufficiencyGapMessage,
    isWorkerOwnedStatus,
    readableSufficiencyGaps,
    resolveJobLayout,
    resolveJobPollInterval,
} from "@/features/org-content-generation/utils/job-state";
import {
    describeContentGenerationFailure,
    readInsufficiencyFromConflict,
    validateStartMaterial,
    validateStartTitle,
} from "@/features/org-content-generation/utils/api-failure";
import type {
    ContentGenerationJob,
    ContentInsufficiency,
} from "@/features/org-content-generation/types/content-generation";

/**
 * Slice 5 — O9/O10/O11 (docs/TENANCY/ADMIN_UI_DESIGN.md, docs/CONTENT_PIPELINE.md).
 *
 * What is pinned here is the pipeline's two load-bearing ideas: **the checkpoint** — nothing may
 * offer approval outside `awaiting_review` — and **the refusal** — it is a state with two answers
 * and never a bypass.
 */

const EVERY_STATUS = [
    "structuring",
    "awaiting_review",
    "insufficient",
    "generating",
    "completed",
    "failed",
] as const;

function buildJob(overrides: Partial<ContentGenerationJob> = {}): ContentGenerationJob {
    return {
        id: "11111111-1111-1111-1111-111111111111",
        title: "Возражения по цене, октябрь",
        status: "awaiting_review",
        gapSourceRef: null,
        sourceMaterial: "Материал",
        structure: {
            product: "Облачный учёт складских остатков",
            icp: null,
            tone: null,
            objections: [],
            scriptStages: [],
            glossary: {},
            bannedClaims: [],
        },
        insufficiency: null,
        structuredAt: "2026-08-18T09:00:00Z",
        approvedAt: null,
        producedLessonId: null,
        producedLessonVersionId: null,
        producedExerciseCount: 0,
        generatedAt: null,
        failureReason: null,
        createdAt: "2026-08-18T08:59:00Z",
        updatedAt: "2026-08-18T09:00:00Z",
        ...overrides,
    };
}

describe("resolveJobLayout", () => {
    it("maps six statuses onto five layouts, sharing one between the two worker-owned states", () => {
        expect(resolveJobLayout("structuring")).toBe("in_progress");
        expect(resolveJobLayout("generating")).toBe("in_progress");
        expect(resolveJobLayout("awaiting_review")).toBe("checkpoint");
        expect(resolveJobLayout("insufficient")).toBe("insufficient");
        expect(resolveJobLayout("completed")).toBe("completed");
        expect(resolveJobLayout("failed")).toBe("failed");
    });

    it("answers 'unknown' for a status this build does not know, never a guessed layout", () => {
        expect(resolveJobLayout("promoted")).toBe("unknown");
        expect(resolveJobLayout("")).toBe("unknown");
    });
});

describe("the checkpoint gate", () => {
    it("offers approval in exactly one state — awaiting_review", () => {
        const approvableStatuses = EVERY_STATUS.filter(canApproveStructure);

        expect(approvableStatuses).toEqual(["awaiting_review"]);
    });

    it("never offers approval on a refused run: the threshold is arguable, not waivable", () => {
        expect(canApproveStructure("insufficient")).toBe(false);
    });

    it("opens the structure editor in exactly the two states PUT …/structure accepts", () => {
        const editableStatuses = EVERY_STATUS.filter(canEditStructure);

        expect(editableStatuses).toEqual(["awaiting_review", "insufficient"]);
    });

    it("offers «добавить материал» only on a refused run — 409 everywhere else", () => {
        expect(EVERY_STATUS.filter(canSupplementMaterial)).toEqual(["insufficient"]);
    });

    it("offers a retry only on a failed run", () => {
        expect(EVERY_STATUS.filter(canRetryRun)).toEqual(["failed"]);
    });
});

describe("canOpenStructureFromRefusal", () => {
    it("is true for a refusal decided from the structure, which means one exists to open", () => {
        const job = buildJob({
            status: "insufficient",
            insufficiency: {
                stage: "structure",
                gaps: [{ code: "no_objections", message: "В материале нет возражений." }],
                note: null,
            },
        });

        expect(canOpenStructureFromRefusal(job)).toBe(true);
    });

    it("is false at stage 'material': nothing was extracted, so there is nothing to edit", () => {
        const job = buildJob({
            status: "insufficient",
            structure: null,
            insufficiency: {
                stage: "material",
                gaps: [{ code: "too_short", message: "Материала слишком мало." }],
                note: null,
            },
        });

        expect(canOpenStructureFromRefusal(job)).toBe(false);
    });

    it("is false at stage 'material' even if a structure somehow survives on the row", () => {
        const job = buildJob({
            status: "insufficient",
            insufficiency: {
                stage: "material",
                gaps: [{ code: "off_topic", message: "Материал не про продажи." }],
                note: null,
            },
        });

        expect(canOpenStructureFromRefusal(job)).toBe(false);
    });

    it("is false for any run that is not refused", () => {
        expect(canOpenStructureFromRefusal(buildJob({ status: "awaiting_review" }))).toBe(false);
        expect(canOpenStructureFromRefusal(buildJob({ status: "completed" }))).toBe(false);
    });
});

describe("polling", () => {
    it("polls every three seconds while a worker owns the run and never otherwise", () => {
        expect(resolveJobPollInterval("structuring", false)).toBe(JOB_POLL_INTERVAL_MILLISECONDS);
        expect(resolveJobPollInterval("generating", false)).toBe(JOB_POLL_INTERVAL_MILLISECONDS);

        expect(resolveJobPollInterval("awaiting_review", false)).toBe(false);
        expect(resolveJobPollInterval("insufficient", false)).toBe(false);
        expect(resolveJobPollInterval("completed", false)).toBe(false);
        expect(resolveJobPollInterval("failed", false)).toBe(false);
    });

    it("stops entirely behind a hidden tab, including mid-generation", () => {
        expect(resolveJobPollInterval("structuring", true)).toBe(false);
        expect(resolveJobPollInterval("generating", true)).toBe(false);
    });

    it("does not poll before the first response has told it what the status is", () => {
        expect(resolveJobPollInterval(undefined, false)).toBe(false);
    });

    it("agrees with isWorkerOwnedStatus about which two states those are", () => {
        expect(EVERY_STATUS.filter(isWorkerOwnedStatus)).toEqual(["structuring", "generating"]);
    });
});

describe("describeJobProgress", () => {
    it("tells the reviewer they may leave while the material is being read", () => {
        expect(describeJobProgress("structuring").title).toBe("Разбираем материал…");
        expect(describeJobProgress("structuring").description).toContain("Можно закрыть страницу");
    });

    it("says «собираем упражнения» for the half that has already been paid for", () => {
        expect(describeJobProgress("generating").title).toBe("Собираем упражнения…");
    });
});

describe("the refusal, as the screen reads it", () => {
    const insufficiency: ContentInsufficiency = {
        stage: "structure",
        gaps: [
            {
                code: "no_objections",
                message: "В материале нет ни одного возражения клиента. Добавьте примеры возражений.",
            },
            { code: "no_icp", message: "Не описано, кому вы продаёте." },
        ],
        note: "модель считает материал маркетинговым",
    };

    it("keeps the gaps as separate items, never joined into a paragraph", () => {
        const gaps = readableSufficiencyGaps(insufficiency);

        expect(gaps).toHaveLength(2);
        expect(gaps.map((gap) => gap.code)).toEqual(["no_objections", "no_icp"]);
    });

    it("prints the first gap in the list row, because that is usually the actionable one", () => {
        expect(firstSufficiencyGapMessage(insufficiency)).toBe(
            "В материале нет ни одного возражения клиента. Добавьте примеры возражений."
        );
    });

    it("has nothing to print for a run that was not refused", () => {
        expect(firstSufficiencyGapMessage(null)).toBeNull();
        expect(readableSufficiencyGaps(undefined)).toEqual([]);
    });

    it("falls back to the code's own sentence when the server's message is blank", () => {
        expect(describeSufficiencyGapMessage("no_script", "   ")).toBe(
            CONTENT_SUFFICIENCY_CODE_FALLBACK_LABELS.no_script
        );
    });

    it("drops a gap that is neither a known code nor a sentence, rather than showing an empty bullet", () => {
        expect(describeSufficiencyGapMessage("send_us_your_contract", "")).toBeNull();

        const withInventedCode: ContentInsufficiency = {
            stage: "structure",
            gaps: [
                { code: "send_us_your_contract", message: "" },
                { code: "no_product", message: "Не понятно, что вы продаёте." },
            ],
            note: null,
        };

        expect(readableSufficiencyGaps(withInventedCode).map((gap) => gap.code)).toEqual([
            "no_product",
        ]);
    });

    it("never surfaces the model's note — it is a diagnostic, not the customer's text", () => {
        const renderedText = readableSufficiencyGaps(insufficiency)
            .map((gap) => describeSufficiencyGapMessage(gap.code, gap.message))
            .join(" ");

        expect(renderedText).not.toContain("модель считает");
    });
});

describe("readInsufficiencyFromConflict", () => {
    it("reads the gap list out of the 409 body that approve answers with", () => {
        const conflict = new ApiError(409, {
            message: "The structure is too thin to generate from.",
            insufficiency: {
                stage: "structure",
                gaps: [{ code: "no_objections", message: "Нет ни одного возражения." }],
                note: null,
            },
        });

        expect(readInsufficiencyFromConflict(conflict)?.gaps).toEqual([
            { code: "no_objections", message: "Нет ни одного возражения." },
        ]);
    });

    it("returns null for the other kind of 409 — «прогон уже ушёл дальше»", () => {
        const conflict = new ApiError(409, { message: "The run is already generating." });

        expect(readInsufficiencyFromConflict(conflict)).toBeNull();
    });

    it("returns null for a 409 whose gap list is empty: an unactionable refusal is not one", () => {
        const conflict = new ApiError(409, {
            message: "no",
            insufficiency: { stage: "structure", gaps: [], note: null },
        });

        expect(readInsufficiencyFromConflict(conflict)).toBeNull();
    });

    it("ignores a non-conflict status entirely", () => {
        expect(readInsufficiencyFromConflict(new ApiError(400, { message: "bad" }))).toBeNull();
        expect(readInsufficiencyFromConflict(new Error("network"))).toBeNull();
    });
});

describe("describeContentGenerationFailure", () => {
    it("turns the stale-state 409 into «обновите страницу», not «попробуйте ещё раз»", () => {
        const conflict = new ApiError(409, { message: "The run is already generating." });

        expect(describeContentGenerationFailure(conflict, "approve")).toContain("Обновите страницу");
    });

    it("names the run, not the lesson, on a 404 from a run route", () => {
        const notFound = new ApiError(404, {});

        expect(describeContentGenerationFailure(notFound, "retry")).toContain("Прогон не найден");
        expect(describeContentGenerationFailure(notFound, "unarchiveLesson")).toContain(
            "Урок не найден"
        );
    });

    it("repeats the server's own 400 sentence rather than paraphrasing it", () => {
        const badRequest = new ApiError(400, { message: "Material must not be empty." });

        expect(describeContentGenerationFailure(badRequest, "start")).toBe(
            "Material must not be empty."
        );
    });

    it("falls back to a plain sentence when the failure is not an ApiError at all", () => {
        expect(describeContentGenerationFailure(new Error("offline"), "saveStructure")).toContain(
            "правки остались на экране"
        );
    });
});

describe("the start form's two client-side rules", () => {
    it("refuses an empty textarea", () => {
        expect(validateStartMaterial("   ")).not.toBeNull();
        expect(validateStartTitle("")).not.toBeNull();
    });

    it("refuses material over the server's 60 000-character ceiling", () => {
        expect(validateStartMaterial("а".repeat(MATERIAL_MAXIMUM_LENGTH))).toBeNull();
        expect(validateStartMaterial("а".repeat(MATERIAL_MAXIMUM_LENGTH + 1))).not.toBeNull();
    });

    it("does NOT refuse thin material — that is a run in `insufficient`, not a form error", () => {
        expect(validateStartMaterial("Продаём CRM.")).toBeNull();
        expect(validateStartMaterial("три слайда")).toBeNull();
    });
});

describe("the status dictionary", () => {
    it("uses the fixed §1.4 wording for all six states", () => {
        expect(describeJobStatus("structuring")).toBe("Разбираем материал");
        expect(describeJobStatus("awaiting_review")).toBe("Ждёт проверки");
        expect(describeJobStatus("insufficient")).toBe("Материала не хватает");
        expect(describeJobStatus("generating")).toBe("Генерируем");
        expect(describeJobStatus("completed")).toBe("Готово");
        expect(describeJobStatus("failed")).toBe("Ошибка");
    });

    it("shows an unknown status as itself rather than inventing a translation", () => {
        expect(describeJobStatus("promoted")).toBe("promoted");
        expect(jobStatusTone("promoted")).toBe("neutral");
    });

    it("colours a refusal amber and a failure red — nothing broke in the first case", () => {
        expect(jobStatusTone("insufficient")).toBe("warn");
        expect(jobStatusTone("failed")).toBe("bad");
    });

    it("puts the two states needing a person at the front of the filter", () => {
        expect(CONTENT_GENERATION_STATUS_FILTER_ORDER.slice(0, 2)).toEqual([
            "awaiting_review",
            "insufficient",
        ]);
    });

    it("reads provenance off gapSourceRef and never off anything the client assembles", () => {
        expect(describeRunOrigin("skill-gap:discovery@2026-08-18")).toBe("с дашборда");
        expect(describeRunOrigin(null)).toBe("вручную");
    });
});
