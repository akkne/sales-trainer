"use client";

import { useState, useEffect } from "react";
import { DialogFeedback } from "@/lib/hooks/useDialog";
import { Icon } from "@/components/ui/Icon";

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
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-inverse-surface/50 p-4">
            <div className="bg-surface-container-lowest rounded-2xl max-w-lg w-full max-h-[80vh] flex flex-col shadow-xl">
                {/* Header */}
                <div className="p-6 border-b border-outline-variant flex items-center justify-between">
                    <div>
                        <div className="flex items-center gap-2 mb-2">
                            <Icon name="book" size="md" className="text-primary" />
                            <h2 className="text-xl font-headline font-bold text-on-surface">
                                Обратная связь
                            </h2>
                        </div>
                        {feedback.xpEarned > 0 && (
                            <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-primary-container rounded-full">
                                <Icon name="bolt" size="sm" className="text-primary" />
                                <span className="text-primary font-bold">+{feedback.xpEarned} XP</span>
                                <span className="text-on-primary-container text-sm">заработано</span>
                            </div>
                        )}
                    </div>
                    <button
                        onClick={onClose}
                        className="p-2 rounded-full hover:bg-surface-container tonal-transition"
                        aria-label="Закрыть"
                    >
                        <Icon name="close" size="md" className="text-on-surface-variant" />
                    </button>
                </div>

                {/* Content */}
                <div className="p-6 overflow-y-auto flex-1">
                    <div
                        className="prose prose-sm max-w-none text-on-surface-variant [&>strong]:font-bold [&>strong]:text-on-surface"
                        dangerouslySetInnerHTML={{ __html: feedback.summary }}
                    />

                    {!isExpanded && feedback.content !== feedback.summary && (
                        <button
                            onClick={() => setIsExpanded(true)}
                            className="mt-4 flex items-center gap-2 text-primary hover:text-primary-dim font-medium tonal-transition"
                        >
                            <span>Подробнее</span>
                            <Icon name="chevron-down" size="sm" />
                        </button>
                    )}

                    {isExpanded && (
                        <>
                            <div className="my-4 border-t border-outline-variant" />
                            <div
                                className="prose prose-sm max-w-none text-on-surface-variant [&>h3]:text-base [&>h3]:font-bold [&>h3]:text-on-surface [&>h3]:mt-4 [&>h3]:mb-2 [&>h3:first-child]:mt-0 [&>ul]:my-2 [&>ul]:pl-5 [&>p]:my-2 [&_strong]:font-bold [&_strong]:text-on-surface [&_em]:italic [&_em]:text-on-surface-variant"
                                dangerouslySetInnerHTML={{ __html: feedback.content }}
                            />
                            <button
                                onClick={() => setIsExpanded(false)}
                                className="mt-4 flex items-center gap-2 text-on-surface-variant hover:text-on-surface font-medium tonal-transition"
                            >
                                <span>Свернуть</span>
                                <Icon name="chevron-up" size="sm" />
                            </button>
                        </>
                    )}
                </div>

                {/* Footer */}
                <div className="p-6 border-t border-outline-variant">
                    <button
                        onClick={onClose}
                        className="w-full py-3 bg-primary text-on-primary font-bold rounded-full shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 tonal-transition flex items-center justify-center gap-2"
                    >
                        <Icon name="plus" size="sm" />
                        Новый диалог
                    </button>
                </div>
            </div>
        </div>
    );
}
