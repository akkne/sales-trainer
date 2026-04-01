"use client";

import Link from "next/link";
import { useProfile } from "@/lib/hooks/useProfile";
import { useLogout } from "@/lib/hooks/useAuth";
import { useAuthStore } from "@/lib/store/authStore";

export default function ProfilePage() {
    const { data: profileStats, isLoading } = useProfile();
    const logoutMutation = useLogout();
    const { authenticatedUser } = useAuthStore();
    const isAdmin =
        authenticatedUser?.role === "Admin" || authenticatedUser?.role === "SuperAdmin";

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    if (!profileStats) return null;

    const completionPercent =
        profileStats.totalSkillCount > 0
            ? Math.round(
                  (profileStats.completedSkillCount / profileStats.totalSkillCount) * 100
              )
            : 0;

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            <div className="flex items-center justify-between mb-8">
                <div>
                    <h1 className="font-[var(--font-space-grotesk)] text-2xl font-bold text-gray-900">
                        {profileStats.displayName}
                    </h1>
                    <p className="text-sm text-gray-400">{profileStats.email}</p>
                </div>
                <div className="w-14 h-14 rounded-full bg-[#58CC02] flex items-center justify-center text-white font-bold text-xl">
                    {profileStats.displayName[0]?.toUpperCase()}
                </div>
            </div>

            <div className="grid grid-cols-2 gap-3 mb-6">
                <div className="bg-[#F7F7F7] rounded-2xl p-5 flex flex-col items-center">
                    <span className="text-3xl mb-1">🔥</span>
                    <span className="font-[var(--font-space-grotesk)] font-bold text-2xl text-gray-900">
                        {profileStats.currentStreakDayCount}
                    </span>
                    <span className="text-xs text-gray-400 uppercase tracking-wider">
                        Стрик
                    </span>
                </div>

                <div className="bg-[#F7F7F7] rounded-2xl p-5 flex flex-col items-center">
                    <span className="text-3xl mb-1">⚡</span>
                    <span className="font-[var(--font-space-grotesk)] font-bold text-2xl text-[#1CB0F6]">
                        {profileStats.totalXpAmount}
                    </span>
                    <span className="text-xs text-gray-400 uppercase tracking-wider">
                        Всего XP
                    </span>
                </div>

                <div className="bg-[#F7F7F7] rounded-2xl p-5 flex flex-col items-center">
                    <span className="text-3xl mb-1">🏅</span>
                    <span className="font-[var(--font-space-grotesk)] font-bold text-2xl text-gray-900">
                        {profileStats.longestStreakDayCount}
                    </span>
                    <span className="text-xs text-gray-400 uppercase tracking-wider">
                        Рекорд стрика
                    </span>
                </div>

                <div className="bg-[#F7F7F7] rounded-2xl p-5 flex flex-col items-center">
                    <span className="text-3xl mb-1">🎯</span>
                    <span className="font-[var(--font-space-grotesk)] font-bold text-2xl text-[#FFC800]">
                        {profileStats.averageExerciseScore}%
                    </span>
                    <span className="text-xs text-gray-400 uppercase tracking-wider">
                        Средний балл
                    </span>
                </div>
            </div>

            <div className="bg-[#F7F7F7] rounded-2xl p-5 mb-6">
                <div className="flex items-center justify-between mb-3">
                    <span className="font-semibold text-gray-700">Навыки пройдено</span>
                    <span className="font-bold text-gray-900">
                        {profileStats.completedSkillCount} / {profileStats.totalSkillCount}
                    </span>
                </div>
                <div className="h-3 bg-gray-200 rounded-full overflow-hidden">
                    <div
                        className="h-full bg-[#58CC02] rounded-full transition-all duration-500"
                        style={{ width: `${completionPercent}%` }}
                    />
                </div>
            </div>

            {isAdmin && (
                <Link
                    href="/admin/skills"
                    className="block w-full py-3 rounded-2xl text-center text-gray-500 hover:text-gray-800 font-semibold transition-colors mb-2"
                >
                    Admin Panel
                </Link>
            )}

            <button
                onClick={() => logoutMutation.mutate()}
                disabled={logoutMutation.isPending}
                className="w-full py-3 rounded-2xl text-gray-400 hover:text-gray-600 font-semibold transition-colors"
            >
                Выйти
            </button>
        </div>
    );
}
