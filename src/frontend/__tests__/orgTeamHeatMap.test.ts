import { describe, expect, it } from "vitest";
import type {
    TeamSkillMap,
    TeamSkillMapMember,
} from "@/features/org-shell/hooks/use-team-directory";
import {
    buildHeatMapColumns,
    indexMemberCells,
    readMemberCell,
} from "@/features/org-team/utils/heat-map-matrix";
import {
    describeUnmeasuredCell,
    formatHeatMapCellValue,
    resolveHeatMapTone,
} from "@/features/org-team/utils/heat-map-scale";

/**
 * The four-step scale and the column alignment behind O1's heat map
 * (docs/TENANCY/ADMIN_UI_DESIGN.md, O1 «Цвет ячейки»).
 *
 * Both of these break silently rather than loudly: a boundary off by one repaints a whole column,
 * and a positional read of `member.skills[]` shifts an entire row sideways the first time somebody
 * skips a skill. Neither shows up as an exception, and both send the РОП to coach the wrong person.
 */
describe("heat map colour scale", () => {
    it("splits accuracy at 50, 65 and 80 exactly as the design does", () => {
        expect(resolveHeatMapTone(0)).toBe("critical");
        expect(resolveHeatMapTone(49)).toBe("critical");
        expect(resolveHeatMapTone(50)).toBe("weak");
        expect(resolveHeatMapTone(64)).toBe("weak");
        expect(resolveHeatMapTone(65)).toBe("plain");
        expect(resolveHeatMapTone(79)).toBe("plain");
        expect(resolveHeatMapTone(80)).toBe("strong");
        expect(resolveHeatMapTone(100)).toBe("strong");
    });

    it("treats a withheld percentage as its own step, never as zero", () => {
        expect(resolveHeatMapTone(null)).toBe("unmeasured");
        expect(resolveHeatMapTone(null)).not.toBe(resolveHeatMapTone(0));
        expect(formatHeatMapCellValue(null)).toBe("—");
        expect(formatHeatMapCellValue(0)).toBe("0%");
    });

    it("explains a dash with the threshold the server echoed back", () => {
        expect(describeUnmeasuredCell(5)).toBe("меньше 5 попыток");
        expect(describeUnmeasuredCell(12)).toBe("меньше 12 попыток");
    });
});

function buildCell(key: string, attemptCount: number, accuracyPercent: number | null) {
    return { key, attemptCount, accuracyPercent };
}

function buildMember(overrides: Partial<TeamSkillMapMember>): TeamSkillMapMember {
    return {
        userId: "11111111-1111-1111-1111-111111111111",
        displayName: "Иванов А.",
        isActiveMember: true,
        attemptCount: 40,
        accuracyPercent: 61,
        weakestStageKey: "closing",
        weakestSkillId: null,
        dialogCount: 3,
        dialogAverageScore: 62,
        stages: [],
        skills: [],
        ...overrides,
    };
}

const skillMap: TeamSkillMap = {
    windowStart: "2026-05-20T00:00:00Z",
    stages: [
        { key: "contact", label: "Контакт", accent: "", order: 1, attemptCount: 300, accuracyPercent: 78 },
        { key: "closing", label: "Закрытие", accent: "", order: 5, attemptCount: 214, accuracyPercent: 47 },
    ],
    skills: [
        {
            skillId: "aaaa",
            title: "Работа с ценой",
            stageKey: "closing",
            orderInTree: 2,
            attemptCount: 90,
            accuracyPercent: 38,
        },
        {
            skillId: "bbbb",
            title: "Первое касание",
            stageKey: "contact",
            orderInTree: 1,
            attemptCount: 120,
            accuracyPercent: 81,
        },
    ],
    members: [],
    unattributedAttemptCount: 340,
    minimumAttemptsForAccuracy: 5,
    rosterKnown: true,
};

describe("heat map columns", () => {
    it("carries the team-wide number in the same array the rows are drawn against", () => {
        const columns = buildHeatMapColumns(skillMap, "stages");

        expect(columns.map((column) => column.key)).toEqual(["contact", "closing"]);
        expect(columns[1]).toMatchObject({
            label: "Закрытие",
            stageLabel: null,
            accuracyPercent: 47,
            attemptCount: 214,
        });
    });

    it("names the stage a skill belongs to, so thirty columns stay readable", () => {
        const columns = buildHeatMapColumns(skillMap, "skills");

        expect(columns.map((column) => column.key)).toEqual(["aaaa", "bbbb"]);
        expect(columns[0]).toMatchObject({ label: "Работа с ценой", stageLabel: "Закрытие" });
        expect(columns[1]).toMatchObject({ label: "Первое касание", stageLabel: "Контакт" });
    });

    it("falls back to the raw stage key when the lookup has no label for it", () => {
        const orphanedSkillMap: TeamSkillMap = {
            ...skillMap,
            stages: [],
            skills: [{ ...skillMap.skills[0], stageKey: "unknown-stage" }],
        };

        expect(buildHeatMapColumns(orphanedSkillMap, "skills")[0].stageLabel).toBe("unknown-stage");
    });
});

describe("member cells", () => {
    it("reads a cell by column key rather than by position", () => {
        const member = buildMember({
            skills: [buildCell("bbbb", 20, 81)],
        });

        const cellsByKey = indexMemberCells(member, "skills");

        expect(readMemberCell(cellsByKey, "bbbb")).toEqual(buildCell("bbbb", 20, 81));
        expect(readMemberCell(cellsByKey, "aaaa")).toBeNull();
    });

    it("keeps a row aligned when the manager skipped the first column", () => {
        const member = buildMember({ stages: [buildCell("closing", 30, 41)] });
        const cellsByKey = indexMemberCells(member, "stages");
        const columns = buildHeatMapColumns(skillMap, "stages");

        const rowValues = columns.map(
            (column) => readMemberCell(cellsByKey, column.key)?.accuracyPercent ?? null
        );

        expect(rowValues).toEqual([null, 41]);
    });

    it("switches axes without touching the other axis's cells", () => {
        const member = buildMember({
            stages: [buildCell("contact", 10, 70)],
            skills: [buildCell("aaaa", 10, 38)],
        });

        expect([...indexMemberCells(member, "stages").keys()]).toEqual(["contact"]);
        expect([...indexMemberCells(member, "skills").keys()]).toEqual(["aaaa"]);
    });
});
