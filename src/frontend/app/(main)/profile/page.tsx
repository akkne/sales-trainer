"use client";

import Link from "next/link";
import { useProfile } from "@/lib/hooks/useProfile";
import { useLogout } from "@/lib/hooks/useAuth";
import { useAuthStore } from "@/lib/store/authStore";
import { useSkills, useUpdateEnrolledSkills } from "@/lib/hooks/useSkillTree";

const ALWAYS_ENROLLED_SLUG = "sales-basics";

export default function ProfilePage() {
    const { data: profileStats, isLoading: profileLoading } = useProfile();
    const { data: allSkills, isLoading: skillsLoading } = useSkills();
    const logoutMutation = useLogout();
    const updateEnrolledMutation = useUpdateEnrolledSkills();
    const { authenticatedUser } = useAuthStore();
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

    /** Enrolled = has an active (non-locked) progress record */
    const enrolledSlugs = new Set(
        (allSkills ?? [])
            .filter((s) => s.status !== "locked")
            .map((s) => s.slug)
    );

    function toggleEnrollment(slug: string) {
        if (slug === ALWAYS_ENROLLED_SLUG) return; // can never unenroll from basics
        const next = new Set(enrolledSlugs);
        if (next.has(slug)) next.delete(slug);
        else next.add(slug);
        updateEnrolledMutation.mutate(Array.from(next));
    }

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

            {/* ── My Skills (enrollment manager) ──────────────────────────── */}
            <div className="mb-6">
                <div className="flex items-center justify-between mb-4">
                    <h2 className="font-bold text-gray-900 text-lg">Мои навыки</h2>
                    <span className="text-xs text-[#AFAFAF]">
                        Включи навыки, которые хочешь изучать
                    </span>
                </div>

                {skillsLoading ? (
                    <div className="flex flex-col gap-3">
                        {[1, 2, 3].map((i) => (
                            <div key={i} className="h-16 rounded-2xl bg-[#F7F7F7] animate-pulse" />
                        ))}
                    </div>
                ) : !allSkills || allSkills.length === 0 ? (
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
                    <div className="flex flex-col gap-3">
                        {allSkills.map((skill) => {
                            const isAlwaysOn = skill.slug === ALWAYS_ENROLLED_SLUG;
                            const isEnrolled = enrolledSlugs.has(skill.slug);
                            const isSaving = updateEnrolledMutation.isPending;

                            return (
                                <button
                                    key={skill.skillId}
                                    onClick={() => toggleEnrollment(skill.slug)}
                                    disabled={isAlwaysOn || isSaving}
                                    className={`flex items-center gap-4 px-5 py-4 rounded-2xl text-left transition-all border-2 ${
                                        isAlwaysOn
                                            ? "border-gray-200 bg-gray-50 cursor-default"
                                            : isEnrolled
                                            ? "border-[#58CC02] bg-[#E8F9D6] cursor-pointer hover:border-[#58CC02]/70"
                                            : "border-transparent bg-[#F7F7F7] cursor-pointer hover:border-[#58CC02]/40"
                                    }`}
                                >
                                    {/* Info */}
                                    <div className="flex-1 min-w-0">
                                        <p
                                            className={`font-semibold truncate ${
                                                isAlwaysOn
                                                    ? "text-gray-400"
                                                    : isEnrolled
                                                    ? "text-[#3C8400]"
                                                    : "text-gray-700"
                                            }`}
                                        >
                                            {skill.title}
                                        </p>
                                        <p className="text-xs text-[#AFAFAF] mt-0.5">
                                            {isAlwaysOn
                                                ? "Базовый — всегда включён"
                                                : isEnrolled
                                                ? `${skill.completedLessonCount}/${skill.totalLessonCount} уроков пройдено`
                                                : "Нажми, чтобы добавить"}
                                        </p>
                                    </div>

                                    {/* Toggle switch */}
                                    <div
                                        className={`w-12 h-6 rounded-full transition-colors shrink-0 flex items-center px-1 ${
                                            isAlwaysOn
                                                ? "bg-gray-300"
                                                : isEnrolled
                                                ? "bg-[#58CC02]"
                                                : "bg-gray-200"
                                        }`}
                                    >
                                        <div
                                            className={`w-4 h-4 rounded-full bg-white shadow transition-transform ${
                                                isAlwaysOn || isEnrolled
                                                    ? "translate-x-6"
                                                    : "translate-x-0"
                                            }`}
                                        />
                                    </div>
                                </button>
                            );
                        })}
                    </div>
                )}

                {updateEnrolledMutation.isError && (
                    <p className="mt-3 text-xs text-red-500 text-center">
                        Не удалось сохранить изменения. Попробуй ещё раз.
                    </p>
                )}

                <p className="text-xs text-[#AFAFAF] mt-3 text-center">
                    Выбранные навыки доступны на вкладке{" "}
                    <Link href="/tree" className="text-[#58CC02] font-semibold">
                        Путь
                    </Link>
                </p>
            </div>
            {/* ── end My Skills ──────────────────────────────────────────── */}

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
