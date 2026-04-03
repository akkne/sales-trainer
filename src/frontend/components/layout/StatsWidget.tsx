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
            <div className="border-2 border-[#FFC800] rounded-2xl p-4 flex items-center gap-3 hover:border-[#E0A800] transition-colors">
                <span className="text-2xl">🔥</span>
                <div>
                    <div className="font-bold text-xl text-gray-900">{currentStreakDayCount}</div>
                    <div className="text-xs text-[#AFAFAF] uppercase tracking-wider">Стрик</div>
                </div>
            </div>

            <div className="border-2 border-[#1CB0F6] rounded-2xl p-4 flex items-center gap-3 hover:border-[#0090D6] transition-colors">
                <span className="text-2xl">⚡</span>
                <div>
                    <div className="font-bold text-xl text-[#1CB0F6]">{weeklyXpAmount}</div>
                    <div className="text-xs text-[#AFAFAF] uppercase tracking-wider">XP за неделю</div>
                </div>
            </div>

            <div className="border-2 border-[#FF4B4B] rounded-2xl p-4 flex items-center gap-3 hover:border-[#CC3333] transition-colors">
                <span className="text-2xl">🏆</span>
                <div>
                    <div className="font-bold text-xl text-[#FF4B4B]">{totalXpAmount}</div>
                    <div className="text-xs text-[#AFAFAF] uppercase tracking-wider">Всего XP</div>
                </div>
            </div>

            {/* Mascot card */}
            <div className="border-2 border-[#E5E5E5] rounded-2xl p-4 flex items-start gap-3 mt-1">
                <span className="text-2xl">🦊</span>
                <p className="text-xs text-gray-600 leading-relaxed">
                    Каждый урок приближает тебя к мастерству. Продолжай!
                </p>
            </div>
        </div>
    );
}
