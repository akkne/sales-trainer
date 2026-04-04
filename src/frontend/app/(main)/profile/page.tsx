"use client";

import Link from "next/link";
import { useProfile } from "@/lib/hooks/useProfile";
import { useLogout } from "@/lib/hooks/useAuth";
import { useAuthStore } from "@/lib/store/authStore";
import { useSkills } from "@/lib/hooks/useSkillTree";
import { useSelectedSkillStore } from "@/lib/store/selectedSkillStore";

export default function ProfilePage() {
    const { data: profileStats, isLoading: profileLoading } = useProfile();
    const { data: allSkills, isLoading: skillsLoading } = useSkills();
    const logoutMutation = useLogout();
    const { authenticatedUser } = useAuthStore();
    const { selectedSkill, setSelectedSkill, clearSelectedSkill } = useSelectedSkillStore();
    const isAdmin =
        authenticatedUser?.role === "Admin" || authenticatedUser?.role === "SuperAdmin";

    if (profileLoading) {
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
            {/* User header */}
            <div className="flex items-center justify-between mb-8">
                <div>
                    <h1 className="font-extrabold text-2xl text-gray-900">
                        {profileStats.displayName}
                    </h1>
                    <p className="text-sm text-[#AFAFAF]">{profileStats.email}</p>
                </div>
                <div className="w-14 h-14 rounded-full bg-[#58CC02] flex items-center justify-center text-white font-bold text-xl">
                    {profileStats.displayName[0]?.toUpperCase()}
                </div>
            </div>

            {/* Stats grid */}
            <div className="grid grid-cols-2 gap-3 mb-6">
                <div className="bg-[#F7F7F7] rounded-2xl p-5 flex flex-col items-center">
                    <span className="text-3xl mb-1">🔥</span>
                    <span className="font-bold text-2xl text-gray-900">
                        {profileStats.currentStreakDayCount}
                    </span>
                    <span className="text-xs text-[#AFAFAF] uppercase tracking-wider">Стрик</span>
                </div>

                <div className="bg-[#F7F7F7] rounded-2xl p-5 flex flex-col items-center">
                    <span className="text-3xl mb-1">⚡</span>
                    <span className="font-bold text-2xl text-[#1CB0F6]">
                        {profileStats.totalXpAmount}
                    </span>
                    <span className="text-xs text-[#AFAFAF] uppercase tracking-wider">Всего XP</span>
                </div>

                <div className="bg-[#F7F7F7] rounded-2xl p-5 flex flex-col items-center">
                    <span className="text-3xl mb-1">🏅</span>
                    <span className="font-bold text-2xl text-gray-900">
                        {profileStats.longestStreakDayCount}
                    </span>
                    <span className="text-xs text-[#AFAFAF] uppercase tracking-wider">Рекорд</span>
                </div>

                <div className="bg-[#F7F7F7] rounded-2xl p-5 flex flex-col items-center">
                    <span className="text-3xl mb-1">🎯</span>
                    <span className="font-bold text-2xl text-[#FFC800]">
                        {profileStats.averageExerciseScore}%
                    </span>
                    <span className="text-xs text-[#AFAFAF] uppercase tracking-wider">Средний балл</span>
                </div>
            </div>

            {/* Skills progress bar */}
            <div className="bg-[#F7F7F7] rounded-2xl p-5 mb-6">
                <div className="flex items-center justify-between mb-3">
                    <span className="font-semibold text-gray-700">Навыки пройдено</span>
                    <span className="font-bold text-gray-900">
                        {profileStats.completedSkillCount} / {profileStats.totalSkillCount}
                    </span>
                </div>
                <div className="h-3 bg-[#E5E5E5] rounded-full overflow-hidden">
                    <div
                        className="h-full bg-[#58CC02] rounded-full transition-all duration-500"
                        style={{ width: `${completionPercent}%` }}
                    />
                </div>
            </div>

            {/* ── Skill picker ─────────────────────────────────────────────── */}
            <div className="mb-6">
                <div className="flex items-center justify-between mb-3">
                    <h2 className="font-bold text-gray-900">Выберите навык для изучения</h2>
                    {selectedSkill && (
                        <button
                            onClick={clearSelectedSkill}
                            className="text-xs text-[#AFAFAF] hover:text-gray-500 transition-colors"
                        >
                            Сбросить
                        </button>
                    )}
                </div>

                {skillsLoading ? (
                    /* Loading skeleton */
                    <div className="flex flex-col gap-2">
                        {[1, 2, 3].map((i) => (
                            <div
                                key={i}
                                className="h-16 rounded-2xl bg-[#F7F7F7] animate-pulse"
                            />
                        ))}
                    </div>
                ) : !allSkills || allSkills.length === 0 ? (
                    /* Empty state — no skills in the system yet */
                    <div className="bg-[#F7F7F7] rounded-2xl px-5 py-8 text-center">
                        <p className="text-3xl mb-2">📚</p>
                        <p className="text-sm font-semibold text-gray-500">
                            Навыки ещё не добавлены
                        </p>
                        <p className="text-xs text-[#AFAFAF] mt-1">
                            Попросите администратора добавить навыки
                        </p>
                    </div>
                ) : (
                    <div className="flex flex-col gap-2">
                        {allSkills.map((skill) => {
                            const isSelected = selectedSkill?.slug === skill.slug;
                            const isLocked = skill.status === "locked";

                            return (
                                <button
                                    key={skill.skillId}
                                    onClick={() => {
                                        if (isLocked) return;
                                        setSelectedSkill({
                                            slug: skill.slug,
                                            title: skill.title,
                                            iconName: skill.iconName,
                                        });
                                    }}
                                    disabled={isLocked}
                                    className={`flex items-center gap-4 px-4 py-3 rounded-2xl text-left transition-all border-2 ${
                                        isSelected
                                            ? "border-[#58CC02] bg-[#E8F9D6]"
                                            : isLocked
                                              ? "border-transparent bg-[#F7F7F7] opacity-50 cursor-not-allowed"
                                              : "border-transparent bg-[#F7F7F7] hover:bg-[#E8F9D6] hover:border-[#58CC02] cursor-pointer"
                                    }`}
                                >
                                    <span className="text-2xl shrink-0">
                                        {skill.iconName || "📚"}
                                    </span>
                                    <div className="flex-1 min-w-0">
                                        <p
                                            className={`font-semibold truncate ${
                                                isSelected
                                                    ? "text-[#3C8400]"
                                                    : isLocked
                                                      ? "text-[#AFAFAF]"
                                                      : "text-gray-800"
                                            }`}
                                        >
                                            {skill.title}
                                        </p>
                                        <p className="text-xs text-[#AFAFAF]">
                                            {isLocked
                                                ? "🔒 Заблокировано"
                                                : `${skill.completedLessonCount}/${skill.totalLessonCount} уроков`}
                                        </p>
                                    </div>
                                    {isSelected && (
                                        <span className="text-[#58CC02] font-bold text-lg shrink-0">
                                            ✓
                                        </span>
                                    )}
                                    {isLocked && (
                                        <svg
                                            className="w-4 h-4 text-[#AFAFAF] shrink-0"
                                            fill="currentColor"
                                            viewBox="0 0 24 24"
                                        >
                                            <path d="M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z" />
                                        </svg>
                                    )}
                                </button>
                            );
                        })}
                    </div>
                )}

                {/* Hint when a skill is selected */}
                {selectedSkill && (
                    <p className="text-xs text-[#AFAFAF] mt-3 text-center">
                        Выбранный навык отображается на вкладке{" "}
                        <Link href="/tree" className="text-[#58CC02] font-semibold">
                            Путь
                        </Link>
                    </p>
                )}
            </div>
            {/* ── end skill picker ─────────────────────────────────────────── */}

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
                className="w-full py-3 rounded-2xl text-[#AFAFAF] hover:text-gray-600 font-semibold transition-colors"
            >
                Выйти
            </button>
        </div>
    );
}
