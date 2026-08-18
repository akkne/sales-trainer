import { describe, expect, it } from "vitest";
import type {
    TeamSkillMap,
    TeamSkillMapMember,
} from "@/features/org-shell/hooks/use-team-directory";
import type { OrganizationMembership } from "@/features/org-team/types/organization-membership";
import { UNNAMED_MEMBER_LABEL, mergeTeamRoster } from "@/features/org-team/utils/team-roster";
import {
    ATTEMPT_PLURAL_FORMS,
    PERSON_PLURAL_FORMS,
    formatCountWithNoun,
    formatWindowStartDate,
    pluralizeRussian,
    summarizeTeamWindow,
} from "@/features/org-team/utils/team-summary";
import { describeGapSuppressionReason } from "@/features/org-team/constants/gap-suppression";

function buildMember(overrides: Partial<TeamSkillMapMember>): TeamSkillMapMember {
    return {
        userId: "user-with-practice",
        displayName: "Иванов А.",
        isActiveMember: true,
        attemptCount: 40,
        accuracyPercent: 61,
        weakestStageKey: "closing",
        weakestSkillId: null,
        dialogCount: 2,
        dialogAverageScore: 62,
        stages: [],
        skills: [],
        ...overrides,
    };
}

function buildSkillMap(overrides: Partial<TeamSkillMap>): TeamSkillMap {
    return {
        windowStart: "2026-05-20T00:00:00Z",
        stages: [
            {
                key: "closing",
                label: "Закрытие",
                accent: "",
                order: 5,
                attemptCount: 214,
                accuracyPercent: 47,
            },
        ],
        skills: [],
        members: [],
        unattributedAttemptCount: 0,
        minimumAttemptsForAccuracy: 5,
        rosterKnown: true,
        ...overrides,
    };
}

function buildMembership(overrides: Partial<OrganizationMembership>): OrganizationMembership {
    return {
        userId: "user-with-practice",
        email: "ivanov@example.com",
        displayName: "Иванов А.",
        role: "Manager",
        status: "Active",
        joinedAt: "2026-01-10T00:00:00Z",
        deactivatedAt: null,
        ...overrides,
    };
}

/**
 * The roster merge behind O1's rows.
 *
 * `GET /admin/team/skill-map` cannot produce the design's «уже не работает» mark on its own: when
 * it reaches identity-service its member list *is* the active roster, so every `isActiveMember` it
 * returns is `true`, and when it cannot reach it every one is `null`. `GET /memberships?status=all`
 * — a route that landed after ADMIN_UI_DESIGN.md §6.1 was written — is what restores both the
 * departed people who still have history and the hired people who have practised nothing.
 */
describe("team roster merge", () => {
    it("marks somebody who has history but no longer works here", () => {
        const merged = mergeTeamRoster(
            buildSkillMap({
                rosterKnown: false,
                members: [buildMember({ userId: "departed", displayName: "Кузьма О." })],
            }),
            [buildMembership({ userId: "departed", displayName: "Кузьма О.", status: "Deactivated" })]
        );

        expect(merged.rows).toHaveLength(1);
        expect(merged.rows[0].isActiveMember).toBe(false);
        expect(merged.isRosterKnown).toBe(true);
    });

    it("adds an active member who has practised nothing, and counts them as silent", () => {
        const merged = mergeTeamRoster(
            buildSkillMap({ members: [buildMember({})] }),
            [
                buildMembership({}),
                buildMembership({
                    userId: "never-practised",
                    displayName: "Сидоров К.",
                    email: "sidorov@example.com",
                }),
            ]
        );

        expect(merged.rows.map((row) => row.displayName)).toEqual(["Иванов А.", "Сидоров К."]);
        expect(merged.rows[1]).toMatchObject({
            attemptCount: 0,
            accuracyPercent: null,
            weakestStageKey: null,
            hasPractice: false,
            isActiveMember: true,
        });
        expect(merged.silentMemberCount).toBe(1);
    });

    it("does not resurrect a departed person who never practised", () => {
        const merged = mergeTeamRoster(buildSkillMap({ members: [] }), [
            buildMembership({ userId: "long-gone", status: "Deactivated" }),
        ]);

        expect(merged.rows).toEqual([]);
    });

    it("degrades to the design's palliative when the roster cannot be read", () => {
        const merged = mergeTeamRoster(
            buildSkillMap({
                rosterKnown: false,
                members: [buildMember({ isActiveMember: null })],
            }),
            null
        );

        expect(merged.rows[0].isActiveMember).toBeNull();
        expect(merged.isRosterKnown).toBe(false);
    });

    it("keeps the map's own answer trustworthy when only learning-service knew the roster", () => {
        const merged = mergeTeamRoster(
            buildSkillMap({ rosterKnown: true, members: [buildMember({})] }),
            null
        );

        expect(merged.isRosterKnown).toBe(true);
        expect(merged.rows[0].isActiveMember).toBe(true);
    });

    it("reports an unknown activity for somebody the roster does not mention at all", () => {
        const merged = mergeTeamRoster(
            buildSkillMap({ members: [buildMember({ userId: "stranger" })] }),
            [buildMembership({ userId: "somebody-else" })]
        );

        const stranger = merged.rows.find((row) => row.userId === "stranger");
        expect(stranger?.isActiveMember).toBeNull();
    });

    it("orders busy people first, silent people next and departed people last", () => {
        const merged = mergeTeamRoster(
            buildSkillMap({
                members: [
                    buildMember({ userId: "quiet", displayName: "Петров И.", attemptCount: 4, dialogCount: 0 }),
                    buildMember({ userId: "busy", displayName: "Иванов А.", attemptCount: 90, dialogCount: 3 }),
                    buildMember({ userId: "gone", displayName: "Кузьма О.", attemptCount: 50, dialogCount: 0 }),
                ],
            }),
            [
                buildMembership({ userId: "quiet" }),
                buildMembership({ userId: "busy" }),
                buildMembership({ userId: "gone", status: "Deactivated" }),
                buildMembership({ userId: "fresh", displayName: "Сидоров К." }),
            ]
        );

        expect(merged.rows.map((row) => row.userId)).toEqual(["busy", "quiet", "fresh", "gone"]);
    });

    it("falls back to the roster's name, then to a placeholder, rather than rendering nothing", () => {
        const merged = mergeTeamRoster(
            buildSkillMap({
                members: [
                    buildMember({ userId: "nameless-in-map", displayName: "" }),
                    buildMember({ userId: "nameless-everywhere", displayName: "", attemptCount: 1 }),
                ],
            }),
            [buildMembership({ userId: "nameless-in-map", displayName: "Иванов А." })]
        );

        const byUserId = new Map(merged.rows.map((row) => [row.userId, row.displayName]));
        expect(byUserId.get("nameless-in-map")).toBe("Иванов А.");
        expect(byUserId.get("nameless-everywhere")).toBe(UNNAMED_MEMBER_LABEL);
    });
});

