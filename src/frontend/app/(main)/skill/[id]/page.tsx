"use client";

import Link from "next/link";
import { use } from "react";
import { useLessonsForSkill } from "@/features/exercise/hooks/use-lesson";
import { LessonPath } from "@/shared/components/lesson-path";

interface SkillPageProps {
    params: Promise<{ id: string }>;
}

export default function SkillPage({ params }: SkillPageProps) {
    const { id: skillSlug } = use(params);
    const { data: lessonSummaries, isLoading } = useLessonsForSkill(skillSlug);

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-ink border-t-transparent animate-spin" />
            </div>
        );
    }

    const lessons = (lessonSummaries ?? []).slice().sort((a, b) => a.orderInTopic - b.orderInTopic);
    const completedCount = lessons.filter((l) => l.status === "completed").length;

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            <Link
                href="/tree"
                className="text-ink-4 hover:text-ink text-sm mb-6 inline-flex items-center gap-1"
            >
                ← Назад
            </Link>

            <div className="bg-surface border border-line rounded-2xl px-5 py-4 mb-8">
                <div className="flex items-center justify-between mb-2">
                    <span className="font-semibold text-ink-2 text-sm">Уроки</span>
                    <div className="flex items-center gap-3">
                        <Link
                            href={`/skill/${skillSlug}/map`}
                            className="text-xs text-olive font-semibold hover:underline"
                        >
                            Карта курса →
                        </Link>
                        <span className="text-sm font-bold text-olive">
                            {completedCount}/{lessons.length}
                        </span>
                    </div>
                </div>
                <div className="h-2 bg-line rounded-full overflow-hidden">
                    <div
                        className="h-full bg-olive rounded-full transition-all duration-500"
                        style={{
                            width: `${lessons.length > 0 ? (completedCount / lessons.length) * 100 : 0}%`,
                        }}
                    />
                </div>
            </div>

            <LessonPath lessons={lessons} />
        </div>
    );
}
