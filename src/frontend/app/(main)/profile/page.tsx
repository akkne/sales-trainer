"use client";

import Link from "next/link";
import { useProfile } from "@/features/profile/hooks/use-profile";
import { useAchievements } from "@/features/achievements/hooks/use-achievements";
import { useLogout } from "@/features/auth/hooks/use-auth";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useThemeStore } from "@/shared/stores/theme-store";
import { useSkills, useUpdateEnrolledSkills } from "@/features/skills/hooks/use-skill-tree";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { StatTile } from "@/shared/components/stat-tile";
import { Progress } from "@/shared/components/progress";
import { useVoiceUsage } from "@/features/voice/hooks/use-voice-usage";

const ALWAYS_ENROLLED_SLUG = "sales-basics";

const PERSONA_LABELS: Record<string, string> = {
    sdr: "SDR",
    account_executive: "Account Executive",
    account_manager: "Account Manager",
    founder: "Основатель",
    other: "Другое",
};

type Theme = "light" | "dark" | "system";

const THEME_OPTIONS: { value: Theme; label: string; icon: IconName }[] = [
    { value: "light", label: "Светлая", icon: "sun" },
    { value: "dark", label: "Тёмная", icon: "moon" },
    { value: "system", label: "Системная", icon: "settings" },
];

