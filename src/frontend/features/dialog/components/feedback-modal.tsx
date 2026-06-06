"use client";

import { useState, useEffect } from "react";
import { DialogFeedback } from "@/features/dialog/hooks/use-dialog";
import { Icon } from "@/shared/components/icon";

interface FeedbackModalProps {
    feedback: DialogFeedback;
    onClose: () => void;
}

export function FeedbackModal({ feedback, onClose }: FeedbackModalProps) {
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
                    {feedback.xpEarned > 0 && (
                        <span
                            className="badge"
                            style={{ background: "var(--primary-soft)", color: "var(--primary)", fontSize: 13, padding: "6px 12px", marginBottom: 16 }}
                        >
                            <Icon name="bolt" size={15} />
                            +{feedback.xpEarned} XP заработано
                        </span>
                    )}

                    <div
                        className="body [&_strong]:font-semibold [&_p]:my-2 [&_ul]:my-2 [&_ul]:pl-5"
                        style={{ fontSize: 14, lineHeight: 1.65 }}
                        dangerouslySetInnerHTML={{ __html: feedback.summary }}
                    />

                    {!isExpanded && feedback.content !== feedback.summary && (
                        <button className="more-btn" onClick={() => setIsExpanded(true)}>
                            Подробнее <Icon name="chevron-down" size={18} />
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
