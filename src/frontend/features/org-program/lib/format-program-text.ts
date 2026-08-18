const SHORT_MONTH_NAMES = [
    "янв",
    "фев",
    "мар",
    "апр",
    "мая",
    "июн",
    "июл",
    "авг",
    "сен",
    "окт",
    "ноя",
    "дек",
] as const;

/**
 * «12 авг», and «12 авг 2025» once the year stops being the current one. An unparseable or missing
 * timestamp renders as an empty string — a broken date must never reach the screen as
 * «Invalid Date».
 */
export function formatProgramDate(isoTimestamp: string | null, now: Date = new Date()): string {
    if (!isoTimestamp) return "";

    const moment = new Date(isoTimestamp);
    if (Number.isNaN(moment.getTime())) return "";

    const dayAndMonth = `${moment.getDate()} ${SHORT_MONTH_NAMES[moment.getMonth()]}`;
    return moment.getFullYear() === now.getFullYear()
        ? dayAndMonth
        : `${dayAndMonth} ${moment.getFullYear()}`;
}

/** `3` → `"v3"`. One place, because the label appears in five components and in three sentences. */
export function formatVersionLabel(versionNumber: number): string {
    return `v${versionNumber}`;
}

/**
 * Picks the Russian form for a count: `[one, few, many]` — «1 урок», «2 урока», «5 уроков», with the
 * 11–14 exception that catches every naive implementation.
 */
export function pluralizeRussianCount(
    count: number,
    forms: readonly [string, string, string]
): string {
    const absoluteCount = Math.abs(count) % 100;
    const lastDigit = absoluteCount % 10;

    if (absoluteCount > 10 && absoluteCount < 20) return forms[2];
    if (lastDigit > 1 && lastDigit < 5) return forms[1];
    if (lastDigit === 1) return forms[0];
    return forms[2];
}

export function describeLessonCount(count: number): string {
    return `${count} ${pluralizeRussianCount(count, ["урок", "урока", "уроков"])}`;
}

export function describePersonCount(count: number): string {
    return `${count} ${pluralizeRussianCount(count, ["человек", "человека", "человек"])}`;
}

/**
 * A learner the roster does not name. The panel shows an id fragment rather than «Неизвестный»,
 * so that two unnamed rows stay distinguishable and support can match one against the database.
 */
export function describeUnknownPerson(userId: string): string {
    return `Без имени · ${userId.slice(0, 8)}`;
}