export default function ProfilePage() {
    const { data: profileStats, isLoading: profileLoading } = useProfile();
    const { data: achievements } = useAchievements();
    const { data: allSkills, isLoading: skillsLoading } = useSkills();
    const logoutMutation = useLogout();
    const updateEnrolledMutation = useUpdateEnrolledSkills();
    const { data: voiceUsage } = useVoiceUsage();
    const { authenticatedUser } = useAuthStore();
    const { theme, setTheme } = useThemeStore();
    const isAdmin =
        authenticatedUser?.role === "Admin" || authenticatedUser?.role === "SuperAdmin";

    if (profileLoading) {
        return (
            <div className="page">
                <div className="app-backdrop" />
                <div className="center" style={{ minHeight: "60vh" }}>
                    <div
                        style={{
                            width: 48,
                            height: 48,
                            borderRadius: "50%",
                            border: "3px solid var(--line-2)",
                            borderTopColor: "var(--primary)",
                            animation: "spin 0.8s linear infinite",
                        }}
                    />
                </div>
            </div>
        );
    }

    if (!profileStats) return null;

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
    const personaLabel = profileStats.persona
        ? PERSONA_LABELS[profileStats.persona] ?? profileStats.persona
        : null;

    return (
        <div className="page">
            <div className="app-backdrop" />
            <div className="container" style={{ maxWidth: 760 }}>
                {/* Header */}
                <div className="profile-head">
                    <GeoAvatar seed={profileStats.displayName} size={88} />
                    <div className="grow">
                        <div className="row gap-3 wrap" style={{ alignItems: "center" }}>
                            <h1 className="h1" style={{ fontSize: 36 }}>
                                {profileStats.displayName}
                            </h1>
                            {personaLabel && (
                                <span className="chip primary">{personaLabel}</span>
                            )}
                        </div>
                        <p className="small" style={{ marginTop: 4 }}>
                            {profileStats.email}
                        </p>
                    </div>
                    <span
                        className="badge"
                        style={{
                            background: "var(--surface-2)",
                            color: "var(--ink-2)",
                            padding: "8px 14px",
                            fontSize: 13,
                        }}
                    >
                        <Icon name="trophy" size={16} style={{ color: "var(--amber)" }} />
                        {unlockedAchievements.length} достижений
                    </span>
                </div>

                {/* Stats grid */}
                <div className="profile-stats">
                    <StatTile
                        icon={<Icon name="flame" size={18} />}
                        tone="flame"
                        label="Стрик"
                        value={profileStats.currentStreakDayCount}
                        unit=" дн"
                    />
                    <StatTile
                        icon={<Icon name="bolt" size={18} />}
                        tone="primary"
                        label="Всего XP"
                        value={profileStats.totalXpAmount.toLocaleString("ru")}
                    />
                    <StatTile
                        icon={<Icon name="trophy" size={18} />}
                        tone="success"
                        label="Рекорд"
                        value={profileStats.longestStreakDayCount}
                        unit=" дн"
                    />
                    <StatTile
                        icon={<Icon name="target" size={18} />}
                        tone="amber"
                        label="Точность"
                        value={profileStats.averageExerciseScore}
                        unit="%"
                    />
                </div>

                {/* Skills progress */}
                <div className="card card-pad" style={{ marginTop: 16 }}>
                    <div className="row between">
                        <h4 className="h4">Навыки пройдено</h4>
                        <span className="num small">
                            {profileStats.completedSkillCount} / {profileStats.totalSkillCount}
                        </span>
                    </div>
                    <div style={{ marginTop: 14 }}>
                        <Progress
                            value={profileStats.completedSkillCount}
                            max={Math.max(1, profileStats.totalSkillCount)}
                        />
                    </div>
                </div>

                {/* Voice minutes */}
                {voiceUsage &&
                    (voiceUsage.dailyLimitSeconds > 0 ||
                        voiceUsage.monthlyLimitSeconds > 0) && (
                        <div className="card card-pad" style={{ marginTop: 16 }}>
                            <div className="row gap-3" style={{ marginBottom: 18 }}>
                                <span
                                    className="itile flame"
                                    style={{ width: 40, height: 40 }}
                                >
                                    <Icon name="mic" size={20} />
                                </span>
                                <h4 className="h4">Голосовые звонки</h4>
                            </div>
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
                                    style={{ marginTop: 14 }}
                                />
                            )}
                        </div>
                    )}

                {/* Achievements */}
                {achievements && achievements.length > 0 && (
                    <div className="card card-pad" style={{ marginTop: 16 }}>
                        <div className="row between" style={{ marginBottom: 18 }}>
                            <h4 className="h4">Достижения</h4>
                            <span className="num small">
                                {unlockedAchievements.length} / {achievements.length}
                            </span>
                        </div>
                        <div className="ach-grid">
                            {achievements.map((a) => (
                                <div
                                    key={a.achievementId}
                                    className={"ach" + (a.isUnlocked ? " on" : "")}
                                    title={`${a.title}: ${a.description}`}
                                >
                                    <span className="ach-ic" style={{ fontSize: 22 }}>
                                        {a.iconEmoji}
                                    </span>
                                    <span className="ach-name">{a.title}</span>
                                </div>
                            ))}
                        </div>
                    </div>
                )}

                {/* My Skills */}
                <div className="card card-pad" style={{ marginTop: 16 }}>
                    <div className="row between" style={{ marginBottom: 18 }}>
                        <h4 className="h4">Мои навыки</h4>
                        <span className="small">Включи навыки для изучения</span>
                    </div>

                    {skillsLoading ? (
                        <div className="col gap-3">
                            {[1, 2, 3].map((i) => (
                                <div
                                    key={i}
                                    style={{
                                        height: 64,
                                        borderRadius: "var(--r-md)",
                                        background: "var(--surface-2)",
                                        animation: "pulse 1.5s ease-in-out infinite",
                                    }}
                                />
                            ))}
                        </div>
                    ) : !allSkills || allSkills.length === 0 ? (
                        <div className="empty">
                            <span className="itile" style={{ width: 48, height: 48 }}>
                                <Icon name="book" size="lg" />
                            </span>
                            <p className="body" style={{ marginTop: 12 }}>
                                Навыки ещё не добавлены
                            </p>
                            <p className="small">Попросите администратора добавить навыки</p>
                        </div>
                    ) : (
                        <div className="col gap-2">
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
                        <p
                            className="small"
                            style={{ marginTop: 12, color: "var(--heart)", textAlign: "center" }}
                        >
                            Не удалось сохранить изменения. Попробуй ещё раз.
                        </p>
                    )}

                    <p className="small" style={{ marginTop: 12, textAlign: "center" }}>
                        Выбранные навыки доступны на вкладке{" "}
                        <Link href="/tree" style={{ color: "var(--primary)", fontWeight: 700 }}>
                            Путь
                        </Link>
                    </p>
                </div>

                {/* Theme */}
                <div className="card card-pad" style={{ marginTop: 16 }}>
                    <span className="eyebrow muted">Тема оформления</span>
                    <div className="theme-grid" style={{ marginTop: 14 }}>
                        {THEME_OPTIONS.map((option) => (
                            <button
                                key={option.value}
                                className={"theme-opt" + (theme === option.value ? " active" : "")}
                                onClick={() => setTheme(option.value)}
                            >
                                <Icon name={option.icon} size={22} />
                                <span>{option.label}</span>
                            </button>
                        ))}
                    </div>
                </div>

                {/* Admin */}
                {isAdmin && (
                    <Link
                        href="/admin/skills"
                        className="card card-pad logout-row"
                        style={{ marginTop: 16 }}
                    >
                        <span className="itile primary" style={{ width: 40, height: 40 }}>
                            <Icon name="settings" size={20} />
                        </span>
                        <div className="grow" style={{ textAlign: "left" }}>
                            <div className="h4">Панель администратора</div>
                            <div className="small">Управление контентом</div>
                        </div>
                        <Icon name="chevron-right" style={{ color: "var(--ink-4)" }} />
                    </Link>
                )}

                {/* Logout */}
                <button
                    className="card card-pad logout-row"
                    style={{ marginTop: 16 }}
                    onClick={() => logoutMutation.mutate()}
                    disabled={logoutMutation.isPending}
                >
                    <span className="itile heart" style={{ width: 40, height: 40 }}>
                        <Icon name="arrow-left" size={20} />
                    </span>
                    <div className="grow" style={{ textAlign: "left" }}>
                        <div className="h4" style={{ color: "var(--heart)" }}>
                            Выйти из аккаунта
                        </div>
                        <div className="small">Завершить сессию</div>
                    </div>
                    <Icon name="chevron-right" style={{ color: "var(--ink-4)" }} />
                </button>
            </div>
        </div>
    );
}

