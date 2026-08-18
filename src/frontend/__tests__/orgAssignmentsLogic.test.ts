import { describe, expect, it } from "vitest";
import { ApiError } from "@/shared/api/api-client";
import {
    assignmentStatusTone,
    describeAssignmentSourceType,
    describeAssignmentStatus,
    describeContentKind,
    describeProgressStatus,
} from "@/features/org-assignments/constants/assignment-dictionary";
import type { AssignmentFunnel, AssignmentSummary } from "@/features/org-assignments/types/assignment";
import { describeAssignmentWriteFailure } from "@/features/org-assignments/utils/api-failure";
import {
    buildAudienceRule,
    describeAudience,
    describeAudienceKind,
    pluralizeContentItems,
    pluralizePeople,
    validateAudienceRule,
} from "@/features/org-assignments/utils/audience-rule";
import {
    buildCompletionRuleDocument,
    describeBestScore,
    describeHalfMeasuredContentWarning,
    requiredContentKindForRule,
    toCompletionRuleDraft,
    validateCompletionRuleDraft,
    EMPTY_COMPLETION_RULE_DRAFT,
} from "@/features/org-assignments/utils/completion-rule-draft";
import {
    collectContentKinds,
    containsContentItem,
    moveContentItem,
    toContentDraftItems,
    toContentItems,
    type AssignmentContentDraftItem,
} from "@/features/org-assignments/utils/content-draft";
import {
    buildDashboardFunnelStages,
    buildListFunnelSegments,
    countReminderRecipients,
    describeDeadline,
    describeWaveComparison,
} from "@/features/org-assignments/utils/funnel";
import {
    buildRepeatScheduleDocument,
    describeRepeatSchedule,
    readRepeatOffsetDays,
    validateRepeatOffsetDays,
    DEFAULT_REPEAT_OFFSET_DAYS,
} from "@/features/org-assignments/utils/repeat-schedule";
import { readDeadlineInput, writeDateInput } from "@/features/org-assignments/utils/schedule-input";

function buildSummary(overrides: Partial<AssignmentSummary> = {}): AssignmentSummary {
    return {
        id: "assignment-1",
        title: "Отработка возражения «дорого»",
        sourceType: "training",
        status: "active",
        audienceKind: "whole_team",
        opensAt: null,
        deadline: null,
        hasRepeatSchedule: false,
        repeatOfAssignmentId: null,
        repeatWaveIndex: null,
        contentItemCount: 3,
        assignedCount: 12,
        startedCount: 9,
        completedCount: 6,
        failedThresholdCount: 3,
        createdBy: null,
        createdAt: "2026-08-01T00:00:00Z",
        updatedAt: "2026-08-01T00:00:00Z",
        ...overrides,
    };
}

function buildFunnel(overrides: Partial<AssignmentFunnel> = {}): AssignmentFunnel {
    return {
        assignedCount: 12,
        notStartedCount: 3,
        startedCount: 9,
        completedCount: 6,
        failedThresholdCount: 3,
        leftOrganizationCount: 1,
        assignedActiveCount: 11,
        ...overrides,
    };
}

describe("assignment dictionary", () => {
    it("translates the fixed vocabulary of ADMIN_UI_DESIGN §1.4", () => {
        expect(describeAssignmentStatus("draft")).toBe("Черновик");
        expect(describeAssignmentStatus("active")).toBe("Выдано");
        expect(describeAssignmentStatus("closed")).toBe("Закрыто");
        expect(describeAssignmentSourceType("training")).toBe("По тренингу");
        expect(describeAssignmentSourceType("manual")).toBe("Вручную");
        expect(describeAssignmentSourceType("gap_detected")).toBe("По провалу на дашборде");
        expect(describeProgressStatus("failed_threshold")).toBe("Ниже порога");
        expect(describeProgressStatus("not_started")).toBe("Не начал");
        expect(describeContentKind("dialog_scenario")).toBe("Разговор");
    });

    it("shows an unknown server value verbatim instead of blanking it", () => {
        expect(describeAssignmentStatus("archived_someday")).toBe("archived_someday");
        expect(describeProgressStatus("abandoned")).toBe("abandoned");
        expect(assignmentStatusTone("archived_someday")).toBe("neutral");
    });
});

