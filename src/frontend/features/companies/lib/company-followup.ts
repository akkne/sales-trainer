export type FollowUpTone = "due" | "overdue";

interface FollowUpToneMeta {
    readonly label: string;
    readonly toneClassName: string;
}

const FOLLOW_UP_TONE_META: Record<FollowUpTone, FollowUpToneMeta> = {
    due: { label: "Скоро", toneClassName: "co-followup--due" },
    overdue: { label: "Просрочено", toneClassName: "co-followup--overdue" },
};

/** How far ahead of the due date a follow-up starts showing as "due soon" rather than plain. */
const DUE_SOON_WINDOW_MS = 24 * 60 * 60 * 1000;

/**
 * Returns the badge tone for a scheduled follow-up, or null when there's nothing to show:
 * no follow-up scheduled, or a follow-up scheduled more than a day out.
 */
export function getFollowUpTone(nextActionAt: string | null | undefined, now: Date = new Date()): FollowUpTone | null {
    if (!nextActionAt) return null;

    const dueAt = new Date(nextActionAt).getTime();
    if (Number.isNaN(dueAt)) return null;

    const diff = dueAt - now.getTime();
    if (diff < 0) return "overdue";
    if (diff <= DUE_SOON_WINDOW_MS) return "due";
    return null;
}

export function getFollowUpToneMeta(tone: FollowUpTone): FollowUpToneMeta {
    return FOLLOW_UP_TONE_META[tone];
}
