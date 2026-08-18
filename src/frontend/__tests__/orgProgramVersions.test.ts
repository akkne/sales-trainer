import { describe, expect, it } from "vitest";

import {
    describeLessonCount,
    describePersonCount,
    describeUnknownPerson,
    formatProgramDate,
    formatVersionLabel,
    pluralizeRussianCount,
} from "@/features/org-program/lib/format-program-text";
import {
    buildMemberNameLookup,
    isEnrollmentBehind,
    selectCurrentPublishedVersion,
    selectDraftVersion,
    selectEnrollableMembers,
    selectPreviousPublishedVersion,
    selectPublishedVersions,
    summarizeEnrollmentSpread,
} from "@/features/org-program/lib/program-versions";
import {
    describeProgramVersionStatus,
    resolveProgramVersionStatusTone,
} from "@/features/org-program/constants/program-dictionary";
import type { ProgramEnrollment, ProgramVersionSummary } from "@/features/org-program/types/program";
import type { ProgramRosterMember } from "@/features/org-program/types/program-roster";

function buildVersion(
    overrides: Partial<ProgramVersionSummary> & Pick<ProgramVersionSummary, "id" | "versionNumber">
): ProgramVersionSummary {
    return {
        status: "published",
        itemCount: 47,
        enrollmentCount: 0,
        createdBy: null,
        createdAt: "2026-08-01T10:00:00Z",
        publishedAt: "2026-08-12T10:00:00Z",
        ...overrides,
    };
}

function buildEnrollment(
    overrides: Partial<ProgramEnrollment> &
        Pick<ProgramEnrollment, "userId" | "programVersionId" | "programVersionNumber">
): ProgramEnrollment {
    return {
        previousProgramVersionId: null,
        enrolledAt: "2026-08-12T10:00:00Z",
        switchedAt: null,
        ...overrides,
    };
}

function buildMember(userId: string, displayName: string): ProgramRosterMember {
    return { userId, displayName };
}

const VERSION_THREE = buildVersion({ id: "version-3", versionNumber: 3, enrollmentCount: 7 });
const VERSION_TWO = buildVersion({
    id: "version-2",
    versionNumber: 2,
    enrollmentCount: 2,
    publishedAt: "2026-07-28T10:00:00Z",
});
const VERSION_ONE = buildVersion({
    id: "version-1",
    versionNumber: 1,
    publishedAt: "2026-07-01T10:00:00Z",
});
const DRAFT_VERSION = buildVersion({
    id: "version-draft",
    versionNumber: 4,
    status: "draft",
    publishedAt: null,
});

/**
 * O18 «Программа обучения» (docs/TENANCY/ADMIN_UI_DESIGN.md O18). What is checked here is the one
 * decision the screen exists to get right: who is behind, and on what.
 */
describe("selectCurrentPublishedVersion", () => {
    it("picks the newest published version regardless of the order the endpoint answered in", () => {
        const versions = [VERSION_ONE, VERSION_THREE, VERSION_TWO];
        expect(selectCurrentPublishedVersion(versions)?.id).toBe("version-3");
    });

    it("never picks the draft, even though its number is the highest", () => {
        const versions = [DRAFT_VERSION, VERSION_THREE];
        expect(selectCurrentPublishedVersion(versions)?.id).toBe("version-3");
    });

    it("is null when nothing has been published yet, draft or no draft", () => {
        expect(selectCurrentPublishedVersion([])).toBeNull();
        expect(selectCurrentPublishedVersion([DRAFT_VERSION])).toBeNull();
    });

    it("keeps the draft out of the published list", () => {
        expect(selectPublishedVersions([DRAFT_VERSION, VERSION_TWO, VERSION_THREE])).toHaveLength(2);
    });
});

describe("selectDraftVersion", () => {
    it("finds the single mutable draft", () => {
        expect(selectDraftVersion([VERSION_THREE, DRAFT_VERSION])?.id).toBe("version-draft");
    });

    it("is null when there is nothing to publish", () => {
        expect(selectDraftVersion([VERSION_THREE, VERSION_TWO])).toBeNull();
    });
});

