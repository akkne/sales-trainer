"use client";

import { useCurrentLeague } from "@/lib/hooks/useLeague";
import { useState } from "react";

const TIER_LABELS: Record<string, { label: string; emoji: string }> = {
    bronze: { label: "Бронза", emoji: "🥉" },
    silver: { label: "Серебро", emoji: "🥈" },
    gold: { label: "Золото", emoji: "🥇" },
    diamond: { label: "Алмаз", emoji: "💎" },
};

const OUTCOME_BANNERS = {
    promoted: {
        bg: "bg-[#D7FFB8] border border-[#58CC02]",
        icon: "🚀",
        title: "Повышение!",
        text: "Ты поднялся в следующую лигу. Отличная работа!",
        textColor: "text-[#3C8400]",
    },
    demoted: {
        bg: "bg-[#FFDFE0] border border-[#FF4B4B]",
        icon: "📉",
        title: "Понижение",
        text: "На прошлой неделе не хватило XP. Держись — впереди новая неделя!",
        textColor: "text-[#CC3333]",
    },
};

const PROMOTION_ZONE_SIZE = 10;
const DEMOTION_ZONE_SIZE = 5;

export default function LeaguePage() {
    const { data: leagueData, isLoading } = useCurrentLeague();
    const [bannerDismissed, setBannerDismissed] = useState(false);

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    if (!leagueData) return null;

    const tierInfo = TIER_LABELS[leagueData.tier] ?? { label: leagueData.tier, emoji: "🏆" };
    const totalParticipantCount = leagueData.participantsByRank.length;
    const outcomeConfig = leagueData.previousWeekOutcome
        ? OUTCOME_BANNERS[leagueData.previousWeekOutcome]
        : null;

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            {/* Previous week outcome banner */}
            {outcomeConfig && !bannerDismissed && (
                <div className={`${outcomeConfig.bg} rounded-2xl p-4 mb-6 flex items-start gap-3`}>
                    <span className="text-2xl mt-0.5">{outcomeConfig.icon}</span>
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
                        className="text-gray-400 hover:text-gray-600 text-xl leading-none"
                    >
                        ✕
                    </button>
                </div>
            )}

            <div className="text-center mb-8">
                <div className="text-5xl mb-2">{tierInfo.emoji}</div>
                <h1 className="font-[var(--font-space-grotesk)] text-2xl font-bold text-gray-900">
                    Лига {tierInfo.label}
                </h1>
                <p className="text-sm text-gray-400 mt-1">
                    Неделя до {leagueData.weekEndDate}
                </p>
                {leagueData.currentUserRank > 0 && (
                    <p className="text-sm font-semibold text-[#58CC02] mt-2">
                        Твоё место: #{leagueData.currentUserRank}
                    </p>
                )}
            </div>

            <div className="flex flex-col gap-1">
                {leagueData.participantsByRank.map((participant, participantIndex) => {
                    const isInPromotionZone = participantIndex < PROMOTION_ZONE_SIZE;
                    const isInDemotionZone =
                        participantIndex >= totalParticipantCount - DEMOTION_ZONE_SIZE;

                    // Zone separator lines
                    const isPromotionBoundary = participantIndex === PROMOTION_ZONE_SIZE;
                    const isDemotionBoundary =
                        participantIndex === totalParticipantCount - DEMOTION_ZONE_SIZE;

                    return (
                        <div key={participant.userId}>
                            {isPromotionBoundary && (
                                <div className="flex items-center gap-2 py-2 px-1">
                                    <div className="flex-1 h-px bg-gray-200" />
                                    <span className="text-xs text-gray-400 uppercase tracking-wider whitespace-nowrap">
                                        Безопасная зона
                                    </span>
                                    <div className="flex-1 h-px bg-gray-200" />
                                </div>
                            )}
                            {isDemotionBoundary && (
                                <div className="flex items-center gap-2 py-2 px-1">
                                    <div className="flex-1 h-px bg-red-200" />
                                    <span className="text-xs text-red-400 uppercase tracking-wider whitespace-nowrap">
                                        Зона вылета
                                    </span>
                                    <div className="flex-1 h-px bg-red-200" />
                                </div>
                            )}

                            <div
                                className={`flex items-center gap-4 px-4 py-3 rounded-2xl ${
                                    participant.isCurrentUser
                                        ? "bg-[#E8F9D6] border-2 border-[#58CC02]"
                                        : isInPromotionZone
                                          ? "bg-[#F0FDE4]"
                                          : isInDemotionZone
                                            ? "bg-[#FFF5F5]"
                                            : participantIndex % 2 === 0
                                              ? "bg-white"
                                              : "bg-[#F7F7F7]"
                                }`}
                            >
                                <div
                                    className={`w-7 text-center font-bold text-sm ${
                                        isInPromotionZone
                                            ? "text-[#58CC02]"
                                            : isInDemotionZone
                                              ? "text-red-400"
                                              : "text-gray-400"
                                    }`}
                                >
                                    {participant.rank}
                                </div>

                                <div
                                    className={`w-8 h-8 rounded-full flex items-center justify-center text-white font-bold text-sm shrink-0 ${
                                        participant.isCurrentUser ? "bg-[#58CC02]" : "bg-gray-300"
                                    }`}
                                >
                                    {participant.displayName[0]?.toUpperCase()}
                                </div>

                                <span
                                    className={`flex-1 font-medium ${
                                        participant.isCurrentUser
                                            ? "text-gray-900 font-bold"
                                            : "text-gray-700"
                                    }`}
                                >
                                    {participant.displayName}
                                    {participant.isCurrentUser && " (ты)"}
                                </span>

                                <span className="font-[var(--font-space-grotesk)] font-bold text-[#1CB0F6] text-sm">
                                    {participant.weeklyXpAmount} XP
                                </span>

                                {isInPromotionZone && (
                                    <span className="text-xs text-[#58CC02]" title="Зона повышения">
                                        ↑
                                    </span>
                                )}
                                {isInDemotionZone && (
                                    <span className="text-xs text-red-400" title="Зона вылета">
                                        ↓
                                    </span>
                                )}
                            </div>
                        </div>
                    );
                })}
            </div>

            {/* Zone legend */}
            <div className="mt-6 flex gap-4 justify-center text-xs text-gray-400">
                <span className="flex items-center gap-1">
                    <span className="text-[#58CC02]">↑</span> Топ {PROMOTION_ZONE_SIZE} → повышение
                </span>
                <span className="flex items-center gap-1">
                    <span className="text-red-400">↓</span> Низ {DEMOTION_ZONE_SIZE} → вылет
                </span>
            </div>
        </div>
    );
}
