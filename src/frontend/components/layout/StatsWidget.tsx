"use client";

import { Icon } from "@/components/ui/Icon";

interface StatsWidgetProps {
    currentStreakDayCount: number;
    totalXpAmount: number;
    weeklyXpAmount: number;
}

export function StatsWidget({
    currentStreakDayCount,
    totalXpAmount,
    weeklyXpAmount,
}: StatsWidgetProps) {
    return (
        <div className="flex flex-col gap-3">
            {/* Streak card */}
            <div className="bg-surface-container rounded-2xl p-4 flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-error-container flex items-center justify-center">
                    <Icon name="local_fire_department" size="md" className="text-on-error-container" />
                </div>
                <div>
                    <div className="font-headline font-bold text-xl text-on-surface">
                        {currentStreakDayCount}
                    </div>
                    <div className="text-xs text-on-surface-variant uppercase tracking-wider">
                        Стрик
                    </div>
                </div>
            </div>

            {/* Weekly XP card */}
            <div className="bg-surface-container rounded-2xl p-4 flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-tertiary-container flex items-center justify-center">
                    <Icon name="bolt" size="md" className="text-on-tertiary-container" />
                </div>
                <div>
                    <div className="font-headline font-bold text-xl text-tertiary">
                        {weeklyXpAmount}
                    </div>
                    <div className="text-xs text-on-surface-variant uppercase tracking-wider">
                        XP за неделю
                    </div>
                </div>
            </div>

            {/* Total XP card - highlighted */}
            <div className="bg-primary-container rounded-2xl p-4 flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-primary flex items-center justify-center">
                    <Icon name="emoji_events" size="md" className="text-on-primary" />
                </div>
                <div>
                    <div className="font-headline font-bold text-xl text-primary">
                        {totalXpAmount}
                    </div>
                    <div className="text-xs text-on-primary-container uppercase tracking-wider">
                        Всего XP
                    </div>
                </div>
            </div>

            {/* Motivational tip card */}
            <div className="bg-surface-container-low rounded-2xl p-4 flex items-start gap-3 mt-1">
                <Icon name="lightbulb" size="md" className="text-secondary shrink-0" />
                <p className="text-xs text-on-surface-variant leading-relaxed">
                    Каждый урок приближает тебя к мастерству. Продолжай!
                </p>
            </div>
        </div>
    );
}