function VoiceQuotaBar({
    label,
    usedSeconds,
    limitSeconds,
    style,
}: {
    label: string;
    usedSeconds: number;
    limitSeconds: number;
    style?: React.CSSProperties;
}) {
    const usedMinutes = Math.round(usedSeconds / 60);
    const limitMinutes = Math.round(limitSeconds / 60);
    const percent = Math.min(100, Math.round((usedSeconds / limitSeconds) * 100));
    const isNearLimit = percent >= 80;
    const isExceeded = usedSeconds >= limitSeconds;
    const tone = isExceeded ? "rust" : isNearLimit ? "rust" : "olive";

    return (
        <div className="quota" style={style}>
            <div className="row between small" style={{ marginBottom: 8 }}>
                <span>{label}</span>
                <span className="num" style={isExceeded ? { color: "var(--heart)" } : undefined}>
                    {usedMinutes} / {limitMinutes} мин
                </span>
            </div>
            <Progress value={usedSeconds} max={Math.max(1, limitSeconds)} tone={tone} height={5} />
            {isExceeded && (
                <p className="small" style={{ color: "var(--heart)", marginTop: 6 }}>
                    Лимит исчерпан — звонки откроются{" "}
                    {label === "Сегодня" ? "завтра" : "в следующем месяце"}
                </p>
            )}
        </div>
    );
}

function SkillToggle({
    title,
    subtitle,
    isAlwaysOn,
    isEnrolled,
    disabled,
    onClick,
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
            className="row between gap-3"
            style={{
                padding: "12px 16px",
                borderRadius: "var(--r-md)",
                textAlign: "left",
                width: "100%",
                cursor: isAlwaysOn ? "default" : "pointer",
                transition: "all var(--transition)",
                border: isEnrolled ? "1.5px solid var(--primary)" : "1px solid var(--line)",
                background: isEnrolled
                    ? "var(--primary-soft)"
                    : isAlwaysOn
                    ? "var(--surface-2)"
                    : "var(--surface)",
                opacity: isAlwaysOn ? 0.7 : 1,
            }}
        >
            <div className="grow" style={{ minWidth: 0 }}>
                <p
                    className="body"
                    style={{
                        fontWeight: 700,
                        color: isEnrolled
                            ? "var(--primary)"
                            : isAlwaysOn
                            ? "var(--ink-3)"
                            : "var(--ink)",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                    }}
                >
                    {title}
                </p>
                <p className="small" style={{ marginTop: 2 }}>
                    {subtitle}
                </p>
            </div>
            <div
                style={{
                    width: 44,
                    height: 24,
                    borderRadius: 999,
                    flexShrink: 0,
                    display: "flex",
                    alignItems: "center",
                    padding: 2,
                    transition: "background var(--transition)",
                    background:
                        isAlwaysOn
                            ? "var(--line-2)"
                            : isEnrolled
                            ? "var(--primary)"
                            : "var(--line)",
                }}
            >
                <div
                    style={{
                        width: 20,
                        height: 20,
                        borderRadius: "50%",
                        background: "#fff",
                        boxShadow: "var(--sh-1)",
                        transition: "transform var(--transition)",
                        transform:
                            isAlwaysOn || isEnrolled ? "translateX(20px)" : "translateX(0)",
                    }}
                />
            </div>
        </button>
    );
}
