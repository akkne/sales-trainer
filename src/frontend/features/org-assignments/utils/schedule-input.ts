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

/** An ISO instant back into the `yyyy-mm-ddThh:mm` a datetime-local input can hold. */
export function writeDateTimeInput(isoInstant: string | null): string {
    if (!isoInstant) return "";

    const parsedDate = new Date(isoInstant);
    if (Number.isNaN(parsedDate.getTime())) return "";

    const hours = String(parsedDate.getHours()).padStart(2, "0");
    const minutes = String(parsedDate.getMinutes()).padStart(2, "0");

    return `${writeDateInput(isoInstant)}T${hours}:${minutes}`;
}
