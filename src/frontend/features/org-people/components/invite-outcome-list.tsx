"use client";

import { Card, CardContent } from "@/shared/components/card";
import { Icon } from "@/shared/components/icon";
import {
    describeInviteRejection,
    describeOrganizationRole,
} from "@/features/org-people/constants/people-dictionary";
import { formatShortRussianDate } from "@/features/org-people/utils/format-people";
import {
    describeInviteOutcome,
    type InviteOutcomeLine,
    type InviteOutcomeSummary,
} from "@/features/org-people/utils/invite-outcome";

export const INVITE_TOKEN_WITHHELD_NOTE =
    "Ссылка-приглашение ушла на почту. На экране её нет и не будет: это одноразовый токен, который открывает доступ в вашу компанию любому, кто его увидит.";

interface InviteOutcomeListProps {
    lines: InviteOutcomeLine[];
    summary: InviteOutcomeSummary;
    role: string;
}

/// The answer to a bulk invite, rendered as the one list that was submitted rather than as two.
///
/// A request where three of forty addresses were refused is the ordinary outcome, not an error and
/// not a success — so neither half is hidden, collapsed behind «подробнее», or styled as a failure
/// of the whole operation.
export function InviteOutcomeList({ lines, summary, role }: InviteOutcomeListProps) {
    if (lines.length === 0) return null;

    return (
        <Card className="mb-6">
            <CardContent style={{ marginTop: 0 }}>
                <h2 className="mb-1 text-xs font-medium uppercase tracking-wide text-ink-3">
                    Отправлено только что
                </h2>
                <p className="mb-4 text-sm text-ink-2">{describeInviteOutcome(summary)}</p>

                <ul className="flex flex-col gap-2">
                    {lines.map((line, lineIndex) => (
                        <li
                            key={`${line.email}-${line.rejectionReason ?? "created"}-${lineIndex}`}
                            className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm"
                        >
                            {line.wasCreated ? (
                                <Icon
                                    name="check"
                                    size="sm"
                                    className="shrink-0"
                                    style={{ color: "var(--success)" }}
                                />
                            ) : (
                                <Icon
                                    name="close"
                                    size="sm"
                                    className="shrink-0"
                                    style={{ color: "var(--bad)" }}
                                />
                            )}
                            <span className="text-ink">{line.email}</span>
                            {line.wasCreated ? (
                                <>
                                    <span className="text-ink-3">
                                        {describeOrganizationRole(role)}
                                    </span>
                                    <span className="text-ink-3">
                                        действует до {formatShortRussianDate(line.expiresAt)}
                                    </span>
                                </>
                            ) : (
                                <span className="text-ink-3">
                                    {describeInviteRejection(line.rejectionReason ?? "")}
                                </span>
                            )}
                        </li>
                    ))}
                </ul>

                <p className="mt-4 text-sm text-ink-3">{INVITE_TOKEN_WITHHELD_NOTE}</p>
            </CardContent>
        </Card>
    );
}
