"use client";

import Link from "next/link";
import { useProfile } from "@/lib/hooks/useProfile";
import { useAchievements } from "@/lib/hooks/useAchievements";
import { useLogout } from "@/lib/hooks/useAuth";
import { useAuthStore } from "@/lib/store/authStore";
import { useSkills, useUpdateEnrolledSkills } from "@/lib/hooks/useSkillTree";
import { useVoiceUsage } from "@/lib/hooks/useVoice";
import { Icon, IconName } from "@/components/ui/Icon";
import { ThemeToggle } from "@/components/ui/ThemeToggle";

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
    const { data: voiceUsage } = useVoiceUsage();
    const { authenticatedUser } = useAuthStore();
    const isAdmin =
        authenticatedUser?.role === "Admin" || authenticatedUser?.role === "SuperAdmin";

    if (profileLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen bg-bg">
                <div className="w-12 h-12 rounded-full border-3 border-line-2 border-t-indigo animate-spin" />
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

    const unlockedAchievements = achievements?.filter((a) => a.isUnlocked) ?? [];

    return (
        <div className="min-h-screen bg-bg pb-20">
            {/* Header */}
            <div className="bg-surface border-b border-line px-6 py-6 md:px-8">
                <div className="max-w-2xl mx-auto flex items-center gap-5">
                    {/* Avatar */}
                    <div
                        className="w-20 h-20 rounded-2xl flex items-center justify-center text-white font-medium text-3xl shrink-0"
                        style={{ background: "var(--indigo)", boxShadow: "var(--sh-2)" }}
                    >
                        {profileStats.displayName[0]?.toUpperCase()}
                    </div>
                    <div className="flex-1 min-w-0">
                        <h1 className="text-2xl md:text-3xl font-medium tracking-tight text-ink truncate">
                            {profileStats.displayName}
                        </h1>
                        <p className="text-sm text-ink-3 mt-0.5">{profileStats.email}</p>
                        {profileStats.persona && (
                            <span
                                className="inline-flex items-center gap-1.5 mt-2 px-3 py-1 rounded-full text-xs font-medium"
                                style={{ background: "var(--indigo-soft)", color: "var(--indigo)" }}
                            >
                                {PERSONA_LABELS[profileStats.persona] ?? profileStats.persona}
                            </span>
                        )}
                    </div>
                </div>
            </div>

            <div className="max-w-2xl mx-auto px-4 md:px-6 py-6">
                {/* Stats grid */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-6">
                    <StatTile
                        icon="flame"
                        label="Стрик"
                        value={profileStats.currentStreakDayCount}
                        tone="rust"
                    />
                    <StatTile
                        icon="bolt"
                        label="Всего XP"
                        value={profileStats.totalXpAmount.toLocaleString()}
                        tone="indigo"
                    />
                    <StatTile
                        icon="trophy"
                        label="Рекорд"
                        value={profileStats.longestStreakDayCount}
                        unit="дн"
                        tone="olive"
                    />
                    <StatTile
                        icon="target"
                        label="Точность"
                        value={profileStats.averageExerciseScore}
                        unit="%"
                        tone="clay"
                    />
                </div>

                {/* Skills progress bar */}
                <div
                    className="bg-surface border border-line rounded-2xl p-5 mb-6"
                    style={{ boxShadow: "var(--sh-1)" }}
                >
                    <div className="flex items-center justify-between mb-3">
                        <span className="text-sm font-medium text-ink">Навыки пройдено</span>
                        <span className="font-mono text-sm text-ink-2">
                            {profileStats.completedSkillCount} / {profileStats.totalSkillCount}
                        </span>
                    </div>
                    <div className="h-2 bg-bg-2 rounded-full overflow-hidden">
                        <div
                            className="h-full rounded-full transition-all duration-500"
                            style={{ width: `${completionPercent}%`, background: "var(--indigo)" }}
                        />
                    </div>
                </div>

                {/* Voice minutes */}
                {voiceUsage && (voiceUsage.dailyLimitSeconds > 0 || voiceUsage.monthlyLimitSeconds > 0) && (
                    <div
                        className="bg-surface border border-line rounded-2xl p-5 mb-6"
                        style={{ boxShadow: "var(--sh-1)" }}
                    >
                        <div className="flex items-center gap-2 mb-4">
                            <div className="w-8 h-8 rounded-xl bg-rust-soft flex items-center justify-center text-rust">
                                <Icon name="mic" size="sm" />
                            </div>
                            <span className="text-sm font-medium text-ink">Голосовые звонки</span>
                        </div>
                        <div className="space-y-4">
                            {voiceUsage.dailyLimitSeconds > 0 && (
                                <VoiceQuotaBar
                                    label="Сегодня"
                                    usedSeconds={voiceUsage.dailyUsedSeconds}
                                    limitSeconds={voiceUsage.dailyLimitSeconds}
                                />
                            )}
                            {voiceUsage.monthlyLimitSeconds > 0 && (
                                <VoiceQuotaBar
                                    label="В этом месяце"
                                    usedSeconds={voiceUsage.monthlyUsedSeconds}
                                    limitSeconds={voiceUsage.monthlyLimitSeconds}
                                />
                            )}
                        </div>
                    </div>
                )}

                {/* Achievements */}
                {achievements && achievements.length > 0 && (
                    <div className="mb-6">
                        <div className="flex items-center justify-between mb-4">
                            <h2 className="font-medium text-ink">Достижения</h2>
                            <span className="text-xs text-ink-4 font-mono">
                                {unlockedAchievements.length}/{achievements.length}
                            </span>
                        </div>
                        <div className="grid grid-cols-5 gap-2">
                            {achievements.map((achievement) => (
                                <AchievementBadge key={achievement.achievementId} achievement={achievement} />
                            ))}
                        </div>
                    </div>
                )}

                {/* My Skills */}
                <div className="mb-6">
                    <div className="flex items-center justify-between mb-4">
                        <h2 className="font-medium text-ink">Мои навыки</h2>
                        <span className="text-xs text-ink-4">Включи навыки для изучения</span>
                    </div>

                    {skillsLoading ? (
                        <div className="flex flex-col gap-3">
                            {[1, 2, 3].map((i) => (
                                <div key={i} className="h-16 rounded-2xl bg-surface animate-pulse" />
                            ))}
                        </div>
                    ) : !allSkills || allSkills.length === 0 ? (
                        <div
                            className="bg-surface border border-line rounded-2xl px-5 py-8 text-center"
                            style={{ boxShadow: "var(--sh-1)" }}
                        >
                            <div className="w-12 h-12 rounded-xl bg-bg-2 flex items-center justify-center mx-auto mb-3">
                                <Icon name="book" size="lg" className="text-ink-4" />
                            </div>
                            <p className="text-sm font-medium text-ink-3">Навыки ещё не добавлены</p>
                            <p className="text-xs text-ink-4 mt-1">
                                Попросите администратора добавить навыки
                            </p>
                        </div>
                    ) : (
                        <div className="flex flex-col gap-2">
                            {allSkills.map((skill) => {
                                const isAlwaysOn = skill.slug === ALWAYS_ENROLLED_SLUG;
                                const isEnrolled = enrolledSlugs.has(skill.slug);
                                const isSaving = updateEnrolledMutation.isPending;

                                return (
                                    <SkillToggle
                                        key={skill.skillId}
                                        title={skill.title}
                                        subtitle={
                                            isAlwaysOn
                                                ? "Базовый — всегда включён"
                                                : isEnrolled
                                                ? `${skill.completedLessonCount}/${skill.totalLessonCount} уроков`
                                                : "Нажми, чтобы добавить"
                                        }
                                        isAlwaysOn={isAlwaysOn}
                                        isEnrolled={isEnrolled}
                                        disabled={isAlwaysOn || isSaving}
                                        onClick={() => toggleEnrollment(skill.slug)}
                                    />
                                );
                            })}
                        </div>
                    )}

                    {updateEnrolledMutation.isError && (
                        <p className="mt-3 text-xs text-bad text-center">
                            Не удалось сохранить изменения. Попробуй ещё раз.
                        </p>
                    )}

                    <p className="text-xs text-ink-4 mt-3 text-center">
                        Выбранные навыки доступны на вкладке{" "}
                        <Link href="/tree" className="text-indigo font-medium">
                            Путь
                        </Link>
                    </p>
                </div>

                {/* Settings Section */}
                <div className="border-t border-line pt-6 space-y-3">
                    <ThemeToggle />

                    {isAdmin && (
                        <Link
                            href="/admin/skills"
                            className="flex items-center gap-3 px-5 py-4 rounded-2xl bg-surface border border-line hover:border-line-2 transition-colors mb-2"
                            style={{ boxShadow: "var(--sh-1)" }}
                        >
                            <div className="w-10 h-10 rounded-xl bg-indigo-soft flex items-center justify-center text-indigo">
                                <Icon name="settings" size="md" />
                            </div>
                            <div className="flex-1">
                                <p className="font-medium text-ink text-sm">Панель администратора</p>
                                <p className="text-xs text-ink-4">Управление контентом</p>
                            </div>
                            <Icon name="chevron-right" size="sm" className="text-ink-4" />
                        </Link>
                    )}

                    <button
                        onClick={() => logoutMutation.mutate()}
                        disabled={logoutMutation.isPending}
                        className="flex items-center gap-3 px-5 py-4 rounded-2xl bg-surface border border-line hover:border-bad/30 transition-colors w-full text-left"
                        style={{ boxShadow: "var(--sh-1)" }}
                    >
                        <div className="w-10 h-10 rounded-xl bg-bad-soft flex items-center justify-center text-bad">
                            <Icon name="arrow-left" size="md" />
                        </div>
                        <div className="flex-1">
                            <p className="font-medium text-ink text-sm">Выйти из аккаунта</p>
                            <p className="text-xs text-ink-4">Завершить сессию</p>
                        </div>
                    </button>
                </div>
            </div>
        </div>
    );
}

function VoiceQuotaBar({
    label,
    usedSeconds,
    limitSeconds,
}: {
    label: string;
    usedSeconds: number;
    limitSeconds: number;
}) {
    const usedMinutes = Math.round(usedSeconds / 60);
    const limitMinutes = Math.round(limitSeconds / 60);
    const percent = Math.min(100, Math.round((usedSeconds / limitSeconds) * 100));
    const isNearLimit = percent >= 80;
    const isExceeded = usedSeconds >= limitSeconds;
    const barColor = isExceeded ? "var(--bad)" : isNearLimit ? "var(--warn)" : "var(--olive)";

    return (
        <div>
            <div className="flex items-center justify-between mb-1.5">
                <span className="text-xs text-ink-3">{label}</span>
                <span className={`font-mono text-xs ${isExceeded ? "text-bad" : "text-ink-2"}`}>
                    {usedMinutes} / {limitMinutes} мин
                </span>
            </div>
            <div className="h-2 bg-bg-2 rounded-full overflow-hidden">
                <div
                    className="h-full rounded-full transition-all duration-500"
                    style={{ width: `${percent}%`, background: barColor }}
                />
            </div>
            {isExceeded && (
                <p className="text-[11px] text-bad mt-1.5">
                    Лимит исчерпан — звонки откроются {label === "Сегодня" ? "завтра" : "в следующем месяце"}
                </p>
            )}
        </div>
    );
}

function StatTile({
    icon,
    label,
    value,
    unit,
    tone = "neutral"
}: {
    icon: IconName;
    label: string;
    value: string | number;
    unit?: string;
    tone?: "neutral" | "rust" | "olive" | "indigo" | "clay";
}) {
    const toneStyles = {
        neutral: { bg: "bg-surface", iconBg: "bg-bg-2", iconColor: "text-ink-3", valueColor: "text-ink" },
        rust: { bg: "bg-rust-soft", iconBg: "bg-rust", iconColor: "text-white", valueColor: "text-rust" },
        olive: { bg: "bg-olive-soft", iconBg: "bg-olive", iconColor: "text-white", valueColor: "text-olive" },
        indigo: { bg: "bg-indigo-soft", iconBg: "bg-indigo", iconColor: "text-white", valueColor: "text-indigo" },
        clay: { bg: "bg-surface", iconBg: "bg-clay", iconColor: "text-white", valueColor: "text-clay" },
    };
    const style = toneStyles[tone];

    return (
        <div
            className={`${style.bg} border border-line rounded-2xl p-4 flex flex-col items-center`}
            style={{ boxShadow: "var(--sh-1)" }}
        >
            <div className={`w-9 h-9 rounded-xl ${style.iconBg} ${style.iconColor} flex items-center justify-center mb-2`}>
                <Icon name={icon} size="sm" />
            </div>
            <div className={`text-2xl font-medium tabular-nums ${style.valueColor} flex items-baseline gap-0.5`}>
                {value}
                {unit && <span className="text-sm text-ink-4">{unit}</span>}
            </div>
            <span className="text-[10px] font-mono tracking-[1px] uppercase text-ink-4 mt-1">{label}</span>
        </div>
    );
}

function AchievementBadge({ achievement }: { achievement: { achievementId: string; title: string; description: string; iconEmoji: string; isUnlocked: boolean } }) {
    return (
        <div
            title={`${achievement.title}: ${achievement.description}`}
            className={`flex flex-col items-center gap-1.5 p-3 rounded-2xl text-center transition-all ${
                achievement.isUnlocked
                    ? "bg-indigo-soft"
                    : "bg-surface opacity-40 grayscale"
            }`}
            style={achievement.isUnlocked ? {} : { filter: "grayscale(1)" }}
        >
            <span className="text-2xl">{achievement.iconEmoji}</span>
            <span className="text-[10px] font-medium text-ink leading-tight line-clamp-2">
                {achievement.title}
            </span>
        </div>
    );
}

function SkillToggle({
    title,
    subtitle,
    isAlwaysOn,
    isEnrolled,
    disabled,
    onClick
}: {
    title: string;
    subtitle: string;
    isAlwaysOn: boolean;
    isEnrolled: boolean;
    disabled: boolean;
    onClick: () => void;
}) {
    return (
        <button
            onClick={onClick}
            disabled={disabled}
            className={`flex items-center gap-4 px-4 py-3 rounded-2xl text-left transition-all border ${
                isAlwaysOn
                    ? "border-line bg-bg-2 cursor-default opacity-60"
                    : isEnrolled
                    ? "border-indigo bg-indigo-soft cursor-pointer hover:opacity-90"
                    : "border-line bg-surface cursor-pointer hover:border-line-2"
            }`}
            style={{ boxShadow: isEnrolled ? "none" : "var(--sh-1)" }}
        >
            <div className="flex-1 min-w-0">
                <p
                    className={`font-medium text-sm truncate ${
                        isAlwaysOn
                            ? "text-ink-3"
                            : isEnrolled
                            ? "text-indigo"
                            : "text-ink"
                    }`}
                >
                    {title}
                </p>
                <p className="text-xs text-ink-4 mt-0.5">{subtitle}</p>
            </div>

            {/* Toggle switch */}
            <div
                className={`w-11 h-6 rounded-full shrink-0 flex items-center px-0.5 transition-colors ${
                    isAlwaysOn
                        ? "bg-line-2"
                        : isEnrolled
                        ? "bg-indigo"
                        : "bg-line"
                }`}
            >
                <div
                    className={`w-5 h-5 rounded-full bg-white shadow transition-transform ${
                        isAlwaysOn || isEnrolled ? "translate-x-5" : "translate-x-0"
                    }`}
                />
            </div>
        </button>
    );
}
