"use client";

import Link from "next/link";
import { use } from "react";
import { useLessonsForSkill } from "@/lib/hooks/useLesson";
import { LessonPath } from "@/components/ui/LessonPath";

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

    const lessons = (lessonSummaries ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
    const completedCount = lessons.filter((l) => l.status === "completed").length;

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            <Link
                href="/tree"
                className="text-[#AFAFAF] hover:text-gray-600 text-sm mb-6 inline-flex items-center gap-1"
            >
                ← Назад
            </Link>

            <div className="bg-[#F7F7F7] rounded-2xl px-5 py-4 mb-8">
                <div className="flex items-center justify-between mb-2">
                    <span className="font-semibold text-gray-700 text-sm">Уроки</span>
                    <span className="text-sm font-bold text-[#58CC02]">
                        {completedCount}/{lessons.length}
                    </span>
                </div>
                <div className="h-2 bg-[#E5E5E5] rounded-full overflow-hidden">
                    <div
                        className="h-full bg-[#58CC02] rounded-full transition-all duration-500"
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
