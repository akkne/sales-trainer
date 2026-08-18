import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";

import { EnrollmentSpreadSummary } from "@/features/org-program/components/enrollment-spread-summary";
import { EnrollmentTable } from "@/features/org-program/components/enrollment-table";
import { ProgramDiffView } from "@/features/org-program/components/program-diff-view";
import { summarizeEnrollmentSpread } from "@/features/org-program/lib/program-versions";
import type {
    ProgramDiff,
    ProgramEnrollment,
    ProgramVersionSummary,
} from "@/features/org-program/types/program";

const VERSION_THREE: ProgramVersionSummary = {
    id: "version-3",
    versionNumber: 3,
    status: "published",
    itemCount: 47,
    enrollmentCount: 2,
    createdBy: null,
    createdAt: "2026-08-10T10:00:00Z",
    publishedAt: "2026-08-12T10:00:00Z",
};

function buildEnrollment(
    userId: string,
    programVersionId: string,
    programVersionNumber: number,
    overrides: Partial<ProgramEnrollment> = {}
): ProgramEnrollment {
    return {
        userId,
        programVersionId,
        programVersionNumber,
        previousProgramVersionId: null,
        enrolledAt: "2026-08-12T10:00:00Z",
        switchedAt: null,
        ...overrides,
    };
}

function buildDiff(overrides: Partial<ProgramDiff> = {}): ProgramDiff {
    return {
        fromProgramVersionId: "version-2",
        fromVersionNumber: 2,
        toProgramVersionId: "version-3",
        toVersionNumber: 3,
        addedLessons: [],
        removedLessons: [],
        changedLessons: [],
        movedLessons: [],
        hasBreakingChanges: false,
        ...overrides,
    };
}

/**
 * O18 «Программа обучения». The requirement these render tests pin: a reader must not be able to
 * leave this screen believing everybody is on the newest version
 * (docs/TENANCY/ADMIN_UI_DESIGN.md O18).
 */
describe("EnrollmentSpreadSummary", () => {
    const mixedEnrollments = [
        buildEnrollment("user-a", "version-3", 3),
        buildEnrollment("user-b", "version-3", 3),
        buildEnrollment("user-c", "version-2", 2),
    ];

    it("names both versions in use and how many people are on each", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments: mixedEnrollments,
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [],
        });

        render(
            <EnrollmentSpreadSummary
                spread={spread}
                currentPublishedVersion={VERSION_THREE}
                rosterState="ready"
            />
        );

        expect(screen.getByText(/v3 · 2 человека/)).toBeTruthy();
        expect(screen.getByText(/v2 · 1 человек/)).toBeTruthy();
        expect(screen.getByText(/учится по разным версиям/)).toBeTruthy();
    });

    it("says nothing about a mixed state when everybody is on the newest version", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments: mixedEnrollments.slice(0, 2),
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [],
        });

        render(
            <EnrollmentSpreadSummary
                spread={spread}
                currentPublishedVersion={VERSION_THREE}
                rosterState="ready"
            />
        );

        expect(screen.queryByText(/учится по разным версиям/)).toBeNull();
    });

    it("reports the people who hold no pin at all as learning off the live tree", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments: mixedEnrollments,
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [
                { userId: "user-a", displayName: "Иванов А." },
                { userId: "user-b", displayName: "Петров И." },
                { userId: "user-c", displayName: "Сидорова М." },
                { userId: "user-d", displayName: "Кузнецов П." },
            ],
        });

        render(
            <EnrollmentSpreadSummary
                spread={spread}
                currentPublishedVersion={VERSION_THREE}
                rosterState="ready"
            />
        );

        expect(screen.getByText(/Без зачисления/)).toBeTruthy();
        expect(screen.getByText(/живому дереву навыков/).textContent).toContain("1 человек");
    });

    it("says the roster did not load rather than claiming nobody is unenrolled", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments: mixedEnrollments,
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [],
        });

        render(
            <EnrollmentSpreadSummary
                spread={spread}
                currentPublishedVersion={VERSION_THREE}
                rosterState="unavailable"
            />
        );

        expect(screen.getByText(/Список сотрудников сейчас не загрузился/)).toBeTruthy();
        expect(screen.queryByText(/Без зачисления/)).toBeNull();
    });

    it("states outright that nobody is unenrolled when the whole roster holds a pin", () => {
        const spread = summarizeEnrollmentSpread({
            enrollments: mixedEnrollments,
            currentPublishedVersion: VERSION_THREE,
            rosterMembers: [
                { userId: "user-a", displayName: "Иванов А." },
                { userId: "user-b", displayName: "Петров И." },
                { userId: "user-c", displayName: "Сидорова М." },
            ],
        });

        render(
            <EnrollmentSpreadSummary
                spread={spread}
                currentPublishedVersion={VERSION_THREE}
                rosterState="ready"
            />
        );

        expect(screen.getByText(/Без зачисления никого нет/)).toBeTruthy();
    });
});

