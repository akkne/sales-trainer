"use client";

import { useCurrentLeague } from "@/lib/hooks/useLeague";
import { useEffect, useState } from "react";
import { Icon } from "@/components/ui/Icon";
import Link from "next/link";

function useCountdown(weekEndDate: string) {
    const [timeLeft, setTimeLeft] = useState({ days: 0, hours: 0, mins: 0 });

    useEffect(() => {
        function compute() {
            const endMs = new Date(weekEndDate).getTime();
            const diffMs = endMs - Date.now();
            if (diffMs <= 0) {
                setTimeLeft({ days: 0, hours: 0, mins: 0 });
                return;
            }
            const totalMinutes = Math.floor(diffMs / 60000);
            const days = Math.floor(totalMinutes / 1440);
            const hours = Math.floor((totalMinutes % 1440) / 60);
            const mins = totalMinutes % 60;
            setTimeLeft({ days, hours, mins });
        }
        compute();
        const id = setInterval(compute, 60_000);
        return () => clearInterval(id);
    }, [weekEndDate]);

    return timeLeft;
}

const TIER_CONFIG: Record<string, { label: string; color: string }> = {
    bronze: { label: "Бронза", color: "var(--clay)" },
    silver: { label: "Серебро", color: "var(--ink-3)" },
    gold: { label: "Золото", color: "var(--rust)" },
    diamond: { label: "Алмаз", color: "var(--indigo)" },
};

const PROMOTION_ZONE_SIZE = 10;
const DEMOTION_ZONE_SIZE = 5;

