export function pluralizeRu(count: number, [one, few, many]: [string, string, string]): string {
    const mod100 = count % 100;
    const mod10 = count % 10;
    if (mod10 === 1 && mod100 !== 11) return one;
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few;
    return many;
}

export function pluralizeCompanies(count: number): string {
    return pluralizeRu(count, ["компания", "компании", "компаний"]);
}

export function companiesCountLabel(count: number): string {
    return `${count} ${pluralizeCompanies(count)}`;
}

const RU_MONTHS = [
    "янв", "фев", "мар", "апр", "мая", "июн",
    "июл", "авг", "сен", "окт", "ноя", "дек",
];

export function formatDateRu(iso: string): string {
    const date = new Date(iso);
    return `${date.getDate()} ${RU_MONTHS[date.getMonth()]} ${date.getFullYear()}`;
}

export function formatCalendarDateRu(iso: string): string {
    const date = new Date(iso);
    return `${date.getUTCDate()} ${RU_MONTHS[date.getUTCMonth()]} ${date.getUTCFullYear()}`;
}

export function relativeTimeRu(iso: string): string {
    const diffMs = Date.now() - new Date(iso).getTime();
    const minutes = Math.floor(diffMs / 60_000);

    if (minutes < 1) return "только что";
    if (minutes < 60) return `${minutes} мин назад`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours} ч назад`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `${days} д назад`;
    return formatDateRu(iso);
}