describe("EnrollmentTable", () => {
    const enrollments = [
        buildEnrollment("user-a", "version-3", 3),
        buildEnrollment("user-c", "version-2", 2),
    ];

    it("marks the person on the older version as behind and the other one as current", () => {
        render(
            <EnrollmentTable
                enrollments={enrollments}
                currentPublishedVersion={VERSION_THREE}
                memberNamesByUserId={new Map([["user-a", "Иванов А."]])}
                isLoading={false}
                onShowPendingDiff={() => {}}
            />
        );

        expect(screen.getByText("Отстаёт")).toBeTruthy();
        expect(screen.getByText("Последняя")).toBeTruthy();
    });

    it("shows an id fragment for a learner the roster cannot name", () => {
        render(
            <EnrollmentTable
                enrollments={enrollments}
                currentPublishedVersion={VERSION_THREE}
                memberNamesByUserId={new Map([["user-a", "Иванов А."]])}
                isLoading={false}
                onShowPendingDiff={() => {}}
            />
        );

        expect(screen.getByText("Без имени · user-c")).toBeTruthy();
    });

    it("offers no control that moves a pin — only reading what a move would change", () => {
        const { container } = render(
            <EnrollmentTable
                enrollments={enrollments}
                currentPublishedVersion={VERSION_THREE}
                memberNamesByUserId={new Map()}
                isLoading={false}
                onShowPendingDiff={() => {}}
            />
        );

        const buttonLabels = [...container.querySelectorAll("button")].map(
            (button) => button.textContent ?? ""
        );
        expect(buttonLabels).toEqual(["Что изменится у него"]);
        expect(container.textContent).not.toContain("Перевести");
    });

    it("marks nobody as behind while no version is published", () => {
        render(
            <EnrollmentTable
                enrollments={enrollments}
                currentPublishedVersion={null}
                memberNamesByUserId={new Map()}
                isLoading={false}
                onShowPendingDiff={() => {}}
            />
        );

        expect(screen.queryByText("Отстаёт")).toBeNull();
        expect(screen.queryByText("Последняя")).toBeNull();
    });

    it("explains the section instead of showing a bare table when nobody is enrolled", () => {
        render(
            <EnrollmentTable
                enrollments={[]}
                currentPublishedVersion={VERSION_THREE}
                memberNamesByUserId={new Map()}
                isLoading={false}
                onShowPendingDiff={() => {}}
            />
        );

        expect(screen.getByText("Никто не зачислен")).toBeTruthy();
    });

    it("shows a person who moved themselves as having done so, not as having been enrolled", () => {
        render(
            <EnrollmentTable
                enrollments={[
                    buildEnrollment("user-a", "version-3", 3, {
                        switchedAt: "2026-08-14T10:00:00Z",
                        previousProgramVersionId: "version-2",
                    }),
                ]}
                currentPublishedVersion={VERSION_THREE}
                memberNamesByUserId={new Map()}
                isLoading={false}
                onShowPendingDiff={() => {}}
            />
        );

        expect(screen.getByText(/перешёл сам 14 авг/)).toBeTruthy();
    });
});

