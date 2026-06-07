"use client";

import { useCurrentLeague } from "@/features/league/hooks/use-league";
import { useEffect, useState } from "react";
import { Icon } from "@/shared/components/icon";
import { StatTile, GeoAvatar, ErrorState } from "@/shared/components";
import Link from "next/link";
import { TimingConstants } from "@/shared/constants/timing-constants";

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
        const id = setInterval(compute, TimingConstants.oneMinuteMs);
        return () => clearInterval(id);
    }, [weekEndDate]);

    return timeLeft;
}

// Tier colors mirror the design tokens (--tier-*), which are not declared in
// the app globals.css, so they are inlined here.
const TIER_CONFIG: Record<string, { label: string; color: string }> = {
    bronze: { label: "Бронза", color: "#c47b3f" },
    silver: { label: "Серебро", color: "#9aa3ad" },
    gold: { label: "Золото", color: "#e3b23c" },
    diamond: { label: "Алмаз", color: "#4cc6e8" },
};

const PROMOTION_ZONE_SIZE = 10;
const DEMOTION_ZONE_SIZE = 5;

export default function LeaguePage() {
    const { data: leagueData, isLoading, isError, refetch } = useCurrentLeague();
    const countdown = useCountdown(leagueData?.weekEndDate ?? "");

    if (isLoading) {
        return (
            <div className="page">
                <div className="container">
                    <div className="hero-head">
                        <div className="hh-left">
                            <div className="h-3 w-40 rounded bg-surface-2 animate-pulse" />
                            <div className="h-10 w-72 rounded-xl bg-surface-2 animate-pulse" style={{ marginTop: 14 }} />
                            <div className="h-4 w-96 rounded bg-surface-2 animate-pulse" style={{ marginTop: 12 }} />
                        </div>
                    </div>
                    <div className="league-grid">
                        <div className="col gap-4">
                            <div className="card" style={{ height: 160 }} />
                            <div className="card" style={{ height: 96 }} />
                        </div>
                        <div className="card" style={{ height: 480 }} />
                    </div>
                </div>
            </div>
        );
    }

    if (isError) {
        return (
            <div className="page" style={{ padding: "60px 24px" }}>
                <ErrorState title="Не удалось загрузить лигу" onRetry={() => refetch()} />
            </div>
        );
    }

    if (!leagueData) {
        return (
            <div className="page container">
                <div className="empty" style={{ paddingTop: 120 }}>
                    <div className="ic">
                        <Icon name="trophy" size="lg" />
                    </div>
                    <h1 className="h3" style={{ marginBottom: 8 }}>Лига ещё не сформирована</h1>
                    <p className="small">Заработай первые XP — и попадёшь в еженедельный рейтинг.</p>
                </div>
            </div>
        );
    }

    const tierInfo = TIER_CONFIG[leagueData.tier] ?? { label: leagueData.tier, color: "var(--ink-3)" };
    const totalParticipantCount = leagueData.participantsByRank.length;
    const currentUser = leagueData.participantsByRank.find((p) => p.isCurrentUser);
    const currentUserXp = currentUser?.weeklyXpAmount ?? 0;

    return (
        <div className="page">
            <div className="container">
                {/* Hero header */}
                <div className="hero-head">
                    <div className="hh-left fade-up">
                        <span className="eyebrow">
                            Арена<span className="dot">·</span>
                            <span>Неделя</span>
                        </span>
                        <h1 className="h1 hh-title">
                            Лига <span style={{ color: tierInfo.color }}>{tierInfo.label}</span>
                        </h1>
                        <p className="lead">
                            Топ-{PROMOTION_ZONE_SIZE} поднимаются выше, нижние {DEMOTION_ZONE_SIZE} вылетают.
                            Займи место до конца недели.
                        </p>
                    </div>
                    <div className="hero-stats fade-up">
                        <StatTile
                            label="Твоё место"
                            value={`#${leagueData.currentUserRank}`}
                            icon={<Icon name="trophy" size="xs" />}
                            tone="amber"
                        />
                        <StatTile
                            label="Твой XP"
                            value={currentUserXp.toLocaleString("ru")}
                            icon={<Icon name="bolt" size="xs" />}
                            tone="primary"
                        />
                    </div>
                </div>

                <div className="league-grid">
                    {/* Left column */}
                    <div className="col gap-4">
                        <div className="card card-pad countdown">
                            <span className="eyebrow muted">До конца недели</span>
                            <div className="cd-row">
                                <div className="cd-unit">
                                    <b className="num">{String(countdown.days).padStart(2, "0")}</b>
                                    <span>дней</span>
                                </div>
                                <span className="cd-sep">:</span>
                                <div className="cd-unit">
                                    <b className="num">{String(countdown.hours).padStart(2, "0")}</b>
                                    <span>часов</span>
                                </div>
                                <span className="cd-sep">:</span>
                                <div className="cd-unit">
                                    <b className="num">{String(countdown.mins).padStart(2, "0")}</b>
                                    <span>минут</span>
                                </div>
                            </div>
                        </div>

                        <div className="card card-pad cta-row">
                            <span className="itile primary" style={{ width: 48, height: 48 }}>
                                <Icon name="bolt" />
                            </span>
                            <div className="grow">
                                <h4 className="h4">Ускорь продвижение</h4>
                                <p className="small">Пройди урок, чтобы заработать XP и подняться выше.</p>
                            </div>
                            <Link href="/tree" className="btn btn-primary">
                                Учиться
                                <Icon name="play" size={16} />
                            </Link>
                        </div>
                    </div>

                    {/* Leaderboard */}
                    <div className="card lb-card">
                        <div className="lb-head">
                            <span>#</span>
                            <span>Участник</span>
                            <span>XP</span>
                        </div>
                        {leagueData.participantsByRank.map((participant, idx) => {
                            const isInPromoZone = idx < PROMOTION_ZONE_SIZE;
                            const isInDemoZone = idx >= totalParticipantCount - DEMOTION_ZONE_SIZE;
                            const showPromoBoundary = idx === PROMOTION_ZONE_SIZE;
                            const showDemoBoundary =
                                idx === totalParticipantCount - DEMOTION_ZONE_SIZE && idx > 0;

                            const zoneClass = participant.isCurrentUser
                                ? "you"
                                : isInPromoZone
                                  ? "promo"
                                  : isInDemoZone
                                    ? "demote"
                                    : "";

                            return (
                                <div key={participant.userId}>
                                    {showPromoBoundary && (
                                        <div className="lb-divider promo">
                                            <Icon name="arrow-up" size={14} />
                                            Зона повышения
                                        </div>
                                    )}
                                    {showDemoBoundary && (
                                        <div className="lb-divider demote">
                                            <Icon name="chevron-down" size={14} />
                                            Зона вылета
                                        </div>
                                    )}

                                    <div className={`lb-row ${zoneClass}`}>
                                        <span className={`rank-badge r${Math.min(participant.rank, 4)}`}>
                                            {String(participant.rank).padStart(2, "0")}
                                        </span>
                                        <div className="row gap-3 grow" style={{ minWidth: 0 }}>
                                            <GeoAvatar seed={participant.displayName} size={36} />
                                            <span className="lb-name">
                                                {participant.displayName}
                                                {participant.isCurrentUser && (
                                                    <span className="you-tag"> · ты</span>
                                                )}
                                            </span>
                                        </div>
                                        <span className="num lb-xp">{participant.weeklyXpAmount}</span>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            </div>
        </div>
    );
}
