"use client";

import { useState } from "react";
import { useDialogSession } from "@/features/dialog/hooks/use-dialog";
import { FeedbackModal } from "@/features/dialog/components/feedback-modal";
import type { PracticeCall } from "@/features/companies/hooks/use-practice-calls";
import { relativeTimeRu } from "@/features/companies/lib/format";

interface TimelinePracticeItemProps {
    practiceCall: PracticeCall;
}

export function TimelinePracticeItem({ practiceCall }: TimelinePracticeItemProps) {
    const [isExpanded, setExpanded] = useState(false);
    const [isDismissed, setDismissed] = useState(false);
    const { data: session } = useDialogSession(isExpanded ? practiceCall.dialogSessionId : null);

    const title = practiceCall.goal || "Тренировочный звонок";
    const messageCount = session?.messages.length ?? 0;
    const canOpenDetails = !isExpanded || !!session?.feedback;
    const shouldShowModal = isExpanded && !isDismissed && !!session?.feedback;

    return (
        <div className="co-tl-item practice">
            <div className="co-tl-node" aria-hidden="true" />
            <div className="co-tl-card">
                <div className="co-tl-top">
                    <span className="pill-inprogress">Тренировка</span>
                    <span className="co-tl-time">{relativeTimeRu(practiceCall.createdAt)}</span>
                </div>
                <p className="co-tl-title">{title}</p>
                {isExpanded && messageCount > 0 && (
                    <p className="co-tl-meta">Голосовой звонок · {messageCount} реплик</p>
                )}
                {canOpenDetails && (
                    <button className="btn-link" style={{ marginTop: 6 }} onClick={() => setExpanded(true)}>
                        Разбор →
                    </button>
                )}
            </div>

            {shouldShowModal && session?.feedback && (
                <FeedbackModal feedback={session.feedback} onClose={() => setDismissed(true)} />
            )}
        </div>
    );
}
