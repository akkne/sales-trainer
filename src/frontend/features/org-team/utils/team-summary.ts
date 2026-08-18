import type { TeamSkillMap } from "@/features/org-shell/hooks/use-team-directory";

type RussianPluralForms = readonly [singular: string, few: string, many: string];

export const ATTEMPT_PLURAL_FORMS: RussianPluralForms = ["попытка", "попытки", "попыток"];
export const PERSON_PLURAL_FORMS: RussianPluralForms = ["человек", "человека", "человек"];
export const MANAGER_PLURAL_FORMS: RussianPluralForms = ["менеджер", "менеджера", "менеджеров"];

const LAST_TWO_DIGITS_DIVISOR = 100;
const LAST_DIGIT_DIVISOR = 10;

/// Russian counts three ways, and «12 человека» in a panel a customer pays for reads as a machine
/// talking. The panel is «вы»-form and it has to sound like it was written by somebody.
export function pluralizeRussian(count: number, forms: RussianPluralForms): string {
    const absoluteCount = Math.abs(Math.trunc(count));
    const lastTwoDigits = absoluteCount % LAST_TWO_DIGITS_DIVISOR;
    const lastDigit = absoluteCount % LAST_DIGIT_DIVISOR;

    if (lastTwoDigits >= 11 && lastTwoDigits <= 14) return forms[2];
    if (lastDigit === 1) return forms[0];
    if (lastDigit >= 2 && lastDigit <= 4) return forms[1];
    return forms[2];
}

export function formatCountWithNoun(count: number, forms: RussianPluralForms): string {
    return `${new Intl.NumberFormat("ru-RU").format(count)} ${pluralizeRussian(count, forms)}`;
}

const RUSSIAN_YEAR_SUFFIX = " г.";

/// «20 мая 2026». `toLocaleDateString` appends «г.», which nobody says out loud when reading a
/// dashboard subtitle.
export function formatWindowStartDate(isoDate: string): string {
    const windowStart = new Date(isoDate);
    if (Number.isNaN(windowStart.getTime())) return "";

    return windowStart
        .toLocaleDateString("ru-RU", { day: "numeric", month: "long", year: "numeric" })
        .replace(RUSSIAN_YEAR_SUFFIX, "");
}

export interface TeamWindowSummary {
    memberCount: number;
    /// Every attempt inside the window, including the ones no skill could be named for — the
    /// footnote counts them separately but the headline must not pretend they did not happen.
    attemptCount: number;
}

export function summarizeTeamWindow(
    skillMap: TeamSkillMap,
    memberCount: number
): TeamWindowSummary {
    const attributedAttemptCount = skillMap.stages.reduce(
        (runningTotal, stage) => runningTotal + stage.attemptCount,
        0
    );

    return {
        memberCount,
        attemptCount: attributedAttemptCount + skillMap.unattributedAttemptCount,
    };
}
