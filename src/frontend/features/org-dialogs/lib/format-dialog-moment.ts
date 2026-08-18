/**
 * When a conversation happened, in the shortest form that is still unambiguous
 * (docs/TENANCY/ADMIN_UI_DESIGN.md O5: «вчера, 14:20», «18 авг»).
 *
 * A РОП scanning this column is placing rows against last week, not reading timestamps, so today
 * and yesterday get a clock and everything older gets a date. The year appears only when the
 * conversation is not from this one — «18 авг 2025» in a list of August rows is noise until the
 * one row it applies to.
 */

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

function isSameCalendarDay(left: Date, right: Date): boolean {
    return (
        left.getFullYear() === right.getFullYear() &&
        left.getMonth() === right.getMonth() &&
        left.getDate() === right.getDate()
    );
}

function formatClock(moment: Date): string {
    const hours = String(moment.getHours()).padStart(2, "0");
    const minutes = String(moment.getMinutes()).padStart(2, "0");
    return `${hours}:${minutes}`;
}

/** Empty string for an unparseable timestamp — a broken date must not render as «Invalid Date». */
export function formatDialogMoment(isoTimestamp: string, now: Date = new Date()): string {
    const moment = new Date(isoTimestamp);
    if (Number.isNaN(moment.getTime())) return "";

    if (isSameCalendarDay(moment, now)) {
        return `сегодня, ${formatClock(moment)}`;
    }

    const yesterday = new Date(now);
    yesterday.setDate(yesterday.getDate() - 1);
    if (isSameCalendarDay(moment, yesterday)) {
        return `вчера, ${formatClock(moment)}`;
    }

    const dayAndMonth = `${moment.getDate()} ${SHORT_MONTH_NAMES[moment.getMonth()]}`;
    return moment.getFullYear() === now.getFullYear()
        ? dayAndMonth
        : `${dayAndMonth} ${moment.getFullYear()}`;
}

/** The same instant spelled out in full, for a screen header and for `title` on a short label. */
export function formatDialogMomentInFull(isoTimestamp: string): string {
    const moment = new Date(isoTimestamp);
    if (Number.isNaN(moment.getTime())) return "";
    return `${moment.getDate()} ${SHORT_MONTH_NAMES[moment.getMonth()]} ${moment.getFullYear()}, ${formatClock(moment)}`;
}
