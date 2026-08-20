"use client";

import Link from "next/link";
import { Chip, type ChipTone } from "@/shared/components/chip";
import { Icon } from "@/shared/components/icon";
import { stripFeedbackHtml } from "@/shared/components/feedback-html";
import type { DialogSessionSummary } from "@/features/org-dialogs/hooks/use-dialog-sessions";
import {
    formatDialogMoment,
    formatDialogMomentInFull,
} from "@/features/org-dialogs/lib/format-dialog-moment";
import {
    formatPanelScore,
    resolvePanelScoreTone,
    toPanelScore,
} from "@/features/org-dialogs/lib/dialog-score";
import { resolveMemberLabel } from "@/features/org-dialogs/lib/team-member-labels";

interface DialogSessionListProps {
    sessions: DialogSessionSummary[];
    memberNamesByUserId: Map<string, string>;
}

const UNKNOWN_SCENARIO_LABEL = "Сценарий недоступен";

/** Only three of `ChipTone` can come out of `resolvePanelScoreTone`; the rest never reach here. */
const SCORE_TEXT_COLORS: Partial<Record<ChipTone, string>> = {
    bad: "var(--bad)",
    warn: "var(--warn)",
    good: "var(--ink)",
};

/**
 * The list of O5. A hand-written list rather than the shared `DataTable` for one reason: the
 * second line of every row is `feedbackSummary` across the full width, and that is what lets a РОП
 * pick three conversations to open instead of opening ten. A table cell cannot span a row, and
 * squeezing the summary into one column would have made the column the row.
 *
 * The row's own link covers the row as an overlay so that the «по заданию» chip can be a second,
 * different link — nesting one anchor inside another is not markup a browser agrees to render.
 */
export function DialogSessionList({ sessions, memberNamesByUserId }: DialogSessionListProps) {
    return (
        <ul className="flex flex-col">
            {sessions.map((session) => {
                const panelScore = toPanelScore(session.score);
                const scoreColor = SCORE_TEXT_COLORS[resolvePanelScoreTone(panelScore)] ?? "var(--ink)";

                return (
                    <li
                        key={session.id}
                        className="relative flex flex-col gap-1 py-3 px-1 hover:bg-bg-2 transition-colors"
                        style={{ borderTop: "1px solid var(--line)" }}
                    >
                        <Link
                            href={`/org/dialogs/${encodeURIComponent(session.id)}`}
                            className="absolute inset-0"
                            aria-label={`Открыть разговор от ${formatDialogMomentInFull(session.createdAt)}`}
                        />

                        <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
                            <span
                                className="tnum text-base font-semibold"
                                style={{
                                    fontFamily: "var(--font-mono)",
                                    color: scoreColor,
                                    minWidth: "2.5rem",
                                }}
                            >
                                {formatPanelScore(panelScore)}
                            </span>

                            <span className="font-medium text-ink">
                                {resolveMemberLabel(session.userId, memberNamesByUserId)}
                            </span>

                            <span className="text-sm text-ink-2">
                                {session.modeTitle ?? UNKNOWN_SCENARIO_LABEL}
                            </span>

                            <span className="tnum text-sm text-ink-3">
                                {session.messageCount} реплик
                            </span>

                            <span
                                className="text-sm text-ink-3 ml-auto"
                                title={formatDialogMomentInFull(session.createdAt)}
                            >
                                {formatDialogMoment(session.createdAt)}
                            </span>

                            <Icon name="chevron-right" size="sm" className="text-ink-4" />
                        </div>

                        {session.feedbackSummary && (
                            <p
                                className="text-sm text-ink-3"
                                style={{
                                    display: "-webkit-box",
                                    WebkitLineClamp: 2,
                                    WebkitBoxOrient: "vertical",
                                    overflow: "hidden",
                                }}
                            >
                                {stripFeedbackHtml(session.feedbackSummary)}
                            </p>
                        )}

                        {session.assignmentId && (
                            <Link
                                href={`/org/assignments/${session.assignmentId}`}
                                className="relative w-fit"
                            >
                                <Chip tone="indigo" size="sm">
                                    по заданию
                                </Chip>
                            </Link>
                        )}
                    </li>
                );
            })}
        </ul>
    );
}
