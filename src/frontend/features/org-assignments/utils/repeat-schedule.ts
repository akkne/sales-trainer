import type { AssignmentRepeatSchedule } from "@/features/org-assignments/types/assignment";

/** learning-service's `AssignmentRepeatScheduleLimits`, mirrored so the screen refuses first. */
export const REPEAT_SCHEDULE_LIMITS = {
    maximumWaveCount: 4,
    minimumOffsetDays: 1,
    maximumOffsetDays: 180,
} as const;

/** The roadmap's default: a week later, then three weeks after that. */
export const DEFAULT_REPEAT_OFFSET_DAYS: number[] = [7, 21];

const FIXED_OFFSETS_KIND = "fixed_offsets";

/** The refusal to show under the repeat section, or null when the offsets can be sent. */
export function validateRepeatOffsetDays(offsetDays: number[]): string | null {
    if (offsetDays.length === 0) {
        return "Укажите хотя бы один интервал повтора.";
    }

    if (offsetDays.length > REPEAT_SCHEDULE_LIMITS.maximumWaveCount) {
        return `Повторов может быть не больше ${REPEAT_SCHEDULE_LIMITS.maximumWaveCount}.`;
    }

    for (const offsetDay of offsetDays) {
        if (
            !Number.isInteger(offsetDay) ||
            offsetDay < REPEAT_SCHEDULE_LIMITS.minimumOffsetDays ||
            offsetDay > REPEAT_SCHEDULE_LIMITS.maximumOffsetDays
        ) {
            return `Интервал повтора — целое число дней от ${REPEAT_SCHEDULE_LIMITS.minimumOffsetDays} до ${REPEAT_SCHEDULE_LIMITS.maximumOffsetDays}.`;
        }
    }

    const isStrictlyAscending = offsetDays.every(
        (offsetDay, index) => index === 0 || offsetDay > offsetDays[index - 1]
    );
    if (!isStrictlyAscending) {
        return "Интервалы повтора должны идти по возрастанию и не повторяться, например 7 и 21.";
    }

    return null;
}

export function buildRepeatScheduleDocument(
    isRepeatEnabled: boolean,
    offsetDays: number[]
): AssignmentRepeatSchedule | null {
    if (!isRepeatEnabled) return null;

    return { kind: FIXED_OFFSETS_KIND, offsetDays };
}

/**
 * The offsets an editor should start from. An unrecognised schedule kind yields the default rather
 * than a guess: 40.24 owns this vocabulary, and rewriting a schedule this client cannot read would
 * silently replace it.
 */
export function readRepeatOffsetDays(
    schedule: AssignmentRepeatSchedule | null | undefined
): number[] {
    if (!schedule || schedule.kind !== FIXED_OFFSETS_KIND) return DEFAULT_REPEAT_OFFSET_DAYS;
    if (!schedule.offsetDays || schedule.offsetDays.length === 0) return DEFAULT_REPEAT_OFFSET_DAYS;

    return schedule.offsetDays;
}

/** «повтор +7, +21», or null when there is no schedule this client can read. */
export function describeRepeatSchedule(
    schedule: AssignmentRepeatSchedule | null | undefined
): string | null {
    if (!schedule || schedule.kind !== FIXED_OFFSETS_KIND) return null;
    if (!schedule.offsetDays || schedule.offsetDays.length === 0) return null;

    return `повтор ${schedule.offsetDays.map((offsetDay) => `+${offsetDay}`).join(", ")}`;
}
