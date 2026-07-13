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

/** English plural helper: pluralizeRu(2, "reply", "replies", "replies") -> "replies". */
export function pluralizeRu(count: number, one: string, _few: string, _many: string): string {
    return count === 1 ? one : _many;
}