describe("selectPreviousPublishedVersion", () => {
    it("is the published version immediately below, skipping the draft", () => {
        const versions = [DRAFT_VERSION, VERSION_THREE, VERSION_TWO, VERSION_ONE];
        expect(selectPreviousPublishedVersion(versions, "version-3")?.id).toBe("version-2");
    });

    it("is null for the very first published version — there is nothing to diff against", () => {
        const versions = [VERSION_THREE, VERSION_TWO, VERSION_ONE];
        expect(selectPreviousPublishedVersion(versions, "version-1")).toBeNull();
    });

    it("is null for a version that is not in the list at all", () => {
        expect(selectPreviousPublishedVersion([VERSION_THREE], "version-9")).toBeNull();
    });
});

describe("isEnrollmentBehind", () => {
    const onVersionThree = buildEnrollment({
        userId: "user-a",
        programVersionId: "version-3",
        programVersionNumber: 3,
    });
    const onVersionTwo = buildEnrollment({
        userId: "user-b",
        programVersionId: "version-2",
        programVersionNumber: 2,
    });

    it("says a person pinned to an older version is behind", () => {
        expect(isEnrollmentBehind(onVersionTwo, VERSION_THREE)).toBe(true);
    });

    it("says a person pinned to today's version is not behind", () => {
        expect(isEnrollmentBehind(onVersionThree, VERSION_THREE)).toBe(false);
    });

    it("says nobody is behind when the organization has published nothing", () => {
        expect(isEnrollmentBehind(onVersionTwo, null)).toBe(false);
    });

    it("compares by version id, not by the number label", () => {
        const relabelled = buildEnrollment({
            userId: "user-c",
            programVersionId: "version-2",
            programVersionNumber: 3,
        });
        expect(isEnrollmentBehind(relabelled, VERSION_THREE)).toBe(true);
    });
});

describe("summarizeEnrollmentSpread", () => {
    const enrollments = [
        buildEnrollment({ userId: "user-a", programVersionId: "version-3", programVersionNumber: 3 }),
        buildEnrollment({ userId: "user-b", programVersionId: "version-3", programVersionNumber: 3 }),
        buildEnrollment({ userId: "user-c", programVersionId: "version-2", programVersionNumber: 2 }),
    ];

    it("splits the team across the versions it is actually pinned to, newest first", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments,
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [],
        });

        expect(spread.groups.map((group) => group.programVersionNumber)).toEqual([3, 2]);
        expect(spread.groups[0].enrollmentCount).toBe(2);
        expect(spread.groups[0].isCurrentPublishedVersion).toBe(true);
        expect(spread.groups[1].isCurrentPublishedVersion).toBe(false);
    });

    it("reports the mixed state as the ordinary case it is, not as an error", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments,
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [],
        });

        expect(spread.isSpreadAcrossVersions).toBe(true);
        expect(spread.onCurrentVersionCount).toBe(2);
        expect(spread.behindCount).toBe(1);
        expect(spread.enrolledCount).toBe(3);
    });

    it("is not 'spread' when everybody happens to be on the same version", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments: enrollments.slice(0, 2),
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [],
        });

        expect(spread.isSpreadAcrossVersions).toBe(false);
        expect(spread.behindCount).toBe(0);
    });

    it("counts nobody as on the current version while no version is published", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments,
            currentPublishedVersion: null,
            rosterMembers: [],
        });

        expect(spread.onCurrentVersionCount).toBe(0);
        expect(spread.behindCount).toBe(0);
    });

    it("counts the people who hold no pin at all — they are on the live tree, not on the newest version", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments,
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [
                buildMember("user-a", "Иванов А."),
                buildMember("user-b", "Петров И."),
                buildMember("user-c", "Сидорова М."),
                buildMember("user-d", "Кузнецов П."),
                buildMember("user-e", "Орлова Н."),
            ],
        });

        expect(spread.notEnrolledCount).toBe(2);
    });

    it("does not count an enrolled person the roster has never heard of as unenrolled", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments,
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [buildMember("user-z", "Новичок Б.")],
        });

        expect(spread.notEnrolledCount).toBe(1);
        expect(spread.enrolledCount).toBe(3);
    });

    it("is all zeros with no enrollments at all", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments: [],
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [],
        });

        expect(spread.enrolledCount).toBe(0);
        expect(spread.groups).toHaveLength(0);
        expect(spread.isSpreadAcrossVersions).toBe(false);
    });
});