export default function LeaguePage() {
    const { data: leagueData, isLoading } = useCurrentLeague();
    const [bannerDismissed, setBannerDismissed] = useState(false);
    const countdown = useCountdown(leagueData?.weekEndDate ?? "");

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen bg-bg">
                <div className="w-12 h-12 rounded-full border-3 border-line-2 border-t-indigo animate-spin" />
            </div>
        );
    }

    if (!leagueData) return null;

    const tierInfo = TIER_CONFIG[leagueData.tier] ?? { label: leagueData.tier, color: "var(--ink-3)" };
    const totalParticipantCount = leagueData.participantsByRank.length;
    const currentUser = leagueData.participantsByRank.find(p => p.isCurrentUser);
    const currentUserXp = currentUser?.weeklyXpAmount ?? 0;
    const topTenMinXp = leagueData.participantsByRank[PROMOTION_ZONE_SIZE - 1]?.weeklyXpAmount ?? 0;
    const xpToTopTen = Math.max(0, topTenMinXp - currentUserXp + 1);

    const isInPromotionZone = leagueData.currentUserRank <= PROMOTION_ZONE_SIZE;
    const isInDemotionZone = leagueData.currentUserRank > totalParticipantCount - DEMOTION_ZONE_SIZE;

    return (
        <div className="min-h-screen bg-bg pb-20">
            {/* Header */}
            <div className="bg-surface border-b border-line px-6 py-5 md:px-8">
                <div className="max-w-2xl mx-auto">
                    <div className="flex items-center gap-2 mb-2">
                        <span
                            className="text-[10px] font-mono tracking-[2px] uppercase"
                            style={{ color: tierInfo.color }}
                        >
                            АРЕНА · НЕДЕЛЯ
                        </span>
                    </div>
                    <h1 className="text-3xl md:text-4xl font-medium tracking-tight text-ink">
                        Лига {tierInfo.label}
                    </h1>
                    <p className="text-sm text-ink-3 mt-2 max-w-md">
                        Топ {PROMOTION_ZONE_SIZE} поднимаются выше. Займи место до окончания недели.
                    </p>
                </div>
            </div>

            <div className="max-w-2xl mx-auto px-4 md:px-6 py-6">
                {/* Outcome Banner */}
                {leagueData.previousWeekOutcome && !bannerDismissed && (
                    <div
                        className={`rounded-2xl p-4 mb-6 flex items-start gap-3 ${
                            leagueData.previousWeekOutcome === "promoted"
                                ? "bg-olive-soft"
                                : "bg-bad-soft"
                        }`}
                    >
                        <div
                            className={`w-10 h-10 rounded-xl flex items-center justify-center ${
                                leagueData.previousWeekOutcome === "promoted"
                                    ? "bg-olive text-white"
                                    : "bg-bad text-white"
                            }`}
                        >
                            <Icon
                                name={leagueData.previousWeekOutcome === "promoted" ? "trending_up" : "trending_down"}
                                size="md"
                            />
                        </div>
                        <div className="flex-1">
                            <p className="font-medium text-ink">
                                {leagueData.previousWeekOutcome === "promoted" ? "Повышение!" : "Понижение"}
                            </p>
                            <p className="text-sm text-ink-3">
                                {leagueData.previousWeekOutcome === "promoted"
                                    ? "Ты поднялся в следующую лигу. Отличная работа!"
                                    : "На прошлой неделе не хватило XP. Держись — впереди новая неделя!"}
                            </p>
                        </div>
                        <button
                            onClick={() => setBannerDismissed(true)}
                            className="text-ink-4 hover:text-ink-2 transition-colors p-1"
                        >
                            <Icon name="close" size="sm" />
                        </button>
                    </div>
                )}

                {/* Countdown Timer */}
                <div className="bg-surface border border-line rounded-2xl p-5 mb-4" style={{ boxShadow: "var(--sh-1)" }}>
                    <div className="text-[10px] font-mono tracking-[1.5px] uppercase text-ink-4 mb-3">
                        До конца недели
                    </div>
                    <div className="flex items-end gap-3">
                        <TimeUnit value={countdown.days} label="Дней" />
                        <span className="text-2xl font-medium text-ink-4 mb-1">:</span>
                        <TimeUnit value={countdown.hours} label="Часов" />
                        <span className="text-2xl font-medium text-ink-4 mb-1">:</span>
                        <TimeUnit value={countdown.mins} label="Минут" />
                    </div>
                </div>

                {/* Stats Row */}
                <div className="grid grid-cols-2 gap-3 mb-6">
                    {/* Rank Stat */}
                    <StatTile
                        label="Твоё место"
                        value={`#${leagueData.currentUserRank}`}
                        tone={isInPromotionZone ? "olive" : isInDemotionZone ? "bad" : "neutral"}
                        badge={isInPromotionZone ? "Зона повышения" : isInDemotionZone ? "Зона вылета" : undefined}
                    />
                    {/* XP Stat */}
                    <StatTile
                        label="Твой XP"
                        value={currentUserXp.toLocaleString()}
                        tone="indigo"
                        badge={!isInPromotionZone && xpToTopTen > 0 ? `${xpToTopTen} до топ-${PROMOTION_ZONE_SIZE}` : undefined}
                    />
                </div>

                {/* Leaderboard */}
                <div className="bg-surface border border-line rounded-2xl overflow-hidden" style={{ boxShadow: "var(--sh-1)" }}>
                    {/* Table Header */}
                    <div className="grid grid-cols-[3rem_1fr_auto] items-center px-4 py-3 bg-bg-2 border-b border-line">
                        <span className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4">#</span>
                        <span className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4">Участник</span>
                        <span className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4 text-right">XP</span>
                    </div>

                    {/* Rows */}
                    {leagueData.participantsByRank.map((participant, idx) => {
                        const isInPromoZone = idx < PROMOTION_ZONE_SIZE;
                        const isInDemoZone = idx >= totalParticipantCount - DEMOTION_ZONE_SIZE;
                        const showPromoBoundary = idx === PROMOTION_ZONE_SIZE;
                        const showDemoBoundary = idx === totalParticipantCount - DEMOTION_ZONE_SIZE;

                        return (
                            <div key={participant.userId}>
                                {showPromoBoundary && <ZoneDivider label="Безопасная зона" />}
                                {showDemoBoundary && <ZoneDivider label="Зона вылета" tone="bad" />}

                                <div
                                    className={`grid grid-cols-[3rem_1fr_auto] items-center px-4 py-3 gap-3 border-b border-line/50 transition-colors ${
                                        participant.isCurrentUser
                                            ? "bg-indigo-soft"
                                            : isInPromoZone
                                            ? "bg-olive-soft/30"
                                            : isInDemoZone
                                            ? "bg-bad-soft/30"
                                            : ""
                                    }`}
                                    style={participant.isCurrentUser ? { borderLeft: "3px solid var(--indigo)" } : undefined}
                                >
                                    {/* Rank Badge */}
                                    <RankBadge
                                        rank={participant.rank}
                                        isCurrentUser={participant.isCurrentUser}
                                        isInDemotionZone={isInDemoZone}
                                    />

                                    {/* Participant Info */}
                                    <div className="flex items-center gap-3 min-w-0">
                                        <div
                                            className={`w-9 h-9 rounded-xl flex items-center justify-center text-sm font-medium shrink-0 ${
                                                participant.isCurrentUser
                                                    ? "bg-indigo text-white"
                                                    : "bg-bg-2 text-ink-3"
                                            }`}
                                        >
                                            {participant.displayName[0]?.toUpperCase()}
                                        </div>
                                        <div className="min-w-0">
                                            <p
                                                className={`font-medium text-sm truncate ${
                                                    participant.isCurrentUser ? "text-indigo" : "text-ink"
                                                }`}
                                            >
                                                {participant.displayName}
                                                {participant.isCurrentUser && (
                                                    <span className="text-ink-4 font-normal"> (ты)</span>
                                                )}
                                            </p>
                                            {participant.rank <= 3 && (
                                                <p className="text-[10px] font-mono text-ink-4">
                                                    {participant.rank === 1 ? "Лидер" : `${participant.rank}-е место`}
                                                </p>
                                            )}
                                        </div>
                                    </div>

                                    {/* XP */}
                                    <span
                                        className={`font-mono font-medium text-sm tabular-nums ${
                                            participant.isCurrentUser
                                                ? "text-indigo"
                                                : isInDemoZone
                                                ? "text-bad"
                                                : "text-ink-2"
                                        }`}
                                    >
                                        {participant.weeklyXpAmount}
                                    </span>
                                </div>
                            </div>
                        );
                    })}
                </div>

                {/* Zone Legend */}
                <div className="mt-4 flex flex-wrap gap-4 justify-center text-xs text-ink-4">
                    <span className="flex items-center gap-1">
                        <span className="w-2 h-2 rounded-sm bg-olive" />
                        Топ {PROMOTION_ZONE_SIZE} → повышение
                    </span>
                    <span className="flex items-center gap-1">
                        <span className="w-2 h-2 rounded-sm bg-bad" />
                        Низ {DEMOTION_ZONE_SIZE} → вылет
                    </span>
                </div>

                {/* CTA Banner */}
                <div
                    className="mt-6 rounded-2xl bg-indigo-soft p-5 flex items-center gap-4"
                    style={{ boxShadow: "var(--sh-1)" }}
                >
                    <div className="w-12 h-12 rounded-xl bg-indigo text-white flex items-center justify-center shrink-0">
                        <Icon name="bolt" size="md" />
                    </div>
                    <div className="flex-1 min-w-0">
                        <p className="font-medium text-ink text-sm">Ускорь продвижение</p>
                        <p className="text-xs text-ink-3 mt-0.5">
                            Пройди урок, чтобы заработать XP и подняться выше.
                        </p>
                    </div>
                    <Link
                        href="/tree"
                        className="shrink-0 bg-indigo text-white text-sm font-medium px-4 py-2 rounded-xl hover:opacity-90 transition-opacity flex items-center gap-2"
                    >
                        Учиться
                        <Icon name="arrow_forward" size="sm" />
                    </Link>
                </div>
            </div>
        </div>
    );
}

