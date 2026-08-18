"use client";

import Link from "next/link";
import { Icon, type IconName } from "@/shared/components/icon";
import {
    daysUntilDeadline,
    describeCompletionRule,
    useActiveAssignments,
    type ActiveAssignment,
    type ActiveAssignmentItem,
} from "@/features/assignments/hooks/use-assignments";

/**
 * Phase 40.23. The roadmap's "активное задание — первым экраном у менеджера, пока не выполнено".
 *
 * Deliberately a strip above the learning path rather than a screen of its own. A manager with no
 * assignments — which is most of them, most of the time — must land on their skill tree exactly as
 * before, so this renders nothing at all while loading, on error, and when the list is empty. An
 * assignment surface that could replace the home screen would make one РОП's habit decide whether
 * the product has a home screen.
 */
export function ActiveAssignmentCard() {
    const { data: assignments } = useActiveAssignments();

    if (!assignments || assignments.length === 0) {
        return null;
    }

    return (
        <section className="assignment-strip" aria-label="Активные задания">
            {assignments.map((assignment) => (
                <AssignmentCard key={assignment.id} assignment={assignment} />
            ))}
        </section>
    );
}

function AssignmentCard({ assignment }: { assignment: ActiveAssignment }) {
    const daysLeft = daysUntilDeadline(assignment.deadline);
    const bar = describeCompletionRule(assignment.completionRule);
    const isUnderThreshold = assignment.status === "failed_threshold";

    return (
        <article className={`assignment-card${isUnderThreshold ? " under-threshold" : ""}`}>
            <header className="assignment-card-head">
                <span className="assignment-card-eyebrow">Активное задание</span>
                <DeadlineChip daysLeft={daysLeft} deadline={assignment.deadline} />
            </header>

            <h2 className="assignment-card-title">{assignment.title}</h2>

            {assignment.goal ? <p className="assignment-card-goal">{assignment.goal}</p> : null}

            {bar ? (
                <p className="assignment-card-bar">
                    <Icon name="target" size={14} />
                    <span>Чтобы засчиталось: {bar}</span>
                </p>
            ) : null}

            {/*
              The attempt line is only shown once there is something to say. "Попыток: 0" next to a
              brand-new assignment reads as an accusation, and 40.22 made a point of separating
              "has not started" from "tried and did not reach the bar" — so the screen should not
              blur them either.
            */}
            {assignment.attemptCount > 0 ? (
                <p className="assignment-card-progress">
                    {isUnderThreshold
                        ? "Планка пока не взята — попробуй ещё раз."
                        : "Работа идёт."}{" "}
                    Попыток: {assignment.attemptCount}
                    {assignment.bestScore !== null ? ` · лучший результат: ${assignment.bestScore}` : ""}
                </p>
            ) : null}

            {assignment.content.length > 0 ? (
                <ul className="assignment-card-items">
                    {assignment.content.map((item) => (
                        <li key={`${item.kind}:${item.reference}`}>
                            <AssignmentItemLink item={item} />
                        </li>
                    ))}
                </ul>
            ) : null}
        </article>
    );
}

function DeadlineChip({ daysLeft, deadline }: { daysLeft: number | null; deadline: string | null }) {
    if (deadline === null || daysLeft === null) {
        return <span className="assignment-chip neutral">Без срока</span>;
    }

    // Past-due is reachable here: 40.23 does not close an assignment when its deadline passes, and
    // saying so plainly is better than a chip that quietly reads "0 дней" forever.
    if (daysLeft < 0) {
        return <span className="assignment-chip late">Срок прошёл</span>;
    }

    const tone = daysLeft <= 1 ? "urgent" : "neutral";
    const label = daysLeft === 0 ? "Сегодня" : `Осталось ${daysLeft} ${pluralDays(daysLeft)}`;

    return <span className={`assignment-chip ${tone}`}>{label}</span>;
}

const ITEM_PRESENTATION: Record<
    ActiveAssignmentItem["kind"],
    { icon: IconName; fallbackLabel: string; href: (item: ActiveAssignmentItem) => string | null }
> = {
    lesson_version: {
        icon: "book",
        fallbackLabel: "Упражнения",
        href: (item) => (item.lessonId ? `/session/${item.lessonId}` : null),
    },
    dialog_scenario: {
        icon: "message",
        fallbackLabel: "Практический разговор",
        // The reference is an ai-service mode key, not a route. The practice screen is where a
        // conversation is started, and starting one there is what makes learning-service's
        // persona injection happen — so the link points at practice rather than at a deep link
        // this client would have to assemble from a key it cannot resolve.
        href: () => "/dialog",
    },
    reference_material: {
        icon: "layers",
        fallbackLabel: "Теория",
        href: (item) => `/reference/${item.reference}`,
    },
};

function AssignmentItemLink({ item }: { item: ActiveAssignmentItem }) {
    const presentation = ITEM_PRESENTATION[item.kind];
    if (!presentation) {
        return null;
    }

    const label = item.title ?? presentation.fallbackLabel;
    const href = presentation.href(item);

    // Content archived after the assignment was issued still appears, greyed and unclickable.
    // Dropping it silently would leave somebody told to do four things looking at three, with no
    // way to ask about the fourth.
    if (!href) {
        return (
            <span className="assignment-item unavailable">
                <Icon name={presentation.icon} size={14} />
                <span>{label}</span>
            </span>
        );
    }

    return (
        <Link href={href} className="assignment-item">
            <Icon name={presentation.icon} size={14} />
            <span>{label}</span>
            <Icon name="chevron-right" size={13} />
        </Link>
    );
}

function pluralDays(count: number): string {
    const lastTwo = count % 100;
    const last = count % 10;
    if (lastTwo >= 11 && lastTwo <= 14) return "дней";
    if (last === 1) return "день";
    if (last >= 2 && last <= 4) return "дня";
    return "дней";
}
