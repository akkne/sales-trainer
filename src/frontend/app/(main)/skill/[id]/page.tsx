"use client";

import Link from "next/link";
import { use } from "react";
import { useLessonsForSkill } from "@/lib/hooks/useLesson";

const DIFFICULTY_LABELS = ["", "Базовый", "Средний", "Сложный"];

interface SkillPageProps {
    params: Promise<{ id: string }>;
}

export default function SkillPage({ params }: SkillPageProps) {
    const { id: skillSlug } = use(params);
    const { data: lessonSummaries, isLoading } = useLessonsForSkill(skillSlug);

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            <Link
                href="/tree"
                className="text-gray-400 hover:text-gray-600 text-sm mb-6 inline-block"
            >
                ← Назад
            </Link>

            <h1 className="font-[var(--font-space-grotesk)] text-2xl font-bold text-gray-900 mb-6">
                Уроки
            </h1>

            <div className="flex flex-col gap-3">
                {lessonSummaries?.map((lessonSummary, lessonIndex) => {
                    const isLocked = lessonSummary.status === "locked";
                    const isCompleted = lessonSummary.status === "completed";

                    return (
                        <div key={lessonSummary.lessonId}>
                            {isLocked ? (
                                <div className="flex items-center gap-4 px-5 py-4 rounded-2xl bg-gray-100 opacity-60">
                                    <div className="w-10 h-10 rounded-full bg-gray-300 flex items-center justify-center text-gray-500 font-bold shrink-0">
                                        🔒
                                    </div>
                                    <div className="flex-1">
                                        <div className="font-semibold text-gray-500">
                                            {lessonSummary.title}
                                        </div>
                                        <div className="text-xs text-gray-400">
                                            {DIFFICULTY_LABELS[lessonSummary.difficultyLevel]} ·{" "}
                                            {lessonSummary.xpReward} XP
                                        </div>
                                    </div>
                                </div>
                            ) : (
                                <Link
                                    href={`/exercise/${lessonSummary.lessonId}`}
                                    className="flex items-center gap-4 px-5 py-4 rounded-2xl bg-white hover:bg-[#E8F9D6] transition-colors shadow-sm border border-gray-100"
                                >
                                    <div
                                        className={`w-10 h-10 rounded-full flex items-center justify-center font-bold text-white shrink-0 ${
                                            isCompleted ? "bg-[#58CC02]" : "bg-[#1CB0F6]"
                                        }`}
                                    >
                                        {isCompleted ? "✓" : lessonIndex + 1}
                                    </div>
                                    <div className="flex-1">
                                        <div className="font-semibold text-gray-900">
                                            {lessonSummary.title}
                                        </div>
                                        <div className="text-xs text-gray-400">
                                            {DIFFICULTY_LABELS[lessonSummary.difficultyLevel]} ·{" "}
                                            {lessonSummary.xpReward} XP
                                            {isCompleted &&
                                                ` · Лучший: ${lessonSummary.bestScore}%`}
                                        </div>
                                    </div>
                                    {isCompleted && (
                                        <span className="text-[#FFC800] text-lg">⭐</span>
                                    )}
                                </Link>
                            )}
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
