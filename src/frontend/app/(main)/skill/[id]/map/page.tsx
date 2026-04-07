"use client";

import Link from "next/link";
import { use } from "react";
import { useLessonsForSkill } from "@/lib/hooks/useLesson";
import { useSkills } from "@/lib/hooks/useSkillTree";
import { Icon } from "@/components/ui/Icon";

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
                <div className="w-10 h-10 rounded-full border-4 border-primary border-t-transparent animate-spin" />
            </div>
        );
    }

    const skill = skills?.find((s) => s.slug === skillSlug);
    const lessons = (lessonSummaries ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
    const completedCount = lessons.filter((l) => l.status === "completed").length;
    const totalCount = lessons.length;
    const completionPercent = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

    // Calculate total XP earned
    const totalXpEarned = lessons
        .filter((l) => l.status === "completed")
        .reduce((sum, l) => sum + (l.xpReward || 0), 0);

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            {/* Back link */}
            <Link
                href="/tree"
                className="text-on-surface-variant hover:text-primary text-sm mb-6 inline-flex items-center gap-1 tonal-transition"
            >
                <Icon name="arrow_back" size="sm" />
                Назад к навыкам
            </Link>

            {/* Header card with circular progress */}
            <div className="bg-primary-container rounded-2xl p-6 mb-8">
                <div className="flex items-center gap-5">
                    {/* Circular progress indicator */}
                    <div className="relative w-20 h-20 shrink-0">
                        <svg className="w-20 h-20 -rotate-90" viewBox="0 0 80 80">
                            <circle
                                cx="40"
                                cy="40"
                                r="32"
                                fill="none"
                                stroke="var(--color-on-primary)"
                                strokeOpacity="0.3"
                                strokeWidth="8"
                            />
                            <circle
                                cx="40"
                                cy="40"
                                r="32"
                                fill="none"
                                stroke="var(--color-primary)"
                                strokeWidth="8"
                                strokeLinecap="round"
                                strokeDasharray={`${(completionPercent / 100) * 201} 201`}
                            />
                        </svg>
                        <span className="absolute inset-0 flex items-center justify-center text-primary font-headline font-bold text-lg">
                            {completionPercent}%
                        </span>
                    </div>

                    {/* Stats text */}
                    <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1">
                            <Icon name="call" size="md" className="text-primary" />
                            <h1 className="text-xl font-headline font-bold text-on-surface">
                                {skill?.title ?? skillSlug}
                            </h1>
                        </div>
                        <p className="text-sm text-on-surface-variant mb-2">
                            {completedCount} из {totalCount} уроков пройдено
                        </p>
                        <p className="text-sm font-semibold text-primary">
                            +{totalXpEarned} XP заработано
                        </p>
                    </div>
                </div>
            </div>

            {/* Learning Journey section */}
            <h2 className="text-lg font-semibold text-on-surface mb-4">Путь обучения</h2>

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
                            className={`flex items-start gap-4 rounded-2xl p-4 tonal-transition ${
                                isUpNext
                                    ? "bg-secondary-container ring-2 ring-secondary"
                                    : isCompleted
                                    ? "bg-surface-container"
                                    : isLocked
                                    ? "bg-surface-container-low opacity-60"
                                    : "bg-surface-container"
                            }`}
                        >
                            {/* Step badge */}
                            <div
                                className={`w-10 h-10 rounded-full shrink-0 flex items-center justify-center font-bold text-sm ${
                                    isCompleted
                                        ? "bg-primary text-on-primary"
                                        : isUpNext
                                        ? "bg-secondary text-on-secondary"
                                        : isLocked
                                        ? "bg-surface-variant text-outline"
                                        : "bg-primary text-on-primary"
                                }`}
                            >
                                {isCompleted ? (
                                    <Icon name="check" size="sm" variant="filled" />
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
                                    <span className="text-xs font-medium text-on-surface-variant">
                                        Урок {index + 1}
                                    </span>
                                    {isUpNext && (
                                        <span className="text-xs font-bold text-secondary bg-on-secondary px-2 py-0.5 rounded-full">
                                            Далее
                                        </span>
                                    )}
                                    <span className="text-xs font-semibold text-primary bg-primary-container px-2 py-0.5 rounded-full">
                                        +{lesson.xpReward} XP
                                    </span>
                                    {lesson.estimatedMinutes > 0 && (
                                        <span className="flex items-center gap-0.5 text-xs text-on-surface-variant">
                                            <Icon name="schedule" size="sm" />
                                            {lesson.estimatedMinutes} мин
                                        </span>
                                    )}
                                </div>

                                <h3
                                    className={`font-semibold text-base leading-snug ${
                                        isLocked ? "text-on-surface-variant" : "text-on-surface"
                                    }`}
                                >
                                    {lesson.title}
                                </h3>

                                {lesson.description && (
                                    <p className="text-xs text-on-surface-variant mt-0.5 line-clamp-2">
                                        {lesson.description}
                                    </p>
                                )}

                                {isLocked && (
                                    <p className="flex items-center gap-1 text-xs text-outline mt-2">
                                        <Icon name="info" size="sm" />
                                        Пройди предыдущий урок
                                    </p>
                                )}
                            </div>

                            {/* CTA button */}
                            {isCompleted && (
                                <Link
                                    href={`/session/${lesson.lessonId}`}
                                    className="shrink-0 self-center px-4 py-2 rounded-full border border-outline text-sm font-medium text-on-surface hover:bg-surface-container-high tonal-transition"
                                >
                                    Повторить
                                </Link>
                            )}
                            {isUpNext && (
                                <Link
                                    href={`/session/${lesson.lessonId}`}
                                    className="shrink-0 self-center flex items-center gap-1 px-4 py-2 rounded-full bg-secondary text-on-secondary text-sm font-semibold hover:opacity-90 tonal-transition"
                                >
                                    Начать
                                    <Icon name="bolt" size="sm" />
                                </Link>
                            )}
                            {isActive && !isUpNext && (
                                <Link
                                    href={`/session/${lesson.lessonId}`}
                                    className="shrink-0 self-center px-4 py-2 rounded-full bg-primary text-on-primary text-sm font-semibold hover:opacity-90 tonal-transition"
                                >
                                    Продолжить
                                </Link>
                            )}
                        </div>
                    );
                })}

                {lessons.length === 0 && (
                    <div className="text-center text-on-surface-variant py-12">
                        <div className="w-16 h-16 rounded-full bg-surface-container flex items-center justify-center mx-auto mb-4">
                            <Icon name="inbox" size="xl" className="text-on-surface-variant" />
                        </div>
                        <p className="font-semibold">Уроки ещё не добавлены</p>
                    </div>
                )}
            </div>
        </div>
    );
}
