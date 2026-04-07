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

const TIER_LABELS: Record<string, { label: string; icon: string }> = {
    bronze: { label: "Бронза", icon: "military_tech" },
    silver: { label: "Серебро", icon: "military_tech" },
    gold: { label: "Золото", icon: "military_tech" },
    diamond: { label: "Алмаз", icon: "diamond" },
};

const OUTCOME_BANNERS = {
    promoted: {
        bg: "bg-primary-container",
        icon: "rocket_launch",
        iconColor: "text-primary",
        title: "Повышение!",
        text: "Ты поднялся в следующую лигу. Отличная работа!",
        textColor: "text-on-primary-container",
    },
    demoted: {
        bg: "bg-error-container",
        icon: "trending_down",
        iconColor: "text-error",
        title: "Понижение",
        text: "На прошлой неделе не хватило XP. Держись — впереди новая неделя!",
        textColor: "text-on-error-container",
    },
};

const PROMOTION_ZONE_SIZE = 10;
const DEMOTION_ZONE_SIZE = 5;

export default function LeaguePage() {
    const { data: leagueData, isLoading } = useCurrentLeague();
    const [bannerDismissed, setBannerDismissed] = useState(false);
    const countdown = useCountdown(leagueData?.weekEndDate ?? "");

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-primary border-t-transparent animate-spin" />
            </div>
        );
    }

    if (!leagueData) return null;

    const tierInfo = TIER_LABELS[leagueData.tier] ?? { label: leagueData.tier, icon: "emoji_events" };
    const totalParticipantCount = leagueData.participantsByRank.length;
    const outcomeConfig = leagueData.previousWeekOutcome
        ? OUTCOME_BANNERS[leagueData.previousWeekOutcome]
        : null;

    // Calculate XP to top 10
    const topTenMinXp = leagueData.participantsByRank[PROMOTION_ZONE_SIZE - 1]?.weeklyXpAmount ?? 0;
    const currentUserXp = leagueData.participantsByRank.find(p => p.isCurrentUser)?.weeklyXpAmount ?? 0;
    const xpToTopTen = Math.max(0, topTenMinXp - currentUserXp + 1);

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            {/* Previous week outcome banner */}
            {outcomeConfig && !bannerDismissed && (
                <div className={`${outcomeConfig.bg} rounded-2xl p-4 mb-6 flex items-start gap-3`}>
                    <Icon name={outcomeConfig.icon} size="lg" className={outcomeConfig.iconColor} />
                    <div className="flex-1">
                        <p className={`font-bold ${outcomeConfig.textColor}`}>
                            {outcomeConfig.title}
                        </p>
                        <p className={`text-sm ${outcomeConfig.textColor} opacity-80`}>
                            {outcomeConfig.text}
                        </p>
                    </div>
                    <button
                        onClick={() => setBannerDismissed(true)}
                        className="text-on-surface-variant hover:text-on-surface tonal-transition"
                    >
                        <Icon name="close" size="sm" />
                    </button>
                </div>
            )}

            {/* League Header Card */}
            <section className="rounded-2xl bg-secondary-container text-on-secondary-container p-5 mb-6">
                <div className="flex items-center gap-2 mb-2">
                    <Icon name={tierInfo.icon} size="md" />
                    <span className="text-sm font-semibold uppercase tracking-widest">Арена</span>
                </div>
                <h1 className="font-headline text-3xl font-bold mb-1">Лига {tierInfo.label}</h1>
                <p className="text-sm opacity-80 mb-4">
                    Топ {PROMOTION_ZONE_SIZE} поднимаются выше. Займи место до окончания недели.
                </p>

                {/* Countdown */}
                <div className="flex items-end gap-3">
                    <div className="flex flex-col items-center">
                        <span className="font-headline text-3xl font-bold tabular-nums">
                            {String(countdown.days).padStart(2, "0")}
                        </span>
                        <span className="text-[10px] uppercase tracking-wider opacity-70">Дней</span>
                    </div>
                    <span className="font-headline text-2xl font-bold mb-1">:</span>
                    <div className="flex flex-col items-center">
                        <span className="font-headline text-3xl font-bold tabular-nums">
                            {String(countdown.hours).padStart(2, "0")}
                        </span>
                        <span className="text-[10px] uppercase tracking-wider opacity-70">Часов</span>
                    </div>
                    <span className="font-headline text-2xl font-bold mb-1">:</span>
                    <div className="flex flex-col items-center">
                        <span className="font-headline text-3xl font-bold tabular-nums">
                            {String(countdown.mins).padStart(2, "0")}
                        </span>
                        <span className="text-[10px] uppercase tracking-wider opacity-70">Минут</span>
                    </div>
                </div>
            </section>

            {/* Stat Cards Row */}
            <div className="grid grid-cols-2 gap-3 mb-6">
                {/* Rank Card */}
                <div className="rounded-2xl bg-surface-container p-4">
                    <div className="flex items-center gap-1 text-xs text-on-surface-variant font-medium uppercase tracking-wide mb-1">
                        <Icon name="trending_up" size="sm" className="text-primary" />
                        Твоё место
                    </div>
                    <p className="font-headline text-4xl font-bold text-primary tabular-nums">
                        #{leagueData.currentUserRank}
                    </p>
                    {leagueData.currentUserRank <= PROMOTION_ZONE_SIZE && (
                        <p className="text-xs text-secondary flex items-center gap-1 mt-1">
                            <Icon name="expand_less" size="sm" />
                            Зона повышения
                        </p>
                    )}
                </div>

                {/* Status Card */}
                <div className="rounded-2xl bg-surface-container p-4">
                    <p className="text-xs text-on-surface-variant font-medium uppercase tracking-wide mb-1">
                        Статус
                    </p>
                    <p className="font-headline text-lg font-bold text-on-surface">
                        {leagueData.currentUserRank <= PROMOTION_ZONE_SIZE
                            ? "В зоне повышения"
                            : leagueData.currentUserRank > totalParticipantCount - DEMOTION_ZONE_SIZE
                            ? "Зона вылета"
                            : "Безопасная зона"}
                    </p>
                    {leagueData.currentUserRank > PROMOTION_ZONE_SIZE && (
                        <p className="text-xs text-on-surface-variant mt-1">
                            {xpToTopTen} XP до топ-{PROMOTION_ZONE_SIZE}
                        </p>
                    )}
                </div>
            </div>

            {/* Leaderboard Table */}
            <section className="rounded-2xl bg-surface-container overflow-hidden">
                {/* Table Header */}
                <div className="grid grid-cols-[3rem_1fr_auto] items-center px-4 py-2 bg-surface-container-high text-xs font-semibold text-on-surface-variant uppercase tracking-wider">
                    <span>Место</span>
                    <span>Участник</span>
                    <span className="text-right">XP</span>
                </div>

                {/* Rows */}
                {leagueData.participantsByRank.map((participant, participantIndex) => {
                    const isInPromotionZone = participantIndex < PROMOTION_ZONE_SIZE;
                    const isInDemotionZone = participantIndex >= totalParticipantCount - DEMOTION_ZONE_SIZE;

                    // Zone separator lines
                    const isPromotionBoundary = participantIndex === PROMOTION_ZONE_SIZE;
                    const isDemotionBoundary = participantIndex === totalParticipantCount - DEMOTION_ZONE_SIZE;

                    // Rank badge styles
                    const getRankBadgeClass = () => {
                        if (participant.rank === 1) return "bg-[#FFD700] text-[#5a4000]";
                        if (participant.rank === 2) return "bg-[#C0C0C0] text-[#3a3a3a]";
                        if (participant.rank === 3) return "bg-[#CD7F32] text-white";
                        if (participant.isCurrentUser) return "bg-primary text-on-primary";
                        if (isInDemotionZone) return "bg-error-container text-on-error-container";
                        return "bg-surface-container-high text-on-surface-variant";
                    };

                    // Row background
                    const getRowClass = () => {
                        if (participant.isCurrentUser) return "bg-primary/10 border-l-4 border-primary";
                        if (isInPromotionZone) return "bg-primary/5";
                        if (isInDemotionZone) return "bg-error/5";
                        return "";
                    };

                    return (
                        <div key={participant.userId}>
                            {isPromotionBoundary && (
                                <div className="flex items-center gap-2 py-2 px-4">
                                    <div className="flex-1 h-px bg-outline-variant" />
                                    <span className="text-xs text-on-surface-variant uppercase tracking-wider whitespace-nowrap">
                                        Безопасная зона
                                    </span>
                                    <div className="flex-1 h-px bg-outline-variant" />
                                </div>
                            )}
                            {isDemotionBoundary && (
                                <div className="flex items-center gap-2 py-2 px-4">
                                    <div className="flex-1 h-px bg-error/30" />
                                    <span className="text-xs text-error uppercase tracking-wider whitespace-nowrap">
                                        Зона вылета
                                    </span>
                                    <div className="flex-1 h-px bg-error/30" />
                                </div>
                            )}

                            <div
                                className={`grid grid-cols-[3rem_1fr_auto] items-center px-4 py-3 gap-3 border-b border-outline-variant/50 ${getRowClass()}`}
                            >
                                {/* Rank badge */}
                                <div
                                    className={`flex items-center justify-center w-8 h-8 rounded-full font-bold text-sm ${getRankBadgeClass()}`}
                                >
                                    {String(participant.rank).padStart(2, "0")}
                                </div>

                                {/* Participant info */}
                                <div className="flex items-center gap-3 min-w-0">
                                    <div
                                        className={`w-10 h-10 rounded-full flex items-center justify-center text-white font-bold text-sm shrink-0 ${
                                            participant.isCurrentUser ? "bg-primary ring-2 ring-primary-container" : "bg-surface-container-highest text-on-surface-variant"
                                        }`}
                                    >
                                        {participant.displayName[0]?.toUpperCase()}
                                    </div>
                                    <div className="min-w-0">
                                        <p
                                            className={`font-semibold text-sm truncate ${
                                                participant.isCurrentUser
                                                    ? "text-primary"
                                                    : "text-on-surface"
                                            }`}
                                        >
                                            {participant.displayName}
                                            {participant.isCurrentUser && " (ты)"}
                                        </p>
                                        {participant.rank <= 3 && (
                                            <p className="text-xs text-on-surface-variant">
                                                {participant.rank === 1 ? "Лидер" : participant.rank === 2 ? "2-е место" : "3-е место"}
                                            </p>
                                        )}
                                    </div>
                                </div>

                                {/* XP */}
                                <span
                                    className={`tabular-nums font-bold text-sm text-right ${
                                        participant.isCurrentUser
                                            ? "text-primary"
                                            : isInDemotionZone
                                            ? "text-error"
                                            : "text-tertiary"
                                    }`}
                                >
                                    {participant.weeklyXpAmount}
                                </span>
                            </div>
                        </div>
                    );
                })}
            </section>

            {/* Zone legend */}
            <div className="mt-4 flex gap-4 justify-center text-xs text-on-surface-variant">
                <span className="flex items-center gap-1">
                    <Icon name="expand_less" size="sm" className="text-primary" />
                    Топ {PROMOTION_ZONE_SIZE} → повышение
                </span>
                <span className="flex items-center gap-1">
                    <Icon name="expand_more" size="sm" className="text-error" />
                    Низ {DEMOTION_ZONE_SIZE} → вылет
                </span>
            </div>

            {/* Boost CTA Banner */}
            <div className="mt-6 rounded-2xl bg-tertiary-container text-on-tertiary-container p-4 flex items-start gap-3">
                <Icon name="bolt" size="lg" className="text-tertiary mt-0.5" />
                <div className="flex-1">
                    <p className="font-headline font-bold text-sm">Ускорь продвижение</p>
                    <p className="text-xs mt-0.5 opacity-80">
                        Пройди урок, чтобы заработать XP и подняться выше.
                    </p>
                </div>
                <Link
                    href="/tree"
                    className="shrink-0 bg-tertiary text-on-tertiary text-xs font-semibold px-3 py-1.5 rounded-full hover:opacity-90 tonal-transition flex items-center gap-1"
                >
                    <Icon name="school" size="sm" />
                    Учиться
                </Link>
            </div>
        </div>
    );
}
