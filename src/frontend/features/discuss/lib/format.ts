/** Compact Russian "time ago" label, e.g. "2 ч", "5 мин", "3 д". */
export function formatTimeAgo(isoDate: string): string {
    const then = new Date(isoDate).getTime();
    const seconds = Math.max(0, Math.floor((Date.now() - then) / 1000));

    if (seconds < 60) return "только что";
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes} мин`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours} ч`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `${days} д`;
    const months = Math.floor(days / 30);
    if (months < 12) return `${months} мес`;
    return `${Math.floor(months / 12)} г`;
}

/** Russian plural helper: pluralizeRu(2, "тема", "темы", "тем") -> "темы". */
export function pluralizeRu(count: number, one: string, few: string, many: string): string {
    const mod10 = count % 10;
    const mod100 = count % 100;
    if (mod10 === 1 && mod100 !== 11) return one;
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return few;
    return many;
}
