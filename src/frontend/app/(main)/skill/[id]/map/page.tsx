"use client";

import Link from "next/link";
import { use } from "react";
import { useLessonsForSkill } from "@/lib/hooks/useLesson";
import { useSkills } from "@/lib/hooks/useSkillTree";

interface SkillMapPageProps {
    params: Promise<{ id: string }>;
}

export default function SkillMapPage({ params }: SkillMapPageProps) {
    const { id: skillSlug } = use(params);
    const { data: lessonSummaries, isLoading: lessonsLoading } = useLessonsForSkill(skillSlug);
    const { data: skills, isLoading: skillsLoading } = useSkills();

    const isLoading = lessonsLoading || skillsLoading;

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    const skill = skills?.find((s) => s.slug === skillSlug);
    const lessons = (lessonSummaries ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
    const completedCount = lessons.filter((l) => l.status === "completed").length;
    const totalCount = lessons.length;
    const completionPercent = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            {/* Back link */}
            <Link
                href={`/skill/${skillSlug}`}
                className="text-[#AFAFAF] hover:text-gray-600 text-sm mb-6 inline-flex items-center gap-1"
            >
                ← Назад
            </Link>

            {/* Header */}
            <div className="bg-[#58CC02] rounded-2xl px-5 py-5 mb-8 text-white"
                style={{ boxShadow: "0 4px 0 #58A700" }}>
                <div className="flex items-center gap-3 mb-3">
                    {skill && (
                        <span className="text-3xl">{skill.iconName || "📚"}</span>
                    )}
                    <div>
                        <h1 className="font-extrabold text-xl leading-tight">
                            {skill?.title ?? skillSlug}
                        </h1>
                        <p className="text-sm opacity-80">
                            {completedCount} из {totalCount} уроков пройдено
                        </p>
                    </div>
                    <div className="ml-auto text-right">
                        <span className="font-extrabold text-2xl">{completionPercent}%</span>
                    </div>
                </div>
                <div className="h-2.5 bg-white/30 rounded-full overflow-hidden">
                    <div
                        className="h-full bg-white rounded-full transition-all duration-500"
                        style={{ width: `${completionPercent}%` }}
                    />
                </div>
            </div>

            {/* Lesson list */}
            <div className="flex flex-col gap-3">
                {lessons.map((lesson, index) => {
                    const isCompleted = lesson.status === "completed";
                    const isActive = lesson.status === "available" || lesson.status === "in_progress";
                    const isLocked = lesson.status === "locked";

                    return (
                        <div
                            key={lesson.lessonId}
                            className={`rounded-2xl border-2 px-4 py-4 transition-all ${
                                isActive
                                    ? "border-[#58CC02] bg-white shadow-sm"
                                    : isCompleted
                                      ? "border-[#E5E5E5] bg-[#F9FFF4]"
                                      : "border-[#E5E5E5] bg-[#F7F7F7] opacity-60"
                            }`}
                        >
                            <div className="flex items-start gap-3">
                                {/* Status icon */}
                                <div
                                    className={`w-10 h-10 rounded-full flex-shrink-0 flex items-center justify-center font-extrabold text-sm ${
                                        isCompleted
                                            ? "bg-[#58CC02] text-white"
                                            : isActive
                                              ? "bg-[#58CC02] text-white"
                                              : "bg-[#E5E5E5] text-[#AFAFAF]"
                                    }`}
                                    style={{
                                        boxShadow: isCompleted || isActive
                                            ? "0 3px 0 #58A700"
                                            : "0 3px 0 #D1D5DB",
                                    }}
                                >
                                    {isCompleted ? (
                                        <span className="text-base">✓</span>
                                    ) : isLocked ? (
                                        <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                                            <path d="M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z" />
                                        </svg>
                                    ) : (
                                        index + 1
                                    )}
                                </div>

                                {/* Content */}
                                <div className="flex-1 min-w-0">
                                    <div className="flex items-center gap-2 mb-0.5 flex-wrap">
                                        <span
                                            className={`font-bold text-sm ${
                                                isLocked ? "text-[#AFAFAF]" : "text-gray-900"
                                            }`}
                                        >
                                            Урок {index + 1}
                                        </span>
                                        {/* XP badge */}
                                        <span className="text-xs font-bold text-[#58CC02] bg-[#E6F9D5] px-2 py-0.5 rounded-full">
                                            +{lesson.xpReward} XP
                                        </span>
                                        {/* Duration badge */}
                                        {lesson.estimatedMinutes > 0 && (
                                            <span className="text-xs text-[#AFAFAF]">
                                                ~{lesson.estimatedMinutes} мин
                                            </span>
                                        )}
                                    </div>
                                    <p
                                        className={`font-semibold text-base leading-tight mb-1 ${
                                            isLocked ? "text-[#AFAFAF]" : "text-gray-800"
                                        }`}
                                    >
                                        {lesson.title}
                                    </p>
                                    {lesson.description && (
                                        <p className="text-xs text-[#7A7A7A] line-clamp-2">
                                            {lesson.description}
                                        </p>
                                    )}
                                    {isLocked && (
                                        <p className="text-xs text-[#AFAFAF] mt-1">
                                            Пройди предыдущий урок, чтобы открыть
                                        </p>
                                    )}
                                </div>
                            </div>

                            {/* CTA for active lesson */}
                            {isActive && (
                                <div className="mt-3 pl-13">
                                    <Link
                                        href={`/session/${lesson.lessonId}`}
                                        className="inline-block w-full text-center py-2.5 rounded-xl bg-[#58CC02] text-white text-sm font-bold btn-3d"
                                        style={{ boxShadow: "0 4px 0 #58A700" }}
                                    >
                                        Начать урок
                                    </Link>
                                </div>
                            )}
                        </div>
                    );
                })}

                {lessons.length === 0 && (
                    <div className="text-center text-[#AFAFAF] py-12">
                        <p className="text-4xl mb-3">📭</p>
                        <p className="font-semibold">Уроки ещё не добавлены</p>
                    </div>
                )}
            </div>
        </div>
    );
}