describe("team window summary", () => {
    it("counts the attempts no skill could be named for", () => {
        const summary = summarizeTeamWindow(
            buildSkillMap({
                stages: [
                    { key: "a", label: "A", accent: "", order: 1, attemptCount: 1000, accuracyPercent: 70 },
                    { key: "b", label: "B", accent: "", order: 2, attemptCount: 508, accuracyPercent: 55 },
                ],
                unattributedAttemptCount: 340,
            }),
            12
        );

        expect(summary).toEqual({ memberCount: 12, attemptCount: 1848 });
    });

    it("formats the window start without the «г.» nobody reads out loud", () => {
        expect(formatWindowStartDate("2026-05-20T00:00:00Z")).toBe("20 мая 2026");
        expect(formatWindowStartDate("not-a-date")).toBe("");
    });

    it("declines Russian nouns by count instead of writing «12 человека»", () => {
        expect(pluralizeRussian(1, ATTEMPT_PLURAL_FORMS)).toBe("попытка");
        expect(pluralizeRussian(2, ATTEMPT_PLURAL_FORMS)).toBe("попытки");
        expect(pluralizeRussian(5, ATTEMPT_PLURAL_FORMS)).toBe("попыток");
        expect(pluralizeRussian(11, ATTEMPT_PLURAL_FORMS)).toBe("попыток");
        expect(pluralizeRussian(12, ATTEMPT_PLURAL_FORMS)).toBe("попыток");
        expect(pluralizeRussian(21, ATTEMPT_PLURAL_FORMS)).toBe("попытка");
        expect(pluralizeRussian(114, ATTEMPT_PLURAL_FORMS)).toBe("попыток");
        expect(pluralizeRussian(0, ATTEMPT_PLURAL_FORMS)).toBe("попыток");
        expect(pluralizeRussian(1, PERSON_PLURAL_FORMS)).toBe("человек");
        expect(pluralizeRussian(2, PERSON_PLURAL_FORMS)).toBe("человека");
        expect(pluralizeRussian(12, PERSON_PLURAL_FORMS)).toBe("человек");
    });

    it("groups thousands the Russian way", () => {
        expect(formatCountWithNoun(1848, ATTEMPT_PLURAL_FORMS).endsWith("попыток")).toBe(true);
        expect(formatCountWithNoun(1848, ATTEMPT_PLURAL_FORMS)).toMatch(/^1\s848\s/u);
    });
});

describe("gap suppression vocabulary", () => {
    it("uses the panel's fixed dictionary", () => {
        expect(describeGapSuppressionReason("dismissed")).toBe("Отложено вами");
        expect(describeGapSuppressionReason("run_in_progress")).toBe("Уже идёт генерация");
        expect(describeGapSuppressionReason("recently_addressed")).toBe("Недавно закрывали");
    });

    it("degrades a reason it has never seen into something a person can read", () => {
        expect(describeGapSuppressionReason("quota_exhausted")).toBe("Не предлагаем");
    });
});
