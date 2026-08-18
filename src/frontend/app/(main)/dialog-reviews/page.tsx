"use client";

import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { Skeleton, ErrorState } from "@/shared/components";
import {
    describeReviewStatus,
    useAcknowledgeCoachingNote,
    useDialogReviews,
    type DialogReviewNote,
} from "@/features/dialog-reviews/hooks/use-dialog-reviews";

/**
 * Phase 40.25. The manager's half of docs/TENANCY/ASSIGNMENTS.md §4.1: what the РОП said about
 * their calls, and what became of the scores they disputed.
 *
 * <p>
 * One list for both directions, matching the API. The alternative — a "feedback" tab and a
 * "disputes" tab — splits a conversation between two people across two screens, and the half a
 * manager cares about on any given day is whichever one has something new in it.
 * </p>
 *
 * <p>
 * This is the destination of both notification action routes, so a person arriving from an email
 * lands on the row they were told about rather than on a dashboard they then have to search.
 * </p>
 */
export default function DialogReviewsPage() {
    const { data: notes, isLoading, error, refetch } = useDialogReviews();

    return (
        <div className="page">
            <div className="container">
                <div className="col" style={{ gap: 4, paddingBottom: 20 }}>
                    <h1 className="h2">Разбор разговоров</h1>
                    <p style={{ color: "var(--ink-3)", fontSize: 14 }}>
                        Комментарии руководителя к вашим звонкам и судьба оспоренных оценок
                    </p>
                </div>

                {isLoading && (
                    <div className="col" style={{ gap: 10 }}>
                        {[1, 2, 3].map((index) => (
                            <Skeleton key={index} height={120} rounded={14} />
                        ))}
                    </div>
                )}

                {error && (
                    <ErrorState
                        title="Не удалось загрузить"
                        message={error.message}
                        onRetry={() => refetch()}
                    />
                )}

                {notes && notes.length === 0 && (
                    <div className="empty" style={{ paddingTop: 60 }}>
                        <div className="ic">
                            <Icon name="forum" size="lg" />
                        </div>
                        <h2 className="h4" style={{ marginBottom: 8 }}>
                            Пока тихо
                        </h2>
                        <p style={{ color: "var(--ink-3)", fontSize: 14 }}>
                            Здесь появятся комментарии руководителя к вашим разговорам и ответы на
                            оспоренные оценки.
                        </p>
                    </div>
                )}

                {notes && notes.length > 0 && (
                    <div className="col" style={{ gap: 12 }}>
                        {notes.map((note) => (
                            <ReviewCard key={note.id} note={note} />
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}

function ReviewCard({ note }: { note: DialogReviewNote }) {
    const acknowledge = useAcknowledgeCoachingNote();

    const isCoachingNote = note.kind === "coaching_note";
    const statusLabel = describeReviewStatus(note);

    return (
        <article className="card" style={{ padding: 16 }}>
            <div className="row gap-3" style={{ alignItems: "center", marginBottom: 10 }}>
                <span className="itile primary" style={{ width: 36, height: 36 }}>
                    <Icon name={isCoachingNote ? "forum" : "book"} size="md" />
                </span>
                <div className="col" style={{ gap: 2, flex: 1 }}>
                    <span style={{ fontWeight: 600, fontSize: 15 }}>
                        {isCoachingNote ? "Комментарий руководителя" : "Вы оспорили оценку"}
                    </span>
                    <span style={{ fontSize: 13, color: "var(--ink-3)" }}>
                        {isCoachingNote && note.authorDisplayName
                            ? note.authorDisplayName
                            : note.dialogModeKey}
                        {note.disputedScore !== null && ` · оценка ИИ ${note.disputedScore} из 100`}
                    </span>
                </div>
                {statusLabel && (
                    <span
                        className="chip"
                        style={{ fontSize: 12, color: "var(--ink-3)", whiteSpace: "nowrap" }}
                    >
                        {statusLabel}
                    </span>
                )}
            </div>

            {/* The quoted lines come first. They are the whole reason a coaching note exists, and a
                card that opens with a verdict makes the reader scroll for the thing being judged. */}
            {note.quotedText && (
                <blockquote
                    style={{
                        margin: "0 0 10px",
                        padding: "10px 12px",
                        borderLeft: "3px solid var(--line)",
                        background: "var(--surface-2)",
                        borderRadius: 8,
                        fontSize: 14,
                        whiteSpace: "pre-wrap",
                    }}
                >
                    {note.quotedText}
                </blockquote>
            )}

            <p style={{ fontSize: 14, margin: 0, whiteSpace: "pre-wrap" }}>{note.comment}</p>

            {note.resolution && (
                <p
                    style={{
                        fontSize: 14,
                        marginTop: 10,
                        marginBottom: 0,
                        color: "var(--ink-2)",
                        whiteSpace: "pre-wrap",
                    }}
                >
                    <strong>Ответ руководителя: </strong>
                    {note.resolution}
                </p>
            )}

            {isCoachingNote && note.status === "open" && (
                <div className="row" style={{ justifyContent: "flex-end", marginTop: 12 }}>
                    <Button
                        variant="ghost"
                        onClick={() => acknowledge.mutate(note.id)}
                        disabled={acknowledge.isPending}
                    >
                        Прочитано
                    </Button>
                </div>
            )}
        </article>
    );
}
