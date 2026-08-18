import type { ContentQueueCounts } from "@/features/org-content-generation/hooks/use-content-hub-counters";

/**
 * Russian agreement, computed rather than concatenated: «1 прогон / 2 прогона / 5 прогонов /
 * 11 прогонов / 21 прогон». The 11–14 exception is the one everybody gets wrong, so it is first.
 */
export function pluralizeRussianCount(
    count: number,
    oneForm: string,
    fewForm: string,
    manyForm: string
): string {
    const lastTwoDigits = Math.abs(count) % 100;
    if (lastTwoDigits >= 11 && lastTwoDigits <= 14) return manyForm;

    const lastDigit = lastTwoDigits % 10;
    if (lastDigit === 1) return oneForm;
    if (lastDigit >= 2 && lastDigit <= 4) return fewForm;

    return manyForm;
}

export interface QueueCardCopy {
    /** The lines of numbers, in reading order. Empty when the queue has nothing in it yet. */
    lines: string[];
    /**
     * What the section is for, shown instead of the numbers when there are none. An empty queue in
     * this panel explains the section rather than reporting a zero — the РОП opening «Контент» for
     * the first time needs to learn what would appear there.
     */
    emptyDescription: string;
}

const OWN_LESSONS_EMPTY_DESCRIPTION =
    "Своих уроков ещё нет. Загрузите материалы внутреннего тренинга — ИИ разберёт их, покажет структуру, вы её поправите, и только потом появятся упражнения.";

const ADAPTATIONS_EMPTY_DESCRIPTION =
    "Пакетов правки ещё не было. Можно взять этап воронки целиком и попросить переписать его под ваш продукт и тон — или показать, что в нём методически не так. Каждое предложение вы принимаете поштучно.";

const OVERRIDES_EMPTY_DESCRIPTION =
    "Своих версий уроков пока нет. Как только вы поправите урок из общей библиотеки, здесь появится ваша копия — и мы предупредим, когда исходный урок обновится.";

export function describeOwnLessonsQueue(counts: ContentQueueCounts): QueueCardCopy {
    const lines: string[] = [];

    if (counts.awaitingReviewRunCount > 0) {
        lines.push(
            `${counts.awaitingReviewRunCount} ${pluralizeRussianCount(counts.awaitingReviewRunCount, "ждёт", "ждут", "ждут")} проверки`
        );
    }
    if (counts.insufficientRunCount > 0) {
        lines.push(
            `${counts.insufficientRunCount} ${pluralizeRussianCount(counts.insufficientRunCount, "ждёт", "ждут", "ждут")} материала`
        );
    }
    if (counts.completedRunCount > 0) {
        lines.push(
            `${counts.completedRunCount} ${pluralizeRussianCount(counts.completedRunCount, "готовый", "готовых", "готовых")}`
        );
    }

    return { lines, emptyDescription: OWN_LESSONS_EMPTY_DESCRIPTION };
}

export function describeAdaptationsQueue(counts: ContentQueueCounts): QueueCardCopy {
    const lines: string[] = [];

    if (counts.awaitingReviewProposalCount > 0) {
        lines.push(
            `${counts.awaitingReviewProposalCount} ${pluralizeRussianCount(counts.awaitingReviewProposalCount, "предложение ждёт", "предложения ждут", "предложений ждут")} вашего ответа`
        );
    }

    return { lines, emptyDescription: ADAPTATIONS_EMPTY_DESCRIPTION };
}

export function describeOverridesQueue(counts: ContentQueueCounts): QueueCardCopy {
    const lines: string[] = [];

    if (counts.staleOverrideCount > 0) {
        lines.push(
            `${counts.staleOverrideCount} ${pluralizeRussianCount(counts.staleOverrideCount, "устарела", "устарели", "устарели")}`
        );
    }
    if (counts.totalOverrideCount > 0) {
        lines.push(`${counts.totalOverrideCount} всего`);
    }

    return { lines, emptyDescription: OVERRIDES_EMPTY_DESCRIPTION };
}

/** «12 упражнений» in a finished run's headline. */
export function describeExerciseCount(exerciseCount: number): string {
    return `${exerciseCount} ${pluralizeRussianCount(exerciseCount, "упражнение", "упражнения", "упражнений")}`;
}

/** `2026-08-18T09:14:00Z` → `18 августа, 09:14`. Runs are compared by day far more often than by year. */
const RUSSIAN_MONTH_NAMES_GENITIVE = [
    "января",
    "февраля",
    "марта",
    "апреля",
    "мая",
    "июня",
    "июля",
    "августа",
    "сентября",
    "октября",
    "ноября",
    "декабря",
] as const;

export function formatRunTimestamp(isoTimestamp: string): string {
    const timestamp = new Date(isoTimestamp);
    if (Number.isNaN(timestamp.getTime())) return "—";

    const day = timestamp.getDate();
    const monthName = RUSSIAN_MONTH_NAMES_GENITIVE[timestamp.getMonth()];
    const hours = String(timestamp.getHours()).padStart(2, "0");
    const minutes = String(timestamp.getMinutes()).padStart(2, "0");

    return `${day} ${monthName}, ${hours}:${minutes}`;
}
