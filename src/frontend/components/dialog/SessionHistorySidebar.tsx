"use client";

import Link from "next/link";
import { useState } from "react";
import { DialogSessionSummary } from "@/lib/hooks/useDialog";
import { DeleteConfirmModal } from "./DeleteConfirmModal";
import { Icon } from "@/components/ui/Icon";

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
    const diffDays = Math.floor((now.getTime() - sessionDate.getTime()) / (1000 * 60 * 60 * 24));

    if (diffDays === 0) return "Сегодня";
    if (diffDays === 1) return "Вчера";
    if (diffDays < 7) return `${diffDays} дн. назад`;

    return sessionDate.toLocaleDateString("ru-RU", {
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
            <aside className="w-64 bg-surface-container-lowest border-r border-outline-variant flex flex-col h-full">
                <div className="p-3 border-b border-outline-variant flex gap-2">
                    <button
                        onClick={onClose}
                        className="p-2 text-on-surface-variant hover:text-on-surface hover:bg-surface-container rounded-xl tonal-transition"
                        aria-label="Закрыть"
                    >
                        <Icon name="close" size="md" />
                    </button>
                    <button
                        onClick={onNewChat}
                        className="flex-1 py-2.5 px-4 bg-primary text-on-primary font-semibold rounded-full shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 tonal-transition flex items-center justify-center gap-2"
                    >
                        <Icon name="add" size="sm" />
                        <span>Новый диалог</span>
                    </button>
                </div>

                <div className="flex-1 overflow-y-auto">
                    {sessions.length === 0 ? (
                        <div className="p-4 text-center">
                            <div className="w-12 h-12 rounded-full bg-surface-container flex items-center justify-center mx-auto mb-3">
                                <Icon name="forum" size="lg" className="text-on-surface-variant" />
                            </div>
                            <p className="text-sm text-on-surface-variant">Нет истории диалогов</p>
                        </div>
                    ) : (
                        Array.from(groupedSessions.entries()).map(([dateLabel, dateSessions]) => (
                            <div key={dateLabel} className="py-2">
                                <div className="px-4 py-1 text-xs font-semibold text-on-surface-variant uppercase tracking-wider">
                                    {dateLabel}
                                </div>
                                {dateSessions.map((session) => (
                                    <button
                                        key={session.id}
                                        onClick={() => onSessionClick(session.id)}
                                        className={`w-full px-3 py-2.5 text-left tonal-transition group ${
                                            currentSessionId === session.id
                                                ? "bg-primary-container"
                                                : "hover:bg-surface-container"
                                        }`}
                                    >
                                        <div className="flex items-start gap-2">
                                            <div className="flex-1 min-w-0">
                                                <div className="flex items-center gap-2">
                                                    <span className={`text-sm font-medium truncate ${
                                                        currentSessionId === session.id
                                                            ? "text-primary"
                                                            : "text-on-surface"
                                                    }`}>
                                                        {session.modeTitle}
                                                    </span>
                                                    {session.status === "completed" && session.xpEarned > 0 && (
                                                        <span className="text-xs text-primary font-semibold flex-shrink-0 bg-primary-container px-1.5 py-0.5 rounded">
                                                            +{session.xpEarned}
                                                        </span>
                                                    )}
                                                </div>
                                                <div className="flex items-center gap-2 mt-0.5">
                                                    <span className="text-xs text-on-surface-variant truncate">
                                                        {session.bundleTitle}
                                                    </span>
                                                    <span className="text-xs text-outline">•</span>
                                                    <span className="text-xs text-on-surface-variant flex items-center gap-0.5">
                                                        <Icon name="chat_bubble" size="sm" />
                                                        {session.messageCount}
                                                    </span>
                                                </div>
                                            </div>
                                            <button
                                                onClick={(e) => handleDeleteClick(e, session.id)}
                                                className="text-on-surface-variant hover:text-error opacity-0 group-hover:opacity-100 tonal-transition self-center flex-shrink-0 p-1 rounded hover:bg-error-container"
                                                aria-label="Удалить чат"
                                            >
                                                <Icon name="delete" size="sm" />
                                            </button>
                                        </div>
                                    </button>
                                ))}
                            </div>
                        ))
                    )}
                </div>

                <div className="p-3 border-t border-outline-variant">
                    <Link
                        href="/dialog"
                        className="flex items-center justify-center gap-2 text-sm text-on-surface-variant hover:text-primary tonal-transition py-2"
                    >
                        <Icon name="arrow_back" size="sm" />
                        К выбору навыка
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
