"use client";

import Link from "next/link";
import { use } from "react";
import { useLessonsForSkill } from "@/features/exercise/hooks/use-lesson";
import { useSkills } from "@/features/skills/hooks/use-skill-tree";
import { Icon } from "@/shared/components/icon";

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
                <div className="w-10 h-10 rounded-full border-4 border-ink border-t-transparent animate-spin" />
            </div>
        );
    }

    const skill = skills?.find((s) => s.slug === skillSlug);
    const lessons = (lessonSummaries ?? []).slice().sort((a, b) => a.orderInTopic - b.orderInTopic);
    const completedCount = lessons.filter((l) => l.status === "completed").length;
    const totalCount = lessons.length;
    const completionPercent = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            {/* Back link */}
            <Link
                href="/tree"
                className="text-ink-3 hover:text-indigo-ink text-sm mb-6 inline-flex items-center gap-1 transition-colors"
            >
                <Icon name="arrow-left" size="sm" />
                Назад к навыкам
            </Link>

            {/* Header card with circular progress */}
            <div className="bg-indigo-soft rounded-2xl p-6 mb-8">
                <div className="flex items-center gap-5">
                    {/* Circular progress indicator */}
                    <div className="relative w-20 h-20 shrink-0">
                        <svg className="w-20 h-20 -rotate-90" viewBox="0 0 80 80">
                            <circle
                                cx="40"
                                cy="40"
                                r="32"
                                fill="none"
                                stroke="var(--line)"
                                strokeOpacity="0.3"
                                strokeWidth="8"
                            />
                            <circle
                                cx="40"
                                cy="40"
                                r="32"
                                fill="none"
                                stroke="var(--indigo)"
                                strokeWidth="8"
                                strokeLinecap="round"
                                strokeDasharray={`${(completionPercent / 100) * 201} 201`}
                            />
                        </svg>
                        <span className="absolute inset-0 flex items-center justify-center text-indigo-ink font-bold text-lg">
                            {completionPercent}%
                        </span>
                    </div>

                    {/* Stats text */}
                    <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1">
                            <Icon name="phone" size="md" className="text-indigo-ink" />
                            <h1 className="text-xl font-bold text-ink">
                                {skill?.title ?? skillSlug}
                            </h1>
                        </div>
                        <p className="text-sm text-ink-3 mb-2">
                            {completedCount} из {totalCount} уроков пройдено
                        </p>
                    </div>
                </div>
            </div>

            {/* Learning Journey section */}
            <h2 className="text-lg font-semibold text-ink mb-4">Путь обучения</h2>

            {/* Lesson list */}
            <div className="flex flex-col gap-4">
                {lessons.map((lesson, index) => {
                    const isCompleted = lesson.status === "completed";
                    const isActive = lesson.status === "available" || lesson.status === "in_progress";
                    const isLocked = lesson.status === "locked";
                    const isUpNext = isActive && index === lessons.findIndex((l) => l.status === "available" || l.status === "in_progress");

                    return (
                        <div
                            key={lesson.lessonId}
                            className={`flex items-start gap-4 rounded-2xl p-4 transition-colors ${
                                isUpNext
                                    ? "bg-olive-soft ring-2 ring-olive"
                                    : isCompleted
                                    ? "bg-surface"
                                    : isLocked
                                    ? "bg-surface opacity-60"
                                    : "bg-surface"
                            }`}
                        >
                            {/* Step badge */}
                            <div
                                className={`w-10 h-10 rounded-full shrink-0 flex items-center justify-center font-bold text-sm ${
                                    isCompleted
                                        ? "bg-ink text-bg"
                                        : isUpNext
                                        ? "bg-olive text-white"
                                        : isLocked
                                        ? "bg-bg-2 text-ink-4"
                                        : "bg-ink text-bg"
                                }`}
                            >
                                {isCompleted ? (
                                    <Icon name="check" size="sm" />
                                ) : isLocked ? (
                                    <Icon name="lock" size="sm" />
                                ) : (
                                    index + 1
                                )}
                            </div>

                            {/* Content */}
                            <div className="flex-1 min-w-0">
                                {/* Meta row */}
                                <div className="flex flex-wrap items-center gap-2 mb-1">
                                    <span className="text-xs font-medium text-ink-3">
                                        Урок {index + 1}
                                    </span>
                                    {isUpNext && (
                                        <span className="text-xs font-bold text-olive bg-olive-soft px-2 py-0.5 rounded-full">
                                            Далее
                                        </span>
                                    )}
                                </div>

                                <h3
                                    className={`font-semibold text-base leading-snug ${
                                        isLocked ? "text-ink-3" : "text-ink"
                                    }`}
                                >
                                    {lesson.title}
                                </h3>

                                {isLocked && (
                                    <p className="flex items-center gap-1 text-xs text-ink-4 mt-2">
                                        <Icon name="info" size="sm" />
                                        Пройди предыдущий урок
                                    </p>
                                )}
                            </div>

                            {/* CTA button */}
                            {isCompleted && (
                                <Link
                                    href={`/session/${lesson.lessonId}`}
                                    className="shrink-0 self-center px-4 py-2 rounded-full border border-line-2 text-sm font-medium text-ink hover:bg-surface-2 transition-colors"
                                >
                                    Повторить
                                </Link>
                            )}
                            {isUpNext && (
                                <Link
                                    href={`/session/${lesson.lessonId}`}
                                    className="shrink-0 self-center flex items-center gap-1 px-4 py-2 rounded-full bg-olive text-white text-sm font-semibold hover:opacity-90 transition-colors"
                                >
                                    Начать
                                    <Icon name="bolt" size="sm" />
                                </Link>
                            )}
                            {isActive && !isUpNext && (
                                <Link
                                    href={`/session/${lesson.lessonId}`}
                                    className="shrink-0 self-center px-4 py-2 rounded-full bg-ink text-bg text-sm font-semibold hover:opacity-90 transition-colors"
                                >
                                    Продолжить
                                </Link>
                            )}
                        </div>
                    );
                })}

                {lessons.length === 0 && (
                    <div className="text-center text-ink-3 py-12">
                        <div className="w-16 h-16 rounded-full bg-surface flex items-center justify-center mx-auto mb-4">
                            <Icon name="folder" size="xl" className="text-ink-3" />
                        </div>
                        <p className="font-semibold">Уроки ещё не добавлены</p>
                    </div>
                )}
            </div>
        </div>
    );
}
