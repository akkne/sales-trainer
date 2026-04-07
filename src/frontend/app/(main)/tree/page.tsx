"use client";

import Link from "next/link";
import { useEffect } from "react";
import { LessonPath } from "@/components/ui/LessonPath";
import { StatsWidget } from "@/components/layout/StatsWidget";
import { useSkillTree, useSkills } from "@/lib/hooks/useSkillTree";
import { useLessonsForSkill } from "@/lib/hooks/useLesson";
import { useSelectedSkillStore } from "@/lib/store/selectedSkillStore";
import { Icon } from "@/components/ui/Icon";

// ── Skill lesson view ─────────────────────────────────────────────────────────

function SkillLessonView({
    skillSlug,
    skillTitle,
}: {
    skillSlug: string;
    skillTitle: string;
}) {
    const { data: lessons, isLoading } = useLessonsForSkill(skillSlug);

    if (isLoading) {
        return (
            <div className="flex items-center justify-center py-20">
                <div className="w-10 h-10 rounded-full border-4 border-primary border-t-transparent animate-spin" />
            </div>
        );
    }

    const sorted = (lessons ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
    const completedCount = sorted.filter((l) => l.status === "completed").length;
    const totalCount = sorted.length;
    const progressPercent = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

    return (
        <>
            {/* Skill header banner - Hero Card */}
            <div className="rounded-2xl p-6 mb-8 bg-primary text-on-primary">
                <div className="flex items-start justify-between mb-4">
                    <div>
                        <p className="text-sm font-medium opacity-80 uppercase tracking-wider mb-1">
                            Текущий модуль
                        </p>
                        <h1 className="text-2xl font-headline font-bold leading-tight">
                            {skillTitle}
                        </h1>
                    </div>
                    <Icon name="psychology" size="2xl" className="opacity-50" />
                </div>

                {/* Progress stats */}
                <div className="mb-4">
                    <div className="flex justify-between text-sm mb-2">
                        <span className="font-medium">
                            Уровень мастерства {Math.min(5, Math.floor(progressPercent / 20) + 1)}/5
                        </span>
                        <span className="font-medium">{progressPercent}%</span>
                    </div>
                    <div className="h-2 bg-on-primary/30 rounded-full overflow-hidden">
                        <div
                            className="h-full bg-primary-container rounded-full transition-all duration-500"
                            style={{ width: `${progressPercent}%` }}
                        />
                    </div>
                </div>

                {/* Quick stats row */}
                <div className="flex items-center gap-4">
                    <div className="bg-on-primary/20 rounded-2xl px-3 py-2 text-center">
                        <span className="text-lg font-bold">
                            {completedCount}/{totalCount}
                        </span>
                        <p className="text-xs opacity-80">уроков</p>
                    </div>
                    <Link
                        href={`/skill/${skillSlug}/map`}
                        className="flex items-center gap-2 bg-primary-container text-on-primary-container font-semibold px-5 py-3 rounded-full hover:opacity-90 tonal-transition"
                    >
                        Продолжить урок
                        <Icon name="arrow_forward" size="sm" />
                    </Link>
                </div>
            </div>

            {sorted.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-16 gap-3 text-center">
                    <div className="w-16 h-16 rounded-full bg-surface-container flex items-center justify-center">
                        <Icon name="inbox" size="xl" className="text-on-surface-variant" />
                    </div>
                    <p className="text-lg font-bold text-on-surface">Уроки ещё не добавлены</p>
                    <p className="text-sm text-on-surface-variant max-w-xs">
                        Попроси администратора добавить уроки
                    </p>
                </div>
            ) : (
                <LessonPath lessons={sorted} />
            )}
        </>
    );
}

// ── Empty state when no skill selected ───────────────────────────────────────

function NoSkillSelected() {
    return (
        <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
            <div className="w-20 h-20 rounded-full flex items-center justify-center bg-surface-container">
                <Icon name="touch_app" size="xl" className="text-on-surface-variant" />
            </div>
            <p className="text-xl font-bold text-on-surface">Выбери навык</p>
            <p className="text-sm text-on-surface-variant max-w-xs">
                Нажми на навык слева, чтобы увидеть уроки
            </p>
            <Link
                href="/profile"
                className="mt-2 text-sm text-primary font-semibold hover:underline flex items-center gap-1"
            >
                Добавить навыки в профиле
                <Icon name="arrow_forward" size="sm" />
            </Link>
        </div>
    );
}

