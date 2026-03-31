"use client";

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
            <div className="bg-[#F7F7F7] rounded-2xl p-4 flex items-center gap-3">
                <span className="text-2xl">🔥</span>
                <div>
                    <div className="font-[var(--font-space-grotesk)] font-bold text-xl text-gray-900">
                        {currentStreakDayCount}
                    </div>
                    <div className="text-xs text-gray-500 uppercase tracking-wider">
                        Стрик
                    </div>
                </div>
            </div>

            <div className="bg-[#F7F7F7] rounded-2xl p-4 flex items-center gap-3">
                <span className="text-2xl">⚡</span>
                <div>
                    <div className="font-[var(--font-space-grotesk)] font-bold text-xl text-[#1CB0F6]">
                        {weeklyXpAmount}
                    </div>
                    <div className="text-xs text-gray-500 uppercase tracking-wider">
                        XP за неделю
                    </div>
                </div>
            </div>

            <div className="bg-[#F7F7F7] rounded-2xl p-4 flex items-center gap-3">
                <span className="text-2xl">🏆</span>
                <div>
                    <div className="font-[var(--font-space-grotesk)] font-bold text-xl text-[#FFC800]">
                        {totalXpAmount}
                    </div>
                    <div className="text-xs text-gray-500 uppercase tracking-wider">
                        Всего XP
                    </div>
                </div>
            </div>
        </div>
    );
}