describe("completion rule draft", () => {
    it("refuses a draft with no rule at all", () => {
        expect(validateCompletionRuleDraft(EMPTY_COMPLETION_RULE_DRAFT, ["dialog_scenario"])).toMatch(
            /Без порога/
        );
    });

    it("refuses numbers outside the server's own bounds", () => {
        expect(
            validateCompletionRuleDraft(
                { ...EMPTY_COMPLETION_RULE_DRAFT, kind: "dialog_score", requiredCount: 21 },
                ["dialog_scenario"]
            )
        ).toMatch(/от 1 до 20/);

        expect(
            validateCompletionRuleDraft(
                { ...EMPTY_COMPLETION_RULE_DRAFT, kind: "dialog_score", minimumScore: 0 },
                ["dialog_scenario"]
            )
        ).toMatch(/от 1 до 100/);

        expect(
            validateCompletionRuleDraft(
                {
                    ...EMPTY_COMPLETION_RULE_DRAFT,
                    kind: "exercise_accuracy",
                    minimumAccuracyPercent: 101,
                },
                ["lesson_version"]
            )
        ).toMatch(/от 1 до 100/);
    });

    it("refuses a rule the assignment has no content for, the way activate would with a 409", () => {
        expect(
            validateCompletionRuleDraft({ ...EMPTY_COMPLETION_RULE_DRAFT, kind: "dialog_score" }, [
                "lesson_version",
            ])
        ).toMatch(/разговорам/);

        expect(
            validateCompletionRuleDraft(
                { ...EMPTY_COMPLETION_RULE_DRAFT, kind: "exercise_accuracy" },
                ["dialog_scenario"]
            )
        ).toMatch(/упражнениям/);
    });

    it("accepts a rule measured over content that is present", () => {
        expect(
            validateCompletionRuleDraft({ ...EMPTY_COMPLETION_RULE_DRAFT, kind: "dialog_score" }, [
                "dialog_scenario",
                "reference_material",
            ])
        ).toBeNull();
    });

    it("names the content kind each rule is measured over", () => {
        expect(requiredContentKindForRule("dialog_score")).toBe("dialog_scenario");
        expect(requiredContentKindForRule("exercise_accuracy")).toBe("lesson_version");
        expect(requiredContentKindForRule("none")).toBeNull();
    });

    it("builds the document the API stores, and nothing at all while there is no rule", () => {
        expect(
            buildCompletionRuleDocument({
                kind: "dialog_score",
                minimumScore: 70,
                requiredCount: 3,
                minimumAccuracyPercent: 80,
            })
        ).toEqual({ kind: "dialog_score", minimumScore: 70, requiredCount: 3 });

        expect(
            buildCompletionRuleDocument({
                kind: "exercise_accuracy",
                minimumScore: 70,
                requiredCount: 3,
                minimumAccuracyPercent: 80,
            })
        ).toEqual({ kind: "exercise_accuracy", minimumAccuracyPercent: 80 });

        expect(buildCompletionRuleDocument(EMPTY_COMPLETION_RULE_DRAFT)).toBeNull();
    });

    it("reads a stored rule back, and lands on «no rule» for a kind it has never heard of", () => {
        expect(toCompletionRuleDraft({ kind: "dialog_score", minimumScore: 55, requiredCount: 2 })).toMatchObject(
            { kind: "dialog_score", minimumScore: 55, requiredCount: 2 }
        );
        expect(toCompletionRuleDraft({ kind: "voice_tempo_2027" }).kind).toBe("none");
        expect(toCompletionRuleDraft(null).kind).toBe("none");
    });

    it("warns about the unmeasured half only when both halves are present", () => {
        expect(
            describeHalfMeasuredContentWarning(["lesson_version", "dialog_scenario"], "dialog_score")
        ).toMatch(/только разговоры/);
        expect(
            describeHalfMeasuredContentWarning(
                ["lesson_version", "dialog_scenario"],
                "exercise_accuracy"
            )
        ).toMatch(/только упражнения/);
        expect(describeHalfMeasuredContentWarning(["dialog_scenario"], "dialog_score")).toBeNull();
        expect(
            describeHalfMeasuredContentWarning(["lesson_version", "dialog_scenario"], "none")
        ).toBeNull();
    });

    it("labels a score by the rule it was measured with, and a missing one as a dash", () => {
        expect(describeBestScore(61, "dialog_score")).toBe("лучшая 61");
        expect(describeBestScore(61, "exercise_accuracy")).toBe("точность 61%");
        expect(describeBestScore(null, "exercise_accuracy")).toBe("—");
    });
});