describe("selectEnrollableMembers", () => {
    it("offers only people who hold no pin, so the dialog cannot be used to move anybody", () => {
        const enrollable = selectEnrollableMembers(
            [buildMember("user-a", "Иванов А."), buildMember("user-b", "Петров И.")],
            [
                buildEnrollment({
                    userId: "user-a",
                    programVersionId: "version-3",
                    programVersionNumber: 3,
                }),
            ]
        );

        expect(enrollable.map((member) => member.userId)).toEqual(["user-b"]);
    });
});

describe("buildMemberNameLookup", () => {
    it("maps user ids to names for the enrollment rows", () => {
        const lookup = buildMemberNameLookup([buildMember("user-a", "Иванов А.")]);
        expect(lookup.get("user-a")).toBe("Иванов А.");
        expect(lookup.get("user-b")).toBeUndefined();
    });
});

describe("describeUnknownPerson", () => {
    it("keeps two unnamed learners distinguishable instead of collapsing them into «Неизвестный»", () => {
        expect(describeUnknownPerson("6f1c2a90-1111-4444-8888-000000000001")).toBe(
            "Без имени · 6f1c2a90"
        );
        expect(describeUnknownPerson("6f1c2a90-1111-4444-8888-000000000001")).not.toBe(
            describeUnknownPerson("aa1c2a90-1111-4444-8888-000000000002")
        );
    });
});

describe("formatProgramDate", () => {
    const now = new Date("2026-08-18T12:00:00Z");

    it("renders «12 авг» inside the current year", () => {
        expect(formatProgramDate("2026-08-12T09:00:00Z", now)).toBe("12 авг");
    });

    it("adds the year once the date leaves it", () => {
        expect(formatProgramDate("2025-07-28T09:00:00Z", now)).toBe("28 июл 2025");
    });

    it("renders an empty string for a missing or unparseable timestamp, never «Invalid Date»", () => {
        expect(formatProgramDate(null, now)).toBe("");
        expect(formatProgramDate("not-a-date", now)).toBe("");
    });
});

describe("pluralizeRussianCount", () => {
    const lessonForms = ["урок", "урока", "уроков"] as const;

    it("agrees with the design mock's own numbers", () => {
        expect(describeLessonCount(47)).toBe("47 уроков");
        expect(describeLessonCount(45)).toBe("45 уроков");
        expect(describeLessonCount(1)).toBe("1 урок");
        expect(describeLessonCount(2)).toBe("2 урока");
    });

    it("handles the 11–14 exception that catches naive implementations", () => {
        expect(pluralizeRussianCount(11, lessonForms)).toBe("уроков");
        expect(pluralizeRussianCount(12, lessonForms)).toBe("уроков");
        expect(pluralizeRussianCount(14, lessonForms)).toBe("уроков");
        expect(pluralizeRussianCount(21, lessonForms)).toBe("урок");
        expect(pluralizeRussianCount(112, lessonForms)).toBe("уроков");
    });

    it("pluralizes people, where the one-form and the many-form are spelled the same", () => {
        expect(describePersonCount(1)).toBe("1 человек");
        expect(describePersonCount(2)).toBe("2 человека");
        expect(describePersonCount(9)).toBe("9 человек");
        expect(describePersonCount(0)).toBe("0 человек");
    });
});

describe("formatVersionLabel", () => {
    it("is the same label everywhere the number appears", () => {
        expect(formatVersionLabel(3)).toBe("v3");
    });
});

describe("describeProgramVersionStatus", () => {
    it("translates the three known statuses and leaves an unknown one raw", () => {
        expect(describeProgramVersionStatus("draft")).toBe("Черновик");
        expect(describeProgramVersionStatus("published")).toBe("Опубликована");
        expect(describeProgramVersionStatus("archived")).toBe("В архиве");
        expect(describeProgramVersionStatus("superseded")).toBe("superseded");
        expect(resolveProgramVersionStatusTone("superseded")).toBe("neutral");
    });
});
