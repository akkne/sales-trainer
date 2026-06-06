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
            <aside className="w-64 bg-surface border-r border-line flex flex-col h-full">
                <div className="p-3 border-b border-line flex gap-2">
                    <button
                        onClick={onClose}
                        className="p-2 text-ink-3 hover:text-ink hover:bg-bg-2 rounded-xl transition-colors"
                        aria-label="Закрыть"
                    >
                        <Icon name="close" size="md" />
                    </button>
                    <button
                        onClick={onNewChat}
                        className="flex-1 py-2.5 px-4 bg-ink text-bg font-semibold rounded-full active:translate-y-px transition-colors flex items-center justify-center gap-2"
                        style={{ boxShadow: "var(--sh-2)" }}
                    >
                        <Icon name="plus" size="sm" />
                        <span>Новый диалог</span>
                    </button>
                </div>

                <div className="flex-1 overflow-y-auto">
                    {sessions.length === 0 ? (
                        <div className="p-4 text-center">
                            <div className="w-12 h-12 rounded-full bg-bg-2 flex items-center justify-center mx-auto mb-3">
                                <Icon name="message" size="lg" className="text-ink-4" />
                            </div>
                            <p className="text-sm text-ink-3">Нет истории диалогов</p>
                        </div>
                    ) : (
                        Array.from(groupedSessions.entries()).map(([dateLabel, dateSessions]) => (
                            <div key={dateLabel} className="py-2">
                                <div className="px-4 py-1 text-xs font-semibold text-ink-4 uppercase tracking-wider">
                                    {dateLabel}
                                </div>
                                {dateSessions.map((session) => (
                                    <button
                                        key={session.id}
                                        onClick={() => onSessionClick(session.id)}
                                        className={`w-full px-3 py-2.5 text-left transition-colors group ${
                                            currentSessionId === session.id
                                                ? "bg-indigo-soft"
                                                : "hover:bg-bg-2"
                                        }`}
                                    >
                                        <div className="flex items-start gap-2">
                                            <div className="flex-1 min-w-0">
                                                <div className="flex items-center gap-2">
                                                    <span className={`text-sm font-medium truncate ${
                                                        currentSessionId === session.id
                                                            ? "text-indigo-ink"
                                                            : "text-ink"
                                                    }`}>
                                                        {session.modeTitle}
                                                    </span>
                                                    {session.status === "completed" && session.xpEarned > 0 && (
                                                        <span className="text-xs text-indigo-ink font-semibold flex-shrink-0 bg-indigo-soft px-1.5 py-0.5 rounded font-mono">
                                                            +{session.xpEarned}
                                                        </span>
                                                    )}
                                                </div>
                                                <div className="flex items-center gap-2 mt-0.5">
                                                    <span className="text-xs text-ink-3 truncate">
                                                        {session.bundleTitle}
                                                    </span>
                                                    <span className="text-xs text-ink-4">•</span>
                                                    <span className="text-xs text-ink-3 flex items-center gap-0.5">
                                                        <Icon name="message" size="sm" />
                                                        {session.messageCount}
                                                    </span>
                                                </div>
                                            </div>
                                            <button
                                                onClick={(e) => handleDeleteClick(e, session.id)}
                                                className="text-ink-3 hover:text-bad opacity-0 group-hover:opacity-100 transition-colors self-center flex-shrink-0 p-1 rounded hover:bg-bad-soft"
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

                <div className="p-3 border-t border-line">
                    <Link
                        href="/dialog"
                        className="flex items-center justify-center gap-2 text-sm text-ink-3 hover:text-ink transition-colors py-2"
                    >
                        <Icon name="arrow-left" size="sm" />
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
