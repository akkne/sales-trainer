import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AssignmentFunnel } from "@/features/org-assignments/components/assignment-funnel";
import { AssignmentFunnelBar } from "@/features/org-assignments/components/assignment-funnel-bar";
import { RemindDialog } from "@/features/org-assignments/components/remind-dialog";
import { CompletionRuleEditor } from "@/features/org-assignments/components/completion-rule-editor";
import { EMPTY_COMPLETION_RULE_DRAFT } from "@/features/org-assignments/utils/completion-rule-draft";
import type {
    AssignmentDashboardRow,
    AssignmentFunnel as AssignmentFunnelData,
    AssignmentSummary,
} from "@/features/org-assignments/types/assignment";

const funnel: AssignmentFunnelData = {
    assignedCount: 12,
    notStartedCount: 3,
    startedCount: 9,
    completedCount: 6,
    failedThresholdCount: 3,
    leftOrganizationCount: 1,
    assignedActiveCount: 11,
};

const rows: AssignmentDashboardRow[] = [
    {
        userId: "3f2a1b9c-0000-0000-0000-000000000001",
        displayName: null,
        status: "not_started",
        bestScore: null,
        attemptCount: 0,
        firstOpenedAt: null,
        completedAt: null,
        isActiveMember: true,
    },
    {
        userId: "00000000-0000-0000-0000-000000000002",
        displayName: "Иванов А.",
        status: "failed_threshold",
        bestScore: 61,
        attemptCount: 4,
        firstOpenedAt: "2026-08-19T00:00:00Z",
        completedAt: null,
        isActiveMember: true,
    },
    {
        userId: "00000000-0000-0000-0000-000000000003",
        displayName: "Волков С.",
        status: "completed",
        bestScore: 84,
        attemptCount: 3,
        firstOpenedAt: "2026-08-19T00:00:00Z",
        completedAt: "2026-08-21T00:00:00Z",
        isActiveMember: true,
    },
];

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

describe("AssignmentFunnelBar", () => {
    it("draws «ниже порога» only when somebody is under the bar", () => {
        const { rerender } = render(<AssignmentFunnelBar summary={buildSummary()} />);
        expect(screen.getByText("▲ 3 ниже порога")).toBeInTheDocument();

        rerender(<AssignmentFunnelBar summary={buildSummary({ failedThresholdCount: 0 })} />);
        expect(screen.queryByText(/ниже порога/)).not.toBeInTheDocument();
    });

    it("shows a dash rather than an empty bar for an assignment nobody holds yet", () => {
        render(
            <AssignmentFunnelBar
                summary={buildSummary({
                    assignedCount: 0,
                    startedCount: 0,
                    completedCount: 0,
                    failedThresholdCount: 0,
                })}
            />
        );

        expect(screen.getByText("—")).toBeInTheDocument();
    });
});

describe("AssignmentFunnel", () => {
    it("shows five stages with «ниже порога» among them", () => {
        render(<AssignmentFunnel funnel={funnel} isRosterKnown />);

        for (const label of ["Выдано", "Не начали", "Начали", "Выполнили", "Ниже порога"]) {
            expect(screen.getByText(label)).toBeInTheDocument();
        }
    });

    it("says it could not check the roster instead of drawing a zero", () => {
        render(<AssignmentFunnel funnel={funnel} isRosterKnown={false} />);

        expect(
            screen.getByText("Не удалось проверить, кто ещё работает в компании.")
        ).toBeInTheDocument();
    });

    it("reads an untouched issued assignment as a first day, not as a failure", () => {
        render(
            <AssignmentFunnel
                funnel={{
                    assignedCount: 12,
                    notStartedCount: 12,
                    startedCount: 0,
                    completedCount: 0,
                    failedThresholdCount: 0,
                    leftOrganizationCount: 0,
                    assignedActiveCount: 12,
                }}
                isRosterKnown
            />
        );

        expect(screen.getByText("Выдано 12 · пока никто не начал.")).toBeInTheDocument();
    });
});

describe("RemindDialog", () => {
    it("names its recipients and sends nothing until the button is pressed", async () => {
        const onConfirm = vi.fn();
        render(
            <RemindDialog
                open
                scope="not_started"
                onScopeChange={() => {}}
                funnel={funnel}
                rows={rows}
                onClose={() => {}}
                onConfirm={onConfirm}
                isPending={false}
                error={null}
            />
        );

        expect(screen.getByText("Без имени · 3f2a1b9c")).toBeInTheDocument();
        expect(screen.queryByText("Иванов А.")).not.toBeInTheDocument();
        expect(onConfirm).not.toHaveBeenCalled();

        await userEvent.click(screen.getByRole("button", { name: "Отправить напоминание" }));
        expect(onConfirm).toHaveBeenCalledTimes(1);
    });

    it("widens to everybody unfinished, leaving out only the people who are done", () => {
        render(
            <RemindDialog
                open
                scope="unfinished"
                onScopeChange={() => {}}
                funnel={funnel}
                rows={rows}
                onClose={() => {}}
                onConfirm={() => {}}
                isPending={false}
                error={null}
            />
        );

        expect(screen.getByText("Иванов А.")).toBeInTheDocument();
        expect(screen.queryByText("Волков С.")).not.toBeInTheDocument();
    });

    it("counts from the funnel, in both scope labels", () => {
        render(
            <RemindDialog
                open
                scope="not_started"
                onScopeChange={() => {}}
                funnel={funnel}
                rows={rows}
                onClose={() => {}}
                onConfirm={() => {}}
                isPending={false}
                error={null}
            />
        );

        expect(screen.getByText(/Напомнить тем, кто не начал \(3\)/)).toBeInTheDocument();
        expect(screen.getByText(/Напомнить всем, кто не закончил \(6\)/)).toBeInTheDocument();
    });
});

describe("CompletionRuleEditor", () => {
    it("cannot select a rule the assignment has no content for", () => {
        render(
            <CompletionRuleEditor
                draft={EMPTY_COMPLETION_RULE_DRAFT}
                contentKinds={["dialog_scenario"]}
                onChange={() => {}}
            />
        );

        const radios = screen.getAllByRole("radio");
        expect(radios[0]).toBeEnabled();
        expect(radios[1]).toBeDisabled();
        expect(radios[0]).not.toBeChecked();
    });

    it("warns about the unmeasured half when the assignment carries both", () => {
        render(
            <CompletionRuleEditor
                draft={{ ...EMPTY_COMPLETION_RULE_DRAFT, kind: "dialog_score" }}
                contentKinds={["dialog_scenario", "lesson_version"]}
                onChange={() => {}}
            />
        );

        expect(screen.getByText(/порог измеряет только разговоры/)).toBeInTheDocument();
    });
});