function TimeUnit({ value, label }: { value: number; label: string }) {
    return (
        <div className="flex flex-col items-center">
            <span className="text-3xl md:text-4xl font-medium tabular-nums text-ink tracking-tight">
                {String(value).padStart(2, "0")}
            </span>
            <span className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4 mt-1">
                {label}
            </span>
        </div>
    );
}

function StatTile({
    label,
    value,
    tone = "neutral",
    badge
}: {
    label: string;
    value: string;
    tone?: "neutral" | "olive" | "indigo" | "bad";
    badge?: string;
}) {
    const toneStyles = {
        neutral: { bg: "bg-surface", valueColor: "text-ink" },
        olive: { bg: "bg-olive-soft", valueColor: "text-olive" },
        indigo: { bg: "bg-indigo-soft", valueColor: "text-indigo" },
        bad: { bg: "bg-bad-soft", valueColor: "text-bad" },
    };
    const style = toneStyles[tone];

    return (
        <div
            className={`${style.bg} border border-line rounded-2xl p-4`}
            style={{ boxShadow: "var(--sh-1)" }}
        >
            <div className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4 mb-2">
                {label}
            </div>
            <div className={`text-3xl font-medium tabular-nums ${style.valueColor}`}>
                {value}
            </div>
            {badge && (
                <div className="text-[11px] text-ink-3 mt-1 font-mono">
                    {badge}
                </div>
            )}
        </div>
    );
}

function RankBadge({
    rank,
    isCurrentUser,
    isInDemotionZone
}: {
    rank: number;
    isCurrentUser: boolean;
    isInDemotionZone: boolean;
}) {
    const getBadgeStyles = () => {
        if (rank === 1) return "bg-rust text-white";
        if (rank === 2) return "bg-ink-3 text-white";
        if (rank === 3) return "bg-clay text-white";
        if (isCurrentUser) return "bg-indigo text-white";
        if (isInDemotionZone) return "bg-bad-soft text-bad";
        return "bg-bg-2 text-ink-3";
    };

    return (
        <div
            className={`w-8 h-8 rounded-lg flex items-center justify-center font-mono font-medium text-xs ${getBadgeStyles()}`}
        >
            {String(rank).padStart(2, "0")}
        </div>
    );
}

function ZoneDivider({ label, tone = "neutral" }: { label: string; tone?: "neutral" | "bad" }) {
    const lineColor = tone === "bad" ? "bg-bad/30" : "bg-line";
    const textColor = tone === "bad" ? "text-bad" : "text-ink-4";

    return (
        <div className="flex items-center gap-3 py-2 px-4">
            <div className={`flex-1 h-px ${lineColor}`} />
            <span className={`text-[10px] font-mono tracking-[1px] uppercase whitespace-nowrap ${textColor}`}>
                {label}
            </span>
            <div className={`flex-1 h-px ${lineColor}`} />
        </div>
    );
}
