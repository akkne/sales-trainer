"use client";

import Link from "next/link";
import { useEffect } from "react";
import { LessonPath } from "@/components/ui/LessonPath";
import { StatsWidget } from "@/components/layout/StatsWidget";
import { useSkillTree, useSkills } from "@/lib/hooks/useSkillTree";
import { useLessonsForSkill } from "@/lib/hooks/useLesson";
import { useSelectedSkillStore } from "@/lib/store/selectedSkillStore";

// ── Skill lesson view ─────────────────────────────────────────────────────────

function SkillLessonView({
    skillSlug,
    skillTitle,
    skillIcon,
}: {
    skillSlug: string;
    skillTitle: string;
    skillIcon: string;
}) {
    const { data: lessons, isLoading } = useLessonsForSkill(skillSlug);

    if (isLoading) {
        return (
            <div className="flex items-center justify-center py-20">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    const sorted = (lessons ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
    const completedCount = sorted.filter((l) => l.status === "completed").length;

    return (
        <>
            {/* Skill header banner */}
            <div
                className="rounded-3xl px-6 py-5 mb-8 text-white"
                style={{ background: "#58CC02", boxShadow: "0 4px 0 0 #58A700" }}
            >
                <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                        <span className="text-3xl">{skillIcon || "📚"}</span>
                        <div>
                            <p className="text-sm font-semibold opacity-80 uppercase tracking-wider mb-0.5">
                                Текущий навык
                            </p>
                            <h1 className="text-xl font-extrabold">{skillTitle}</h1>
                        </div>
                    </div>
                    <div className="bg-white/20 rounded-2xl px-3 py-2 text-center shrink-0">
                        <span className="text-xl font-extrabold">
                            {completedCount}/{sorted.length}
                        </span>
                    </div>
                </div>
                <div className="mt-4 h-2 bg-white/30 rounded-full overflow-hidden">
                    <div
                        className="h-full bg-white rounded-full transition-all duration-500"
                        style={{
                            width: `${sorted.length > 0 ? (completedCount / sorted.length) * 100 : 0}%`,
                        }}
                    />
                </div>
            </div>

            {sorted.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-16 gap-3 text-center">
                    <span className="text-5xl">📭</span>
                    <p className="text-lg font-extrabold text-gray-800">Уроки ещё не добавлены</p>
                    <p className="text-sm text-[#AFAFAF] max-w-xs">
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
            <div className="w-20 h-20 rounded-full flex items-center justify-center text-4xl bg-[#F7F7F7]">
                👈
            </div>
            <p className="text-xl font-extrabold text-gray-800">Выбери навык</p>
            <p className="text-sm text-[#AFAFAF] max-w-xs">
                Нажми на навык слева, чтобы увидеть уроки
            </p>
            <Link
                href="/profile"
                className="mt-2 text-sm text-[#58CC02] font-semibold hover:underline"
            >
                Добавить навыки в профиле →
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
                    <div key={i} className="h-12 rounded-xl bg-[#F7F7F7] animate-pulse" />
                ))}
            </div>
        );
    }

    if (enrolledSkills.length === 0) {
        return (
            <div className="text-center pt-4">
                <p className="text-xs text-[#AFAFAF] leading-relaxed">
                    Нет активных навыков.{" "}
                    <Link href="/profile" className="text-[#58CC02] font-semibold">
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
                        className={`w-full flex flex-col gap-0.5 px-3 py-2.5 rounded-xl text-left transition-all ${
                            isActive
                                ? "bg-[#E8F9D6] text-[#3C8400]"
                                : "text-gray-600 hover:bg-[#F7F7F7]"
                        }`}
                    >
                        <div className="flex items-center gap-2">
                            <span className="text-lg leading-none shrink-0">
                                {skill.iconName || "📚"}
                            </span>
                            <span
                                className={`text-sm font-semibold truncate leading-tight ${
                                    isActive ? "text-[#3C8400]" : "text-gray-700"
                                }`}
                            >
                                {skill.title}
                            </span>
                            {isActive && (
                                <span className="ml-auto shrink-0 w-2 h-2 rounded-full bg-[#58CC02]" />
                            )}
                        </div>
                        {/* Mini progress bar */}
                        <div className="ml-7 h-1 bg-gray-200 rounded-full overflow-hidden">
                            <div
                                className="h-full bg-[#58CC02] rounded-full transition-all"
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
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    if (isError || !skillTreeData) {
        return (
            <div className="flex items-center justify-center min-h-screen text-gray-500">
                Не удалось загрузить дерево навыков
            </div>
        );
    }

    return (
        <div className="max-w-5xl mx-auto px-4 py-8 flex gap-6">
            {/* ── Left: skill selector ───────────────────────────────────── */}
            <div className="w-44 hidden md:block shrink-0">
                <div className="sticky top-8">
                    <p className="text-xs font-bold uppercase tracking-wider text-[#AFAFAF] mb-3 px-1">
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
                        skillIcon={selectedSkill.iconName}
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
