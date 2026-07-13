"use client";

import Link from "next/link";
import { useState } from "react";
import { DialogSessionSummary } from "@/features/dialog/hooks/use-dialog";
import { DeleteConfirmModal } from "./delete-confirm-modal";
import { Icon } from "@/shared/components/icon";
import { TimingConstants } from "@/shared/constants/timing-constants";

interface SessionHistorySidebarProps {
    sessions: DialogSessionSummary[];
    currentSessionId: string | null;
    onSessionClick: (sessionId: string) => void;
    onNewChat: () => void;
    onDeleteSession: (sessionId: string) => void;
    onClose: () => void;
}

function formatSessionDate(dateString: string): string {
    const sessionDate = new Date(dateString);
    const now = new Date();
    const diffDays = Math.floor((now.getTime() - sessionDate.getTime()) / TimingConstants.oneDayMs);

    if (diffDays === 0) return "Сегодня";
    if (diffDays === 1) return "Вчера";
    if (diffDays < 7) return `${diffDays} дн назад`;

    return sessionDate.toLocaleDateString("en-GB", {
        day: "numeric",
        month: "short",
    });
}

function groupSessionsByDate(sessions: DialogSessionSummary[]): Map<string, DialogSessionSummary[]> {
    const grouped = new Map<string, DialogSessionSummary[]>();

    for (const session of sessions) {
        const dateKey = formatSessionDate(session.createdAt);
        if (!grouped.has(dateKey)) {
            grouped.set(dateKey, []);
        }
        grouped.get(dateKey)!.push(session);
    }

    return grouped;
}

export function SessionHistorySidebar({
    sessions,
    currentSessionId,
    onSessionClick,
    onNewChat,
    onDeleteSession,
    onClose,
}: SessionHistorySidebarProps) {
    const groupedSessions = groupSessionsByDate(sessions);
    const [sessionToDelete, setSessionToDelete] = useState<string | null>(null);

    const handleDeleteClick = (event: React.MouseEvent, sessionId: string) => {
        event.stopPropagation();
        setSessionToDelete(sessionId);
    };

    const handleConfirmDelete = () => {
        if (sessionToDelete) {
            onDeleteSession(sessionToDelete);
            setSessionToDelete(null);
        }
    };

    return (
        <>
            <aside className="dc-side">
                <div className="dc-side-top">
                    <button className="btn btn-dark btn-sm" onClick={onNewChat}>
                        <Icon name="plus" size="sm" />
                        Новый диалог
                    </button>
                    <button className="icon-btn" onClick={onClose} aria-label="Закрыть" style={{ flex: "none" }}>
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="dc-history">
                    {sessions.length === 0 ? (
                        <div className="empty" style={{ padding: "40px 16px" }}>
                            <div className="ic" style={{ width: 48, height: 48, marginBottom: 12 }}>
                                <Icon name="message" size="lg" />
                            </div>
                            <p className="small">Истории диалогов пока нет</p>
                        </div>
                    ) : (
                        Array.from(groupedSessions.entries()).map(([dateLabel, dateSessions]) => (
                            <div key={dateLabel}>
                                <span className="eyebrow muted" style={{ display: "block", margin: "14px 8px 8px" }}>
                                    {dateLabel}
                                </span>
                                {dateSessions.map((session) => (
                                    <button
                                        key={session.id}
                                        onClick={() => onSessionClick(session.id)}
                                        className={"dc-conv group" + (currentSessionId === session.id ? " active" : "")}
                                    >
                                        <div className="row between gap-2">
                                            <span className="dc-conv-title" style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                                                {session.modeTitle}
                                            </span>
                                            <span className="row gap-1" style={{ flex: "none" }}>
                                                {session.status === "completed" && session.xpEarned > 0 && (
                                                    <span className="badge" style={{ background: "var(--primary-soft)", color: "var(--primary)" }}>
                                                        +{session.xpEarned}
                                                    </span>
                                                )}
                                                <span
                                                    onClick={(e) => handleDeleteClick(e, session.id)}
                                                    role="button"
                                                    aria-label="Удалить чат"
                                                    style={{ color: "var(--ink-4)", display: "inline-flex", padding: 2 }}
                                                >
                                                    <Icon name="delete" size="sm" />
                                                </span>
                                            </span>
                                        </div>
                                        <div className="row gap-2 small" style={{ marginTop: 4 }}>
                                            <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                                                {session.bundleTitle}
                                            </span>
                                            <span style={{ color: "var(--ink-4)" }}>·</span>
                                            <span className="row gap-1" style={{ flex: "none" }}>
                                                <Icon name="message" size={13} />
                                                {session.messageCount}
                                            </span>
                                        </div>
                                    </button>
                                ))}
                            </div>
                        ))
                    )}
                </div>

                <div style={{ padding: 12, borderTop: "1px solid var(--line)" }}>
                    <Link href="/dialog" className="back-link" style={{ width: "100%", justifyContent: "center" }}>
                        <Icon name="arrow-left" size="sm" />
                        Назад к навыкам
                    </Link>
                </div>
            </aside>

            {sessionToDelete && (
                <DeleteConfirmModal
                    onConfirm={handleConfirmDelete}
                    onCancel={() => setSessionToDelete(null)}
                />
            )}
        </>
    );
}