describe("ProgramDiffView", () => {
    it("renders the four buckets as four sections, not as one list", () => {
        render(
            <ProgramDiffView
                diff={buildDiff({
                    addedLessons: [
                        {
                            lessonId: "lesson-1",
                            skillId: "skill-1",
                            lessonVersionId: "lesson-version-1",
                            lessonVersionNumber: 1,
                            lessonTitle: "Работа с возражением «дорого»",
                            orderIndex: 4,
                        },
                    ],
                    removedLessons: [
                        {
                            lessonId: "lesson-2",
                            skillId: "skill-1",
                            lessonVersionId: "lesson-version-2",
                            lessonVersionNumber: 2,
                            lessonTitle: "Старый скрипт",
                            orderIndex: 9,
                        },
                    ],
                    changedLessons: [
                        {
                            lessonId: "lesson-3",
                            skillId: "skill-2",
                            lessonTitle: "Квалификация по бюджету",
                            fromLessonVersionId: "lesson-version-3",
                            fromLessonVersionNumber: 2,
                            toLessonVersionId: "lesson-version-4",
                            toLessonVersionNumber: 4,
                            isBreaking: false,
                        },
                    ],
                    movedLessons: [
                        {
                            lessonId: "lesson-4",
                            lessonTitle: "Первый звонок",
                            fromSkillId: "skill-1",
                            toSkillId: "skill-2",
                            fromOrderIndex: 2,
                            toOrderIndex: 5,
                        },
                    ],
                })}
            />
        );

        expect(screen.getByText(/Добавлены/)).toBeTruthy();
        expect(screen.getByText(/Убраны/)).toBeTruthy();
        expect(screen.getByText(/Новая версия урока/)).toBeTruthy();
        expect(screen.getByText(/Переставлены/)).toBeTruthy();
        expect(screen.getByText("v2 → v4")).toBeTruthy();
        expect(screen.getByText("3 → 6")).toBeTruthy();
    });

    it("hides a bucket that is empty rather than printing an empty heading", () => {
        render(
            <ProgramDiffView
                diff={buildDiff({
                    movedLessons: [
                        {
                            lessonId: "lesson-4",
                            lessonTitle: "Первый звонок",
                            fromSkillId: "skill-1",
                            toSkillId: "skill-1",
                            fromOrderIndex: 2,
                            toOrderIndex: 5,
                        },
                    ],
                })}
            />
        );

        expect(screen.queryByText(/Добавлены/)).toBeNull();
        expect(screen.getByText(/Переставлены/)).toBeTruthy();
    });

    it("raises the breaking-change warning when the server says the answers moved", () => {
        render(<ProgramDiffView diff={buildDiff({ hasBreakingChanges: true })} />);

        expect(
            screen.getByText(
                "В некоторых уроках изменился правильный ответ или критерии оценки."
            )
        ).toBeTruthy();
    });

    it("says an identical pair of versions differs in nothing, instead of showing four empty sections", () => {
        render(<ProgramDiffView diff={buildDiff()} />);

        expect(screen.getByText(/нет ни одного отличия/)).toBeTruthy();
        expect(screen.queryByText(/Добавлены/)).toBeNull();
    });

    it("shows «Урок недоступен» for a snapshot whose title is no longer visible", () => {
        render(
            <ProgramDiffView
                diff={buildDiff({
                    addedLessons: [
                        {
                            lessonId: "lesson-1",
                            skillId: "skill-1",
                            lessonVersionId: "lesson-version-1",
                            lessonVersionNumber: null,
                            lessonTitle: null,
                            orderIndex: 0,
                        },
                    ],
                })}
            />
        );

        expect(screen.getByText("Урок недоступен")).toBeTruthy();
    });
});
