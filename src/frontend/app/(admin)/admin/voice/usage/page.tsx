"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { Icon } from "@/shared/components/icon";
import { TimingConstants } from "@/shared/constants/timing-constants";

interface AdminVoiceUsageEntry {
    userId: string;
    email: string;
    displayName: string;
    dailyUsedSeconds: number;
    monthlyUsedSeconds: number;
    totalSeconds: number;
    sessionCount: number;
    lastCallAt: string | null;
}

interface AdminVoiceUsage {
    dailyLimitSeconds: number;
    monthlyLimitSeconds: number;
    users: AdminVoiceUsageEntry[];
}

function useAdminVoiceUsage() {
    return useQuery({
        queryKey: ["admin", "voice", "usage"],
        queryFn: () => apiClient.get<AdminVoiceUsage>("/admin/voice/usage"),
        staleTime: TimingConstants.thirtySecondsMs,
    });
}

function formatMinutes(seconds: number): string {
    return (seconds / 60).toFixed(1);
}

function formatLastCall(value: string | null): string {
    if (!value) return "—";
    const date = new Date(value);
    return date.toLocaleString("ru-RU", {
        day: "numeric",
        month: "short",
        hour: "2-digit",
        minute: "2-digit",
    });
}

export default function AdminVoiceUsagePage() {
    const { data, isLoading, isError, refetch } = useAdminVoiceUsage();

    return (
        <div className="p-6 max-w-7xl">
            <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
                <div>
                    <h1 className="text-2xl font-bold text-ink">Voice Usage</h1>
                    <p className="text-sm text-ink-3 mt-1">
                        Минуты голосовых звонков по пользователям
                        {data && data.dailyLimitSeconds > 0 && (
                            <>
                                {" "}· лимит {Math.round(data.dailyLimitSeconds / 60)} мин/день,{" "}
                                {Math.round(data.monthlyLimitSeconds / 60)} мин/месяц
                            </>
                        )}
                    </p>
                </div>
                <button
                    onClick={() => refetch()}
                    className="px-4 py-2 rounded-xl bg-surface border border-line text-sm text-ink hover:bg-bg-2 transition-colors"
                >
                    Обновить
                </button>
            </div>

            {isLoading && (
                <div className="space-y-2">
                    {[1, 2, 3, 4].map((i) => (
                        <div key={i} className="h-14 rounded-xl bg-surface border border-line animate-pulse" />
                    ))}
                </div>
            )}

            {isError && (
                <div className="bg-bad-soft text-bad rounded-xl px-4 py-3 text-sm flex items-center gap-2">
                    <Icon name="warning" size="sm" />
                    Не удалось загрузить статистику. Попробуйте обновить.
                </div>
            )}

            {data && data.users.length === 0 && (
                <div className="text-center py-16 text-ink-3">
                    <div className="w-14 h-14 rounded-full bg-bg-2 flex items-center justify-center mx-auto mb-3">
                        <Icon name="mic" size="lg" className="text-ink-4" />
                    </div>
                    Голосовых звонков ещё не было
                </div>
            )}

            {data && data.users.length > 0 && (
                <div className="overflow-x-auto rounded-2xl border border-line bg-surface" style={{ boxShadow: "var(--sh-1)" }}>
                    <table className="w-full text-sm min-w-[640px]">
                        <thead>
                            <tr className="border-b border-line text-left text-xs uppercase tracking-wider text-ink-4">
                                <th className="px-4 py-3 font-medium">Пользователь</th>
                                <th className="px-4 py-3 font-medium text-right">Сегодня, мин</th>
                                <th className="px-4 py-3 font-medium text-right">Месяц, мин</th>
                                <th className="px-4 py-3 font-medium text-right">Всего, мин</th>
                                <th className="px-4 py-3 font-medium text-right">Звонков</th>
                                <th className="px-4 py-3 font-medium text-right">Последний звонок</th>
                            </tr>
                        </thead>
                        <tbody>
                            {data.users.map((entry) => {
                                const dailyOver =
                                    data.dailyLimitSeconds > 0 && entry.dailyUsedSeconds >= data.dailyLimitSeconds;
                                const monthlyOver =
                                    data.monthlyLimitSeconds > 0 && entry.monthlyUsedSeconds >= data.monthlyLimitSeconds;
                                return (
                                    <tr key={entry.userId} className="border-b border-line last:border-b-0 hover:bg-bg-2 transition-colors">
                                        <td className="px-4 py-3">
                                            <div className="font-medium text-ink">{entry.displayName || "—"}</div>
                                            <div className="text-xs text-ink-4">{entry.email || entry.userId}</div>
                                        </td>
                                        <td className={`px-4 py-3 text-right font-mono ${dailyOver ? "text-bad font-semibold" : "text-ink-2"}`}>
                                            {formatMinutes(entry.dailyUsedSeconds)}
                                        </td>
                                        <td className={`px-4 py-3 text-right font-mono ${monthlyOver ? "text-bad font-semibold" : "text-ink-2"}`}>
                                            {formatMinutes(entry.monthlyUsedSeconds)}
                                        </td>
                                        <td className="px-4 py-3 text-right font-mono text-ink-2">
                                            {formatMinutes(entry.totalSeconds)}
                                        </td>
                                        <td className="px-4 py-3 text-right font-mono text-ink-2">{entry.sessionCount}</td>
                                        <td className="px-4 py-3 text-right text-xs text-ink-3">{formatLastCall(entry.lastCallAt)}</td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}
        </div>
    );
}