describe("audience rule", () => {
    it("sends the rule the РОП chose, not a resolved list", () => {
        expect(buildAudienceRule("whole_team", ["a", "b"])).toEqual({ kind: "whole_team" });
        expect(buildAudienceRule("users", ["a", "b", "a"])).toEqual({
            kind: "users",
            userIds: ["a", "b"],
        });
    });

    it("refuses an empty hand-picked list", () => {
        expect(validateAudienceRule({ kind: "users", userIds: [] })).toMatch(/хотя бы одного/);
        expect(validateAudienceRule({ kind: "whole_team" })).toBeNull();
        expect(validateAudienceRule({ kind: "users", userIds: ["a"] })).toBeNull();
    });

    it("degrades honestly when the summary carries no headcount", () => {
        expect(describeAudienceKind("whole_team")).toBe("вся команда");
        expect(describeAudienceKind("users")).toBe("выбранные люди");
        expect(describeAudienceKind("users", 0)).toBe("выбранные люди");
        expect(describeAudienceKind("users", 6)).toBe("6 человек");
        expect(describeAudience({ kind: "users", userIds: ["a", "b"] })).toBe("2 человека");
    });

    it("pluralizes Russian counts", () => {
        expect(pluralizePeople(1)).toBe("человек");
        expect(pluralizePeople(2)).toBe("человека");
        expect(pluralizePeople(5)).toBe("человек");
        expect(pluralizePeople(11)).toBe("человек");
        expect(pluralizeContentItems(1)).toBe("материал");
        expect(pluralizeContentItems(3)).toBe("материала");
        expect(pluralizeContentItems(14)).toBe("материалов");
    });
});

describe("funnel maths", () => {
    it("derives «в работе» and «не начали», which the list DTO does not carry", () => {
        const segments = buildListFunnelSegments(buildSummary());

        expect(segments.map((segment) => [segment.key, segment.count])).toEqual([
            ["completed", 6],
            ["failed_threshold", 3],
            ["in_progress", 0],
            ["not_started", 3],
        ]);
    });

    it("never draws a negative segment from an inconsistent read", () => {
        const segments = buildListFunnelSegments(
            buildSummary({ assignedCount: 2, startedCount: 5, completedCount: 5, failedThresholdCount: 2 })
        );

        expect(segments.every((segment) => segment.count >= 0)).toBe(true);
    });

    it("keeps «ниже порога» as its own fifth stage, never folded into «выполнили»", () => {
        const stages = buildDashboardFunnelStages(buildFunnel());

        expect(stages.map((stage) => stage.key)).toEqual([
            "assigned",
            "not_started",
            "started",
            "completed",
            "failed_threshold",
        ]);
        expect(stages[4].count).toBe(3);
        expect(stages[3].filledPercent).toBe(50);
    });

    it("draws an empty funnel rather than dividing by zero", () => {
        const stages = buildDashboardFunnelStages(
            buildFunnel({
                assignedCount: 0,
                notStartedCount: 0,
                startedCount: 0,
                completedCount: 0,
                failedThresholdCount: 0,
            })
        );

        expect(stages.every((stage) => stage.filledPercent === 0)).toBe(true);
    });

    it("counts reminder recipients out of the funnel rather than inventing them", () => {
        expect(countReminderRecipients(buildFunnel(), "not_started")).toBe(3);
        expect(countReminderRecipients(buildFunnel(), "unfinished")).toBe(6);
    });

    it("describes the deadline as the list cell needs it", () => {
        const now = new Date("2026-08-19T12:00:00Z");

        expect(describeDeadline(null, now)).toEqual({ text: "—", isOverdue: false });
        expect(describeDeadline("2026-08-21T12:00:00Z", now).text).toBe("через 2 дн");
        expect(describeDeadline("2026-08-19T20:00:00Z", now)).toEqual({
            text: "сегодня",
            isOverdue: false,
        });

        const passed = describeDeadline("2026-08-11T12:00:00Z", now);
        expect(passed.isOverdue).toBe(true);
        expect(passed.text).toMatch(/^прошёл /);
    });

    it("compares waves only once there is a second one", () => {
        expect(describeWaveComparison([{ waveIndex: 0, funnel: buildFunnel() }])).toBeNull();
        expect(
            describeWaveComparison([
                { waveIndex: 0, funnel: buildFunnel({ completedCount: 6 }) },
                { waveIndex: 1, funnel: buildFunnel({ completedCount: 9 }) },
            ])
        ).toBe("Волна 1: 6 из 12 · Волна 2: 9 из 12");
    });
});

