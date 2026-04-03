"use client";

import Link from "next/link";
import { use } from "react";
import { useLessonsForSkill, type LessonSummary } from "@/lib/hooks/useLesson";

interface SkillPageProps {
    params: Promise<{ id: string }>;
}

// Alternating offsets for the zigzag lesson path
const OFFSETS = [0, 80, 80, 0, -80, -80];

function LessonNode({
    lesson,
    index,
    total,
}: {
    lesson: LessonSummary;
    index: number;
    total: number;
}) {
    const isLocked = lesson.status === "locked";
    const isCompleted = lesson.status === "completed";
    const isActive = lesson.status === "available" || lesson.status === "in_progress";
    const offsetX = OFFSETS[index % OFFSETS.length];

    const nodeCircle = (
        <div className="relative flex items-center justify-center">
            {/* Ping ring for active */}
            {isActive && (
                <span className="absolute w-16 h-16 rounded-full bg-[#58CC02] opacity-20 animate-ping" />
            )}

            <div
                className={`w-14 h-14 rounded-full flex items-center justify-center font-extrabold text-lg relative z-10 transition-transform active:translate-y-1 ${
                    isLocked
                        ? "bg-[#F7F7F7] border-4 border-[#E5E5E5] text-[#AFAFAF]"
                        : isCompleted
                          ? "bg-[#FFC800] text-white"
                          : "bg-[#58CC02] text-white"
                }`}
                style={{
                    boxShadow: isLocked
                        ? "0 4px 0 #D1D5DB"
                        : isCompleted
                          ? "0 4px 0 #E0A800"
                          : "0 4px 0 #58A700",
                }}
            >
                {isLocked ? (
                    <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
                        <path d="M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z" />
                    </svg>
                ) : isCompleted ? (
                    "✓"
                ) : (
                    index + 1
                )}
            </div>

            {/* Popover above active node */}
            {isActive && (
                <div className="absolute bottom-[calc(100%+14px)] left-1/2 -translate-x-1/2 w-52 bg-white rounded-2xl shadow-lg border border-[#E5E5E5] px-4 py-3 z-20 pointer-events-none">
                    <p className="font-bold text-sm text-gray-900 mb-0.5 truncate">{lesson.title}</p>
                    <p className="text-xs text-[#AFAFAF] mb-2">Урок {index + 1} из {total}</p>
                    <Link
                        href={`/exercise/${lesson.lessonId}`}
                        className="pointer-events-auto block w-full text-center py-2 rounded-xl bg-[#58CC02] text-white text-sm font-bold btn-3d"
                    >
                        Старт
                    </Link>
                    {/* Arrow */}
                    <div className="absolute -bottom-2 left-1/2 -translate-x-1/2 w-4 h-2 overflow-hidden">
                        <div className="w-3 h-3 bg-white border border-[#E5E5E5] rotate-45 -translate-y-1.5 mx-auto" />
                    </div>
                </div>
            )}
        </div>
    );

    const content = (
        <div
            className="flex flex-col items-center gap-2"
            style={{ marginLeft: `${offsetX}px` }}
        >
            {nodeCircle}
            <span
                className={`text-xs font-semibold text-center max-w-[100px] leading-tight ${
                    isLocked ? "text-[#AFAFAF]" : "text-gray-700"
                }`}
            >
                {lesson.title}
            </span>
        </div>
    );

    if (isLocked) return <div>{content}</div>;

    return (
        <Link href={`/exercise/${lesson.lessonId}`} className="block">
            {content}
        </Link>
    );
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

    const lessons = lessonSummaries ?? [];
    const completedCount = lessons.filter((l) => l.status === "completed").length;

    return (
        <div className="max-w-2xl mx-auto px-4 py-8">
            <Link
                href="/tree"
                className="text-[#AFAFAF] hover:text-gray-600 text-sm mb-6 inline-flex items-center gap-1"
            >
                ← Назад
            </Link>

            {/* Progress bar header */}
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

            {/* Vertical node path */}
            <div className="relative flex flex-col items-center gap-0">
                {/* Background path line */}
                <div
                    className="absolute left-1/2 -translate-x-1/2 top-7 bottom-7 w-1 rounded-full bg-[#E5E5E5]"
                    aria-hidden
                />

                {lessons.map((lesson, lessonIndex) => {
                    const isPassedOrActive =
                        lesson.status === "completed" ||
                        lesson.status === "in_progress" ||
                        lesson.status === "available";

                    return (
                        <div
                            key={lesson.lessonId}
                            className="relative w-full flex flex-col items-center pb-12"
                        >
                            {/* Active path segment overlay */}
                            {lessonIndex < lessons.length - 1 && isPassedOrActive && (
                                <div
                                    className="absolute left-1/2 -translate-x-1/2 top-7 w-1 rounded-full bg-[#58CC02]"
                                    style={{ height: "calc(100% - 28px)" }}
                                    aria-hidden
                                />
                            )}
                            <LessonNode
                                lesson={lesson}
                                index={lessonIndex}
                                total={lessons.length}
                            />
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
