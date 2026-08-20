const END_OF_DAY_HOURS = 23;
const END_OF_DAY_MINUTES = 59;

/**
 * A `<input type="date">` value read as the last minute of that day.
 *
 * A deadline typed as a bare date means "by the end of the 24th" to the person typing it; sending
 * midnight would quietly move it a day earlier for everybody.
 */
export function readDeadlineInput(dateValue: string): string | null {
    if (!dateValue) return null;

    const [year, month, day] = dateValue.split("-").map(Number);
    if (!year || !month || !day) return null;

    return new Date(year, month - 1, day, END_OF_DAY_HOURS, END_OF_DAY_MINUTES).toISOString();
}

/** An ISO instant back into the `yyyy-mm-dd` a date input can hold. */
export function writeDateInput(isoInstant: string | null): string {
    if (!isoInstant) return "";

    const parsedDate = new Date(isoInstant);
    if (Number.isNaN(parsedDate.getTime())) return "";

    const month = String(parsedDate.getMonth() + 1).padStart(2, "0");
    const day = String(parsedDate.getDate()).padStart(2, "0");

    return `${parsedDate.getFullYear()}-${month}-${day}`;
}

export function readOpensAtInput(dateTimeValue: string): string | null {
    if (!dateTimeValue) return null;

    const parsedDate = new Date(dateTimeValue);
    if (Number.isNaN(parsedDate.getTime())) return null;

    return parsedDate.toISOString();
}

/**
 * O-6 (`docs/AUDIT_PROD.md`) — a deadline dated before today reads as issued-already-overdue to
 * whoever it lands on. `readDeadlineInput` already reads the input as the end of that day, so this
 * only flags a day that has fully passed, not "today".
 */
export function isDeadlineInPast(dateValue: string): boolean {
    const deadlineIso = readDeadlineInput(dateValue);
    return deadlineIso !== null && new Date(deadlineIso).getTime() < Date.now();
}

/**
 * O-6 — mirrors the server's own `RequireConsistentSchedule` (deadline must come after the opening
 * time) so the screen refuses before the request does.
 */
export function isOpensAtAfterDeadline(dateTimeValue: string, dateValue: string): boolean {
    const opensAtIso = readOpensAtInput(dateTimeValue);
    const deadlineIso = readDeadlineInput(dateValue);
    if (opensAtIso === null || deadlineIso === null) return false;

    return new Date(deadlineIso).getTime() <= new Date(opensAtIso).getTime();
}

/** An ISO instant back into the `yyyy-mm-ddThh:mm` a datetime-local input can hold. */
export function writeDateTimeInput(isoInstant: string | null): string {
    if (!isoInstant) return "";

    const parsedDate = new Date(isoInstant);
    if (Number.isNaN(parsedDate.getTime())) return "";

    const hours = String(parsedDate.getHours()).padStart(2, "0");
    const minutes = String(parsedDate.getMinutes()).padStart(2, "0");

    return `${writeDateInput(isoInstant)}T${hours}:${minutes}`;
}