describe("repeat schedule", () => {
    it("mirrors the server's bounds", () => {
        expect(validateRepeatOffsetDays([7, 21])).toBeNull();
        expect(validateRepeatOffsetDays([])).toMatch(/хотя бы один/);
        expect(validateRepeatOffsetDays([1, 2, 3, 4, 5])).toMatch(/не больше 4/);
        expect(validateRepeatOffsetDays([0])).toMatch(/от 1 до 180/);
        expect(validateRepeatOffsetDays([181])).toMatch(/от 1 до 180/);
        expect(validateRepeatOffsetDays([21, 7])).toMatch(/по возрастанию/);
        expect(validateRepeatOffsetDays([7, 7])).toMatch(/по возрастанию/);
    });

    it("builds and reads back the document, and never rewrites a kind it cannot read", () => {
        expect(buildRepeatScheduleDocument(false, [7])).toBeNull();
        expect(buildRepeatScheduleDocument(true, [7, 21])).toEqual({
            kind: "fixed_offsets",
            offsetDays: [7, 21],
        });
        expect(readRepeatOffsetDays({ kind: "fixed_offsets", offsetDays: [3] })).toEqual([3]);
        expect(readRepeatOffsetDays({ kind: "lunar_2028" })).toEqual(DEFAULT_REPEAT_OFFSET_DAYS);
        expect(readRepeatOffsetDays(null)).toEqual(DEFAULT_REPEAT_OFFSET_DAYS);
    });

    it("describes a schedule it understands and stays silent about one it does not", () => {
        expect(describeRepeatSchedule({ kind: "fixed_offsets", offsetDays: [7, 21] })).toBe(
            "повтор +7, +21"
        );
        expect(describeRepeatSchedule({ kind: "lunar_2028" })).toBeNull();
        expect(describeRepeatSchedule(null)).toBeNull();
    });
});

describe("content draft", () => {
    const lessonItem: AssignmentContentDraftItem = {
        kind: "lesson_version",
        reference: "version-1",
        title: "«Работа с ценой» · версия 4",
        persona: null,
    };
    const dialogItem: AssignmentContentDraftItem = {
        kind: "dialog_scenario",
        reference: "hard-buyer",
        title: "режим «Жёсткий закупщик»",
        persona: { name: "Марина", position: null, personality: null, difficulty: "Hard" },
    };

    it("numbers items densely by position and drops a persona from every kind but the dialogue", () => {
        const items = toContentItems([
            dialogItem,
            { ...lessonItem, persona: { name: "x", position: null, personality: null, difficulty: null } },
        ]);

        expect(items[0]).toEqual({
            kind: "dialog_scenario",
            reference: "hard-buyer",
            orderIndex: 0,
            persona: dialogItem.persona,
        });
        expect(items[1].orderIndex).toBe(1);
        expect(items[1].persona).toBeNull();
    });

    it("reads stored content back in server order", () => {
        const draftItems = toContentDraftItems([
            { kind: "dialog_scenario", reference: "b", orderIndex: 1, persona: null },
            { kind: "lesson_version", reference: "a", orderIndex: 0, persona: null },
        ]);

        expect(draftItems.map((item) => item.reference)).toEqual(["a", "b"]);
    });

    it("recognises a duplicate `(kind, reference)`, which the server answers 400 for", () => {
        expect(containsContentItem([lessonItem], "lesson_version", "version-1")).toBe(true);
        expect(containsContentItem([lessonItem], "dialog_scenario", "version-1")).toBe(false);
    });

    it("reorders by position and refuses an out-of-range move", () => {
        expect(moveContentItem([lessonItem, dialogItem], 0, 1).map((item) => item.reference)).toEqual([
            "hard-buyer",
            "version-1",
        ]);
        expect(moveContentItem([lessonItem, dialogItem], 0, 5)).toHaveLength(2);
        expect(moveContentItem([lessonItem, dialogItem], -1, 0)[0].reference).toBe("version-1");
    });

    it("collects the distinct kinds the threshold editor gates on", () => {
        expect(collectContentKinds([lessonItem, dialogItem, lessonItem])).toEqual([
            "lesson_version",
            "dialog_scenario",
        ]);
    });
});

describe("write failures", () => {
    it("words 503 as «nothing was written, press it again», per action", () => {
        const unavailable = new ApiError(503, {});

        expect(describeAssignmentWriteFailure(unavailable, "issue")).toMatch(/сохранено черновиком/);
        expect(describeAssignmentWriteFailure(unavailable, "remind")).toMatch(
            /Никто не получил напоминание/
        );
    });

    it("repeats the server's own sentence for 409 and 400", () => {
        expect(
            describeAssignmentWriteFailure(new ApiError(409, { message: "порог не про то содержание" }), "issue")
        ).toBe("порог не про то содержание");
        expect(
            describeAssignmentWriteFailure(new ApiError(400, { message: "аудитория пуста" }), "issue")
        ).toBe("аудитория пуста");
    });

    it("recognises a deleted draft", () => {
        expect(describeAssignmentWriteFailure(new ApiError(404, {}), "save")).toMatch(/не найдено/);
    });
});

describe("schedule inputs", () => {
    it("reads a bare date as the end of that day, so a deadline does not move a day earlier", () => {
        const isoDeadline = readDeadlineInput("2026-08-24");

        expect(isoDeadline).not.toBeNull();
        expect(writeDateInput(isoDeadline)).toBe("2026-08-24");
        expect(new Date(isoDeadline as string).getHours()).toBe(23);
    });

    it("treats an empty input as no date", () => {
        expect(readDeadlineInput("")).toBeNull();
        expect(writeDateInput(null)).toBe("");
    });
});
