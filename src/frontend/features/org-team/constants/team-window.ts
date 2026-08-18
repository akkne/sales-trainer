/// The three windows O1 offers, and the one it opens on.
///
/// One selector drives both `GET /admin/team/skill-map` and `GET /admin/team/skill-gaps`
/// (ADMIN_UI_DESIGN.md O1): a heat map drawn over ninety days beside suggestions computed over
/// thirty is a disagreement the screen cannot explain.
export const TEAM_WINDOW_DAY_OPTIONS = [30, 90, 180] as const;

export type TeamWindowDays = (typeof TEAM_WINDOW_DAY_OPTIONS)[number];

export const DEFAULT_TEAM_WINDOW_DAYS: TeamWindowDays = 30;

export const TEAM_WINDOW_LABELS: Record<TeamWindowDays, string> = {
    30: "30 дней",
    90: "90 дней",
    180: "180 дней",
};
