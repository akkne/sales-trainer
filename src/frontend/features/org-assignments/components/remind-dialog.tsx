"use client";

import { Button } from "@/shared/components/button";
import { Modal } from "@/shared/components/modal";
import { REMINDER_SCOPE_LABELS } from "@/features/org-assignments/constants/assignment-dictionary";
import type {
    AssignmentDashboardRow,
    AssignmentFunnel,
    AssignmentReminderScope,
} from "@/features/org-assignments/types/assignment";
import { countReminderRecipients } from "@/features/org-assignments/utils/funnel";
import { pluralizePeople } from "@/features/org-assignments/utils/audience-rule";

interface RemindDialogProps {
    open: boolean;
    scope: AssignmentReminderScope;
    onScopeChange: (scope: AssignmentReminderScope) => void;
    funnel: AssignmentFunnel;
    rows: AssignmentDashboardRow[];
    onClose: () => void;
    onConfirm: () => void;
    isPending: boolean;
    error: string | null;
}

const SCOPES: AssignmentReminderScope[] = ["not_started", "unfinished"];

export function selectReminderRecipients(
    rows: AssignmentDashboardRow[],
    scope: AssignmentReminderScope
): AssignmentDashboardRow[] {
    if (scope === "not_started") return rows.filter((row) => row.status === "not_started");

    return rows.filter((row) => row.status !== "completed");
}

/** «Без имени · 3f2a1b9c» — a replica that has not caught up is said so, never invented. */
export function describeRecipientName(row: AssignmentDashboardRow): string {
    return row.displayName ?? `Без имени · ${row.userId.slice(0, 8)}`;
}

/**
 * The button the deadline digest sends the РОП here to press.
 *
 * It names its recipients, because the notification named five people and a nudge that reaches a
 * different set is worse than none. Nothing is sent by opening the dialog — an URL that messages the
 * team on load is an URL a mail scanner fires.
 */
export function RemindDialog({
    open,
    scope,
    onScopeChange,
    funnel,
    rows,
    onClose,
    onConfirm,
    isPending,
    error,
}: RemindDialogProps) {
    const recipients = selectReminderRecipients(rows, scope);
    const funnelCount = countReminderRecipients(funnel, scope);

    return (
        <Modal
            open={open}
            onClose={onClose}
            title="Напомнить о задании"
            size="md"
            footer={
                <>
                    <Button variant="ghost" onClick={onClose} disabled={isPending}>
                        Отмена
                    </Button>
                    <Button variant="primary" onClick={onConfirm} loading={isPending}>
                        Отправить напоминание
                    </Button>
                </>
            }
        >
            <div className="flex flex-col gap-4">
                <div className="flex flex-col gap-2">
                    {SCOPES.map((candidateScope) => (
                        <label
                            key={candidateScope}
                            className="flex items-center gap-2 text-sm text-ink-2"
                        >
                            <input
                                type="radio"
                                name="reminder-scope"
                                checked={scope === candidateScope}
                                disabled={isPending}
                                onChange={() => onScopeChange(candidateScope)}
                            />
                            Напомнить {REMINDER_SCOPE_LABELS[candidateScope]} (
                            {countReminderRecipients(funnel, candidateScope)})
                        </label>
                    ))}
                </div>

                <div>
                    <p className="mb-2 text-xs text-ink-3">
                        Получат напоминание: {funnelCount} {pluralizePeople(funnelCount)}
                    </p>
                    {recipients.length === 0 ? (
                        <p className="text-sm text-ink-3">
                            Никого напоминать не нужно — в этой группе никого нет.
                        </p>
                    ) : (
                        <ul className="flex max-h-52 flex-col gap-1 overflow-y-auto text-sm text-ink-2">
                            {recipients.map((row) => (
                                <li key={row.userId}>
                                    {describeRecipientName(row)}
                                    {row.isActiveMember === false && (
                                        <span className="ml-2 text-xs text-ink-4">
                                            уже не работает в компании — напоминание не придёт
                                        </span>
                                    )}
                                </li>
                            ))}
                        </ul>
                    )}
                </div>

                <p className="text-xs text-ink-4">
                    Если кто-то уже получал напоминание в течение часа, повторное ему не придёт.
                </p>

                {error && (
                    <p className="text-sm" style={{ color: "var(--heart)" }} role="alert">
                        {error}
                    </p>
                )}
            </div>
        </Modal>
    );
}