// ── Left sidebar ──────────────────────────────────────────────────────────────

function SkillSidebar() {
    const { data: allSkills, isLoading } = useSkills();
    const { selectedSkill, setSelectedSkill } = useSelectedSkillStore();

    const enrolledSkills = (allSkills ?? []).filter((s) => s.status !== "locked");

    // Auto-select first enrolled skill when none is selected
    useEffect(() => {
        if (!selectedSkill && enrolledSkills.length > 0) {
            const first = enrolledSkills[0];
            setSelectedSkill({
                slug: first.slug,
                title: first.title,
                iconName: first.iconName,
            });
        }
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [allSkills]);

    if (isLoading) {
        return (
            <div className="flex flex-col gap-2 pt-1">
                {[1, 2, 3].map((i) => (
                    <div key={i} className="h-12 rounded-2xl bg-surface-container animate-pulse" />
                ))}
            </div>
        );
    }

    if (enrolledSkills.length === 0) {
        return (
            <div className="text-center pt-4">
                <p className="text-xs text-on-surface-variant leading-relaxed">
                    Нет активных навыков.{" "}
                    <Link href="/profile" className="text-primary font-semibold">
                        Добавь в профиле
                    </Link>
                </p>
            </div>
        );
    }

    return (
        <nav className="flex flex-col gap-1">
            {enrolledSkills.map((skill) => {
                const isActive = selectedSkill?.slug === skill.slug;
                const progress =
                    skill.totalLessonCount > 0
                        ? Math.round((skill.completedLessonCount / skill.totalLessonCount) * 100)
                        : 0;

                return (
                    <button
                        key={skill.skillId}
                        onClick={() =>
                            setSelectedSkill({
                                slug: skill.slug,
                                title: skill.title,
                                iconName: skill.iconName,
                            })
                        }
                        className={`w-full flex flex-col gap-1 px-3 py-3 rounded-2xl text-left tonal-transition ${
                            isActive
                                ? "bg-primary-container"
                                : "hover:bg-surface-container"
                        }`}
                    >
                        <div className="flex items-center gap-2">
                            <span
                                className={`text-sm font-semibold truncate leading-tight flex-1 ${
                                    isActive ? "text-primary" : "text-on-surface"
                                }`}
                            >
                                {skill.title}
                            </span>
                            {isActive && (
                                <span className="shrink-0 w-2 h-2 rounded-full bg-primary" />
                            )}
                        </div>
                        {/* Mini progress bar */}
                        <div className="h-1 bg-surface-container-highest rounded-full overflow-hidden">
                            <div
                                className={`h-full rounded-full transition-all ${
                                    isActive ? "bg-primary" : "bg-outline-variant"
                                }`}
                                style={{ width: `${progress}%` }}
                            />
                        </div>
                    </button>
                );
            })}
        </nav>
    );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function SkillTreePage() {
    const { data: skillTreeData, isLoading, isError } = useSkillTree();
    const { selectedSkill } = useSelectedSkillStore();

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-primary border-t-transparent animate-spin" />
            </div>
        );
    }

    if (isError || !skillTreeData) {
        return (
            <div className="flex items-center justify-center min-h-screen text-on-surface-variant">
                Не удалось загрузить дерево навыков
            </div>
        );
    }

    return (
        <div className="max-w-5xl mx-auto px-4 py-8 flex gap-6">
            {/* ── Left: skill selector ───────────────────────────────────── */}
            <div className="w-44 hidden md:block shrink-0">
                <div className="sticky top-20">
                    <p className="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-3 px-1">
                        Навыки
                    </p>
                    <SkillSidebar />
                </div>
            </div>

            {/* ── Center: lesson path ────────────────────────────────────── */}
            <div className="flex-1 min-w-0">
                {selectedSkill ? (
                    <SkillLessonView
                        skillSlug={selectedSkill.slug}
                        skillTitle={selectedSkill.title}
                    />
                ) : (
                    <NoSkillSelected />
                )}
            </div>

            {/* ── Right: stats widget ────────────────────────────────────── */}
            <div className="w-52 hidden md:block pt-4 shrink-0">
                <StatsWidget
                    currentStreakDayCount={skillTreeData.currentStreakDayCount}
                    totalXpAmount={skillTreeData.totalXpAmount}
                    weeklyXpAmount={skillTreeData.weeklyXpAmount}
                />
            </div>
        </div>
    );
}
