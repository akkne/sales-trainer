"use client";

import { useState, useEffect } from "react";
import { DialogFeedback } from "@/features/dialog/hooks/use-dialog";
import { Icon } from "@/shared/components/icon";
import { useDisputeScore } from "@/features/dialog-reviews/hooks/use-dialog-reviews";

interface FeedbackModalProps {
    feedback: DialogFeedback;
    onClose: () => void;
    /**
     * Phase 40.25. The conversation this grade belongs to. When present, the modal offers to
     * dispute the score (docs/TENANCY/ASSIGNMENTS.md §4.1).
     *
     * Optional because not every caller of this modal has one to hand, and a missing session id
     * has to degrade to "no dispute button" rather than to a button that fails when pressed.
     */
    sessionId?: string | null;
}

function scoreColor(score: number): { soft: string; ink: string; label: string } {
    if (score <= 2) return { soft: "var(--bad-soft)", ink: "var(--bad)", label: "Провал" };
    if (score <= 4) return { soft: "var(--bad-soft)", ink: "var(--bad)", label: "Слабо" };
    if (score <= 6) return { soft: "var(--warn-soft)", ink: "var(--warn)", label: "Нормально" };
    if (score <= 8) return { soft: "var(--success-soft)", ink: "var(--success)", label: "Хорошо" };
    return { soft: "var(--success-soft)", ink: "var(--success)", label: "Отлично" };
}

export function FeedbackModal({ feedback, onClose, sessionId }: FeedbackModalProps) {
    const [isExpanded, setIsExpanded] = useState(false);

    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") {
                onClose();
            }
        };

        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [onClose]);

    return (
        <div className="modal-overlay" onClick={onClose}>
            <div className="modal fade-up" onClick={(event) => event.stopPropagation()}>
                <div className="modal-head">
                    <div className="row gap-3">
                        <span className="itile primary" style={{ width: 40, height: 40 }}>
                            <Icon name="book" size="md" />
                        </span>
                        <h2 className="h3">Обратная связь</h2>
                    </div>
                    <button className="icon-btn" onClick={onClose} aria-label="Закрыть">
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="modal-body">
                    {typeof feedback.score === "number" && (
                        <div
                            className="row gap-3"
                            style={{ alignItems: "center", marginBottom: 16 }}
                        >
                            <span
                                className="itile"
                                style={{
                                    width: 56,
                                    height: 56,
                                    background: scoreColor(feedback.score).soft,
                                    color: scoreColor(feedback.score).ink,
                                    fontWeight: 700,
                                    fontSize: 18,
                                    borderRadius: 14,
                                }}
                            >
                                {feedback.score}<span style={{ fontSize: 12, opacity: 0.7 }}>/10</span>
                            </span>
                            <div className="col" style={{ gap: 2 }}>
                                <span style={{ fontWeight: 600, fontSize: 15 }}>
                                    {scoreColor(feedback.score).label}
                                </span>
                                <span style={{ fontSize: 13, color: "var(--ink-3)" }}>
                                    Оценка за диалог
                                </span>
                            </div>
                        </div>
                    )}

                    {/* No XP for a call: the analysis is the reward. `xpEarned` is still on the
                        contract (the backend scores the session) but is never shown. */}

                    <div
                        className="body [&_strong]:font-semibold [&_p]:my-2 [&_ul]:my-2 [&_ul]:pl-5"
                        style={{ fontSize: 14, lineHeight: 1.65 }}
                        dangerouslySetInnerHTML={{ __html: feedback.summary }}
                    />

                    {!isExpanded && feedback.content !== feedback.summary && (
                        <button className="more-btn" onClick={() => setIsExpanded(true)}>
                            Ещё <Icon name="chevron-down" size={18} />
                        </button>
                    )}

                    {isExpanded && (
                        <>
                            <div className="hr" style={{ margin: "16px 0" }} />
                            <div
                                className="body [&_h3]:text-[15px] [&_h3]:font-semibold [&_h3]:mt-4 [&_h3]:mb-2 [&_h3:first-child]:mt-0 [&_strong]:font-semibold [&_em]:italic [&_p]:my-2 [&_ul]:my-2 [&_ul]:pl-5"
                                style={{ fontSize: 14, lineHeight: 1.65 }}
                                dangerouslySetInnerHTML={{ __html: feedback.content }}
                            />
                            <button className="more-btn" style={{ color: "var(--ink-3)" }} onClick={() => setIsExpanded(false)}>
                                Свернуть <Icon name="chevron-up" size={18} />
                            </button>
                        </>
                    )}
                </div>

                {sessionId && <DisputeSection sessionId={sessionId} />}

                <div className="modal-foot">
                    <button className="btn btn-primary btn-block" onClick={onClose}>
                        <Icon name="plus" size="sm" />
                        Новый диалог
                    </button>
                </div>
            </div>
        </div>
    );
}

/**
 * Phase 40.25. «Менеджер оспаривает оценку ИИ» at the moment the grade is on the screen.
 *
 * Collapsed behind one link on purpose. The mechanism has to exist — without it the first genuinely
 * disputed score costs the product the team's trust in every number it shows — but a form that is
 * open by default invites a complaint about every grade, and a queue nobody can read is the same as
 * no mechanism at all.
 */
function DisputeSection({ sessionId }: { sessionId: string }) {
    const [isOpen, setIsOpen] = useState(false);
    const [comment, setComment] = useState("");
    const dispute = useDisputeScore();

    if (dispute.isSuccess) {
        return (
            <div style={{ padding: "0 20px 4px", fontSize: 13, color: "var(--ink-3)" }}>
                Оценка отправлена руководителю на рассмотрение. Ответ придёт в уведомлениях.
            </div>
        );
    }

    if (!isOpen) {
        return (
            <div style={{ padding: "0 20px 4px" }}>
                <button
                    className="more-btn"
                    style={{ color: "var(--ink-3)" }}
                    onClick={() => setIsOpen(true)}
                >
                    Не согласны с оценкой?
                </button>
            </div>
        );
    }

    return (
        <div className="col" style={{ gap: 8, padding: "0 20px 4px" }}>
            <textarea
                className="field"
                rows={3}
                placeholder="Что именно оценено неверно и почему"
                value={comment}
                onChange={(event) => setComment(event.target.value)}
            />
            {dispute.isError && (
                <span style={{ fontSize: 13, color: "var(--bad)" }}>{dispute.error.message}</span>
            )}
            <div className="row gap-2" style={{ justifyContent: "flex-end" }}>
                <button className="btn btn-ghost" onClick={() => setIsOpen(false)}>
                    Отмена
                </button>
                <button
                    className="btn btn-primary"
                    disabled={comment.trim().length === 0 || dispute.isPending}
                    onClick={() => dispute.mutate({ sessionId, comment: comment.trim() })}
                >
                    Оспорить
                </button>
            </div>
        </div>
    );
}
