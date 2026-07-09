"use client";

import { useState } from "react";
import { useDialogSession } from "@/features/dialog/hooks/use-dialog";
import { FeedbackModal } from "@/features/dialog/components/feedback-modal";
import type { PracticeCall } from "@/features/companies/hooks/use-practice-calls";
import { relativeTimeRu } from "@/features/companies/lib/format";

interface TimelinePracticeItemProps {
    practiceCall: PracticeCall;
}

/** Practice-call timeline entry — violet node (§3.4a of the design spec). */
export function TimelinePracticeItem({ practiceCall }: TimelinePracticeItemProps) {
    const [showFeedback, setShowFeedback] = useState(false);
    const { data: session } = useDialogSession(practiceCall.dialogSessionId);

    const title = practiceCall.goal || "Тренировочный звонок";
    const messageCount = session?.messages.length ?? 0;

    return (
        <div className="co-tl-item practice">
            <div className="co-tl-node" aria-hidden="true" />
            <div className="co-tl-card">
                <div className="co-tl-top">
                    <span className="pill-inprogress">Тренировка</span>
                    <span className="co-tl-time">{relativeTimeRu(practiceCall.createdAt)}</span>
                </div>
                <p className="co-tl-title">{title}</p>
                {messageCount > 0 && (
                    <p className="co-tl-meta">Голосовой звонок · {messageCount} реплик</p>
                )}
                {session?.feedback && (
                    <button className="btn-link" style={{ marginTop: 6 }} onClick={() => setShowFeedback(true)}>
                        Разбор →
                    </button>
                )}
            </div>

            {showFeedback && session?.feedback && (
                <FeedbackModal feedback={session.feedback} onClose={() => setShowFeedback(false)} />
            )}
        </div>
    );
}
