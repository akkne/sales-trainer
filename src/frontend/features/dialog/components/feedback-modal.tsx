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
        <div
            onClick={onClose}
            style={{
                position: "fixed",
                inset: 0,
                zIndex: 50,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                padding: 16,
                background: "rgba(0, 0, 0, 0.45)",
                backdropFilter: "blur(2px)",
            }}
        >
            <div
                onClick={(event) => event.stopPropagation()}
                style={{
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: "var(--r-2xl)",
                    boxShadow: "var(--sh-3)",
                    maxWidth: 560,
                    width: "100%",
                    maxHeight: "82vh",
                    display: "flex",
                    flexDirection: "column",
                    overflow: "hidden",
                }}
            >
                {/* Header */}
                <div
                    style={{
                        padding: "20px 24px",
                        borderBottom: "1px solid var(--line)",
                        display: "flex",
                        alignItems: "flex-start",
                        justifyContent: "space-between",
                        gap: 12,
                    }}
                >
                    <div>
                        <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: feedback.xpEarned > 0 ? 10 : 0 }}>
                            <Icon name="book" size="md" style={{ color: "var(--accent)" }} />
                            <h2 style={{ fontSize: 21, fontWeight: 600, letterSpacing: -0.3, color: "var(--ink)", margin: 0 }}>
                                Обратная связь
                            </h2>
                        </div>
                        {feedback.xpEarned > 0 && (
                            <div
                                style={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: 8,
                                    padding: "6px 14px",
                                    borderRadius: 999,
                                    background: "var(--accent-soft)",
                                }}
                            >
                                <Icon name="bolt" size="sm" style={{ color: "var(--accent)" }} />
                                <span style={{ color: "var(--accent-ink)", fontWeight: 700, fontSize: 14 }}>
                                    +{feedback.xpEarned} XP
                                </span>
                                <span style={{ color: "var(--accent-ink)", fontSize: 13, opacity: 0.8 }}>заработано</span>
                            </div>
                        )}
                    </div>
                    <button
                        onClick={onClose}
                        aria-label="Закрыть"
                        style={{
                            padding: 8,
                            borderRadius: "50%",
                            background: "transparent",
                            border: "none",
                            cursor: "pointer",
                            color: "var(--ink-3)",
                            display: "flex",
                        }}
                    >
                        <Icon name="close" size="md" />
                    </button>
                </div>

                {/* Content */}
                <div style={{ padding: 24, overflowY: "auto", flex: 1 }}>
                    <div
                        className="[&_strong]:font-semibold [&_strong]:text-ink [&_p]:my-2 [&_ul]:my-2 [&_ul]:pl-5"
                        style={{ fontSize: 14, lineHeight: 1.65, color: "var(--ink-2)" }}
                        dangerouslySetInnerHTML={{ __html: feedback.summary }}
                    />

                    {!isExpanded && feedback.content !== feedback.summary && (
                        <button
                            onClick={() => setIsExpanded(true)}
                            style={{
                                marginTop: 16,
                                display: "flex",
                                alignItems: "center",
                                gap: 8,
                                background: "transparent",
                                border: "none",
                                cursor: "pointer",
                                padding: 0,
                                color: "var(--accent)",
                                fontSize: 14,
                                fontWeight: 500,
                            }}
                        >
                            <span>Подробнее</span>
                            <Icon name="chevron-down" size="sm" />
                        </button>
                    )}

                    {isExpanded && (
                        <>
                            <div style={{ margin: "16px 0", borderTop: "1px solid var(--line)" }} />
                            <div
                                className="[&_h3]:text-[15px] [&_h3]:font-semibold [&_h3]:text-ink [&_h3]:mt-4 [&_h3]:mb-2 [&_h3:first-child]:mt-0 [&_strong]:font-semibold [&_strong]:text-ink [&_em]:italic [&_p]:my-2 [&_ul]:my-2 [&_ul]:pl-5"
                                style={{ fontSize: 14, lineHeight: 1.65, color: "var(--ink-2)" }}
                                dangerouslySetInnerHTML={{ __html: feedback.content }}
                            />
                            <button
                                onClick={() => setIsExpanded(false)}
                                style={{
                                    marginTop: 16,
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 8,
                                    background: "transparent",
                                    border: "none",
                                    cursor: "pointer",
                                    padding: 0,
                                    color: "var(--ink-3)",
                                    fontSize: 14,
                                    fontWeight: 500,
                                }}
                            >
                                <span>Свернуть</span>
                                <Icon name="chevron-up" size="sm" />
                            </button>
                        </>
                    )}
                </div>

                {/* Footer */}
                <div style={{ padding: "20px 24px", borderTop: "1px solid var(--line)" }}>
                    <button
                        onClick={onClose}
                        style={{
                            width: "100%",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            gap: 10,
                            padding: "14px 24px",
                            borderRadius: 999,
                            background: "var(--accent)",
                            color: "var(--bg)",
                            border: "none",
                            cursor: "pointer",
                            fontSize: 15,
                            fontWeight: 600,
                            boxShadow: "var(--sh-2)",
                        }}
                    >
                        <Icon name="plus" size="sm" />
                        Новый диалог
                    </button>
                </div>
            </div>
        </div>
    );
}
