const RUSSIAN_YEAR_SUFFIX = " г.";

export const UNNAMED_MEMBER_LABEL = "Без имени";

/// «25 авг.» — the invite expiry, where the year is noise: an invite lives days, not months.
export function formatShortRussianDate(isoDate: string | null): string {
    if (!isoDate) return "—";

    const parsedDate = new Date(isoDate);
    if (Number.isNaN(parsedDate.getTime())) return isoDate;

    return new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "short" }).format(parsedDate);
}

/// «12 марта 2026» — the joining date, where the year is the whole point. `toLocaleDateString`
/// appends «г.», which nobody says out loud when reading a table.
export function formatLongRussianDate(isoDate: string | null): string {
    if (!isoDate) return "—";

    const parsedDate = new Date(isoDate);
    if (Number.isNaN(parsedDate.getTime())) return isoDate;

    return parsedDate
        .toLocaleDateString("ru-RU", { day: "numeric", month: "long", year: "numeric" })
        .replace(RUSSIAN_YEAR_SUFFIX, "");
}

/// A membership row whose user has never set a display name still has to be identifiable, and the
/// email is the only other thing identity-service holds about them.
export function describeMemberName(displayName: string, email: string): string {
    const trimmedDisplayName = displayName.trim();
    if (trimmedDisplayName.length > 0) return trimmedDisplayName;

    const trimmedEmail = email.trim();
    return trimmedEmail.length > 0 ? trimmedEmail : UNNAMED_MEMBER_LABEL;
}

const INITIALS_LENGTH = 2;

export function buildMemberInitials(displayName: string, email: string): string {
    const words = describeMemberName(displayName, email).split(/\s+/).filter(Boolean);
    if (words.length === 0) return "";

    const letters =
        words.length > 1
            ? words.slice(0, INITIALS_LENGTH).map((word) => word[0])
            : words[0].slice(0, INITIALS_LENGTH).split("");

    return letters.join("").toUpperCase();
}
