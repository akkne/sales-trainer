import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import type { TeamSkillMap } from "@/features/org-shell/hooks/use-team-directory";
import { SkillHeatMap } from "@/features/org-team/components/skill-heat-map";
import { mergeTeamRoster } from "@/features/org-team/utils/team-roster";

vi.mock("next/link", () => ({
    default: ({ children, href }: { children: React.ReactNode; href: string }) => (
        <a href={href}>{children}</a>
    ),
}));

const skillMap: TeamSkillMap = {
    windowStart: "2026-05-20T00:00:00Z",
    stages: [
        {
            key: "contact",
            label: "Контакт",
            accent: "",
            order: 1,
            attemptCount: 300,
            accuracyPercent: 78,
        },
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
    members: [
        {
            userId: "ivanov",
            displayName: "Иванов А.",
            isActiveMember: true,
            attemptCount: 120,
            accuracyPercent: 62,
            weakestStageKey: "closing",
            weakestSkillId: null,
            dialogCount: 4,
            dialogAverageScore: 62,
            stages: [
                { key: "contact", attemptCount: 90, accuracyPercent: 82 },
                { key: "closing", attemptCount: 3, accuracyPercent: null },
            ],
            skills: [],
        },
        {
            userId: "sidorov",
            displayName: "Сидоров К.",
            isActiveMember: true,
            attemptCount: 0,
            accuracyPercent: null,
            weakestStageKey: null,
            weakestSkillId: null,
            dialogCount: 0,
            dialogAverageScore: null,
            stages: [],
            skills: [],
        },
    ],
    unattributedAttemptCount: 340,
    minimumAttemptsForAccuracy: 5,
    rosterKnown: true,
};

const FORBIDDEN_GAMIFICATION_PATTERNS = [
    /\bxp\b/iu,
    /\bопыт/u,
    /\bстрик/u,
    /\bstreak/iu,
    /\bлиг[аеиу]/u,
    /\bleague/iu,
];

/**
 * The states O1's heat map has to survive (docs/TENANCY/ADMIN_UI_DESIGN.md, O1 «Состояния»).
 *
 * Every one of these is a wrong answer the screen could give without throwing: a withheld
 * percentage drawn as 0%, «слаб везде» printed for somebody with no data, a departed mark shown
 * when nobody could check who works here, or a leaderboard growing back on a team dashboard.
 */
describe("O1 heat map", () => {
    function renderHeatMap(overrides?: {
        skillMapOverrides?: Partial<TeamSkillMap>;
        roster?: Parameters<typeof mergeTeamRoster>[1];
    }) {
        const effectiveSkillMap = { ...skillMap, ...overrides?.skillMapOverrides };
        const merged = mergeTeamRoster(effectiveSkillMap, overrides?.roster ?? null);

        return render(
            <SkillHeatMap
                skillMap={effectiveSkillMap}
                rows={merged.rows}
                isRosterKnown={merged.isRosterKnown}
                axis="stages"
                onAxisChange={() => {}}
            />
        );
    }

    it("draws a withheld percentage as a dash and explains it, never as zero", () => {
        renderHeatMap();

        expect(
            screen.getByLabelText("Иванов А. · Закрытие: меньше 5 попыток")
        ).toHaveTextContent("—");
        expect(screen.queryByLabelText(/Иванов А. · Закрытие: 0%/u)).toBeNull();
    });

    it("says «нет данных» for somebody with no weakest stage instead of «слаб везде»", () => {
        renderHeatMap();

        expect(screen.getByText("нет данных")).toBeInTheDocument();
    });

    it("sends a manager's name to that person's conversations", () => {
        renderHeatMap();

        expect(screen.getByRole("link", { name: "Иванов А." })).toHaveAttribute(
            "href",
            "/org/dialogs?userId=ivanov"
        );
    });

    it("footnotes the attempts no skill could be named for", () => {
        renderHeatMap();

        expect(
            screen.getByText(/не отнесены к навыку: упражнение удалено из библиотеки/u)
        ).toBeInTheDocument();
    });

    it("withholds every «уже не работает» mark when nobody could check the roster", () => {
        renderHeatMap({
            skillMapOverrides: {
                rosterKnown: false,
                members: skillMap.members.map((member) => ({ ...member, isActiveMember: null })),
            },
        });

        expect(screen.getByRole("status")).toHaveTextContent(
            "Не удалось проверить, кто ещё работает в компании"
        );
        expect(screen.queryByTitle("уже не работает в компании")).toBeNull();
    });

    it("marks the departed once the roster says so, and footnotes the dagger", () => {
        renderHeatMap({
            roster: [
                {
                    userId: "ivanov",
                    email: "ivanov@example.com",
                    displayName: "Иванов А.",
                    role: "Manager",
                    status: "Deactivated",
                    joinedAt: "2026-01-01T00:00:00Z",
                    deactivatedAt: "2026-07-01T00:00:00Z",
                },
                {
                    userId: "sidorov",
                    email: "sidorov@example.com",
                    displayName: "Сидоров К.",
                    role: "Manager",
                    status: "Active",
                    joinedAt: "2026-01-01T00:00:00Z",
                    deactivatedAt: null,
                },
            ],
        });

        expect(screen.getByTitle("уже не работает в компании")).toBeInTheDocument();
        expect(screen.getByText(/уже не работает в компании$/u)).toBeInTheDocument();
        expect(screen.queryByRole("status")).toBeNull();
    });

    it("shows no XP, no streaks and no leagues on a team dashboard", () => {
        const { container } = renderHeatMap();

        for (const forbiddenPattern of FORBIDDEN_GAMIFICATION_PATTERNS) {
            expect(container.textContent ?? "").not.toMatch(forbiddenPattern);
        }
    });
});
