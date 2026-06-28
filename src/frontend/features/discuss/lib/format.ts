/** Compact English "time ago" label, e.g. "2 h", "5 min", "3 d". */
export function formatTimeAgo(isoDate: string): string {
    const then = new Date(isoDate).getTime();
    const seconds = Math.max(0, Math.floor((Date.now() - then) / 1000));

    if (seconds < 60) return "just now";
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes} min`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours} h`;
    const days = Math.floor(hours / 24);
    if (days < 30) return `${days} d`;
    const months = Math.floor(days / 30);
    if (months < 12) return `${months} mo`;
    return `${Math.floor(months / 12)} y`;
}

/** English plural helper: pluralizeRu(2, "reply", "replies", "replies") -> "replies". */
export function pluralizeRu(count: number, one: string, _few: string, _many: string): string {
    return count === 1 ? one : _many;
}
