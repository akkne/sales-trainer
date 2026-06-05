"use client";

import Link from "next/link";
import { useProfile } from "@/features/profile/hooks/use-profile";
import { useAchievements } from "@/features/achievements/hooks/use-achievements";
import { useLogout } from "@/features/auth/hooks/use-auth";
import { useAuthStore } from "@/stores/auth-store";
import { useSkills, useUpdateEnrolledSkills } from "@/features/skills/hooks/use-skill-tree";
import { Icon } from "@/shared/components/icon";

const ALWAYS_ENROLLED_SLUG = "sales-basics";

const PERSONA_LABELS: Record<string, string> = {
    sdr: "SDR",
    account_executive: "Account Executive",
    account_manager: "Account Manager",
    founder: "Основатель",
    other: "Другое",
};

export default function ProfilePage() {
    const { data: profileStats, isLoading: profileLoading } = useProfile();
    const { data: achievements } = useAchievements();
    const { data: allSkills, isLoading: skillsLoading } = useSkills();
    const logoutMutation = useLogout();
    const updateEnrolledMutation = useUpdateEnrolledSkills();
    const { authenticatedUser } = useAuthStore();
    const isAdmin =
        authenticatedUser?.role === "Admin" || authenticatedUser?.role === "SuperAdmin";

    if (profileLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-primary border-t-transparent animate-spin" />
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
        if (slug === ALWAYS_ENROLLED_SLUG) return;
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
                    <h1 className="font-headline font-bold text-2xl text-on-surface">
                        {profileStats.displayName}
                    </h1>
                    <p className="text-sm text-on-surface-variant">{profileStats.email}</p>
                    {profileStats.persona && (
                        <span className="inline-flex items-center gap-1 mt-2 px-3 py-1 rounded-full bg-primary-container text-primary text-xs font-semibold">
                            <Icon name="badge" size="sm" />
                            {PERSONA_LABELS[profileStats.persona] ?? profileStats.persona}
                        </span>
                    )}
                </div>
                <div className="w-16 h-16 rounded-full bg-primary flex items-center justify-center text-on-primary font-bold text-2xl ring-4 ring-primary-container">
                    {profileStats.displayName[0]?.toUpperCase()}
                </div>
            </div>

            {/* Stats grid */}
            <div className="grid grid-cols-2 gap-3 mb-6">
                <div className="bg-surface-container rounded-2xl p-5 flex flex-col items-center">
                    <div className="w-10 h-10 rounded-full bg-error-container flex items-center justify-center mb-2">
                        <Icon name="local_fire_department" size="md" className="text-error" />
                    </div>
                    <span className="font-headline font-bold text-2xl text-on-surface">
                        {profileStats.currentStreakDayCount}
                    </span>
                    <span className="text-xs text-on-surface-variant uppercase tracking-wider">Стрик</span>
                </div>

                <div className="bg-primary-container rounded-2xl p-5 flex flex-col items-center">
                    <div className="w-10 h-10 rounded-full bg-primary flex items-center justify-center mb-2">
                        <Icon name="bolt" size="md" className="text-on-primary" />
                    </div>
                    <span className="font-headline font-bold text-2xl text-primary">
                        {profileStats.totalXpAmount}
                    </span>
                    <span className="text-xs text-on-primary-container uppercase tracking-wider">Всего XP</span>
                </div>

                <div className="bg-surface-container rounded-2xl p-5 flex flex-col items-center">
                    <div className="w-10 h-10 rounded-full bg-secondary-container flex items-center justify-center mb-2">
                        <Icon name="emoji_events" size="md" className="text-secondary" />
                    </div>
                    <span className="font-headline font-bold text-2xl text-on-surface">
                        {profileStats.longestStreakDayCount}
                    </span>
                    <span className="text-xs text-on-surface-variant uppercase tracking-wider">Рекорд</span>
                </div>

                <div className="bg-surface-container rounded-2xl p-5 flex flex-col items-center">
                    <div className="w-10 h-10 rounded-full bg-tertiary-container flex items-center justify-center mb-2">
                        <Icon name="target" size="md" className="text-tertiary" />
                    </div>
                    <span className="font-headline font-bold text-2xl text-tertiary">
                        {profileStats.averageExerciseScore}%
                    </span>
                    <span className="text-xs text-on-surface-variant uppercase tracking-wider">Средний балл</span>
                </div>
            </div>

            {/* Skills progress bar */}
            <div className="bg-surface-container rounded-2xl p-5 mb-6">
                <div className="flex items-center justify-between mb-3">
                    <span className="font-semibold text-on-surface">Навыки пройдено</span>
                    <span className="font-bold text-on-surface">
                        {profileStats.completedSkillCount} / {profileStats.totalSkillCount}
                    </span>
                </div>
                <div className="h-3 bg-surface-container-highest rounded-full overflow-hidden">
                    <div
                        className="h-full bg-primary rounded-full transition-all duration-500"
                        style={{ width: `${completionPercent}%` }}
                    />
                </div>
            </div>

            {/* ── Achievements ─────────────────────────────────────────────── */}
            {achievements && achievements.length > 0 && (
                <div className="mb-6">
                    <h2 className="font-bold text-on-surface text-lg mb-4">Достижения</h2>
                    <div className="grid grid-cols-5 gap-2">
                        {achievements.map((achievement) => (
                            <div
                                key={achievement.achievementId}
                                title={`${achievement.title}: ${achievement.description}`}
                                className={`flex flex-col items-center gap-1 p-3 rounded-2xl text-center tonal-transition ${
                                    achievement.isUnlocked
                                        ? "bg-primary-container"
                                        : "bg-surface-container opacity-40 grayscale"
                                }`}
                            >
                                <span className="text-2xl">{achievement.iconEmoji}</span>
                                <span className="text-[10px] font-semibold text-on-surface leading-tight">
                                    {achievement.title}
                                </span>
                            </div>
                        ))}
                    </div>
                    <p className="text-xs text-on-surface-variant mt-2 text-center">
                        {achievements.filter((a) => a.isUnlocked).length} из {achievements.length} разблокировано
                    </p>
                </div>
            )}

            {/* ── My Skills (enrollment manager) ──────────────────────────── */}
            <div className="mb-6">
                <div className="flex items-center justify-between mb-4">
                    <h2 className="font-bold text-on-surface text-lg">Мои навыки</h2>
                    <span className="text-xs text-on-surface-variant">
                        Включи навыки для изучения
                    </span>
                </div>

                {skillsLoading ? (
                    <div className="flex flex-col gap-3">
                        {[1, 2, 3].map((i) => (
                            <div key={i} className="h-16 rounded-2xl bg-surface-container animate-pulse" />
                        ))}
                    </div>
                ) : !allSkills || allSkills.length === 0 ? (
                    <div className="bg-surface-container rounded-2xl px-5 py-8 text-center">
                        <div className="w-12 h-12 rounded-full bg-surface-container-high flex items-center justify-center mx-auto mb-3">
                            <Icon name="school" size="lg" className="text-on-surface-variant" />
                        </div>
                        <p className="text-sm font-semibold text-on-surface-variant">
                            Навыки ещё не добавлены
                        </p>
                        <p className="text-xs text-on-surface-variant mt-1">
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
                                    className={`flex items-center gap-4 px-5 py-4 rounded-2xl text-left tonal-transition border-2 ${
                                        isAlwaysOn
                                            ? "border-outline-variant bg-surface-container-low cursor-default opacity-60"
                                            : isEnrolled
                                            ? "border-primary bg-primary-container cursor-pointer hover:opacity-90"
                                            : "border-transparent bg-surface-container cursor-pointer hover:bg-surface-container-high"
                                    }`}
                                >
                                    {/* Info */}
                                    <div className="flex-1 min-w-0">
                                        <p
                                            className={`font-semibold truncate ${
                                                isAlwaysOn
                                                    ? "text-on-surface-variant"
                                                    : isEnrolled
                                                    ? "text-primary"
                                                    : "text-on-surface"
                                            }`}
                                        >
                                            {skill.title}
                                        </p>
                                        <p className="text-xs text-on-surface-variant mt-0.5">
                                            {isAlwaysOn
                                                ? "Базовый — всегда включён"
                                                : isEnrolled
                                                ? `${skill.completedLessonCount}/${skill.totalLessonCount} уроков пройдено`
                                                : "Нажми, чтобы добавить"}
                                        </p>
                                    </div>

                                    {/* Toggle switch */}
                                    <div
                                        className={`w-12 h-6 rounded-full tonal-transition shrink-0 flex items-center px-1 ${
                                            isAlwaysOn
                                                ? "bg-outline-variant"
                                                : isEnrolled
                                                ? "bg-primary"
                                                : "bg-surface-container-highest"
                                        }`}
                                    >
                                        <div
                                            className={`w-4 h-4 rounded-full bg-surface-container-lowest shadow transition-transform ${
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
                    <p className="mt-3 text-xs text-error text-center">
                        Не удалось сохранить изменения. Попробуй ещё раз.
                    </p>
                )}

                <p className="text-xs text-on-surface-variant mt-3 text-center">
                    Выбранные навыки доступны на вкладке{" "}
                    <Link href="/tree" className="text-primary font-semibold">
                        Путь
                    </Link>
                </p>
            </div>

            {/* Admin link */}
            {isAdmin && (
                <Link
                    href="/admin/skills"
                    className="flex items-center justify-center gap-2 w-full py-3 rounded-2xl bg-surface-container text-on-surface-variant hover:text-on-surface font-semibold tonal-transition mb-2"
                >
                    <Icon name="admin_panel_settings" size="md" />
                    Панель администратора
                </Link>
            )}

            {/* Logout button */}
            <button
                onClick={() => logoutMutation.mutate()}
                disabled={logoutMutation.isPending}
                className="w-full py-3 rounded-2xl text-on-surface-variant hover:text-error font-semibold tonal-transition flex items-center justify-center gap-2"
            >
                <Icon name="logout" size="md" />
                Выйти
            </button>
        </div>
    );
}
