"use client";

import Link from "next/link";
import { DialogSessionSummary } from "@/lib/hooks/useDialog";

interface SessionHistorySidebarProps {
    sessions: DialogSessionSummary[];
    currentSessionId: string | null;
    onSessionClick: (sessionId: string) => void;
    onNewChat: () => void;
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
}: SessionHistorySidebarProps) {
    const groupedSessions = groupSessionsByDate(sessions);

    return (
        <aside className="w-64 bg-gray-50 border-r border-gray-200 flex flex-col h-full">
            <div className="p-4 border-b border-gray-200">
                <button
                    onClick={onNewChat}
                    className="w-full py-2 px-4 bg-[#58CC02] text-white font-semibold rounded-xl hover:bg-[#4CAD02] transition-colors flex items-center justify-center gap-2"
                >
                    <span>+</span>
                    <span>Новый диалог</span>
                </button>
            </div>

            <div className="flex-1 overflow-y-auto">
                {sessions.length === 0 ? (
                    <div className="p-4 text-center text-gray-400 text-sm">
                        Нет истории диалогов
                    </div>
                ) : (
                    Array.from(groupedSessions.entries()).map(([dateLabel, dateSessions]) => (
                        <div key={dateLabel} className="py-2">
                            <div className="px-4 py-1 text-xs font-semibold text-gray-400 uppercase">
                                {dateLabel}
                            </div>
                            {dateSessions.map((session) => (
                                <button
                                    key={session.id}
                                    onClick={() => onSessionClick(session.id)}
                                    className={`w-full px-4 py-3 text-left hover:bg-gray-100 transition-colors ${
                                        currentSessionId === session.id ? "bg-gray-100" : ""
                                    }`}
                                >
                                    <div className="flex items-center gap-2">
                                        <span className="text-sm font-medium text-gray-800 truncate flex-1">
                                            {session.modeTitle}
                                        </span>
                                        {session.status === "completed" && session.xpEarned > 0 && (
                                            <span className="text-xs text-[#58CC02] font-semibold">
                                                +{session.xpEarned} XP
                                            </span>
                                        )}
                                    </div>
                                    <div className="flex items-center gap-2 mt-0.5">
                                        <span className="text-xs text-gray-400 truncate">
                                            {session.bundleTitle}
                                        </span>
                                        <span className="text-xs text-gray-300">•</span>
                                        <span className="text-xs text-gray-400">
                                            {session.messageCount} сообщ.
                                        </span>
                                    </div>
                                </button>
                            ))}
                        </div>
                    ))
                )}
            </div>

            <div className="p-4 border-t border-gray-200">
                <Link
                    href="/dialog"
                    className="block text-center text-sm text-gray-500 hover:text-gray-700"
                >
                    ← К выбору навыка
                </Link>
            </div>
        </aside>
    );
}
