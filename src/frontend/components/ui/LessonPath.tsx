"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import type { LessonSummary } from "@/lib/hooks/useLesson";

// Zigzag horizontal offsets (px)
const OFFSETS = [0, 80, 80, 0, -80, -80];

interface LessonNodeProps {
    lesson: LessonSummary;
    index: number;
    total: number;
    isPopoverOpen: boolean;
    onTogglePopover: () => void;
    onClosePopover: () => void;
}

function LessonNode({
    lesson,
    index,
    total,
    isPopoverOpen,
    onTogglePopover,
    onClosePopover,
}: LessonNodeProps) {
    const isLocked = lesson.status === "locked";
    const isCompleted = lesson.status === "completed";
    const isActive = lesson.status === "available" || lesson.status === "in_progress";
    const offsetX = OFFSETS[index % OFFSETS.length];
    const nodeRef = useRef<HTMLDivElement>(null);

    // Close popover on outside click
    useEffect(() => {
        if (!isPopoverOpen) return;
        function handleOutsideClick(e: MouseEvent) {
            if (nodeRef.current && !nodeRef.current.contains(e.target as Node)) {
                onClosePopover();
            }
        }
        document.addEventListener("mousedown", handleOutsideClick);
        return () => document.removeEventListener("mousedown", handleOutsideClick);
    }, [isPopoverOpen, onClosePopover]);

    const nodeCircle = (
        <div className="relative flex items-center justify-center" ref={nodeRef}>
            {isActive && (
                <span className="absolute w-16 h-16 rounded-full bg-[#58CC02] opacity-20 animate-ping" />
            )}

            <div
                onClick={isLocked ? undefined : onTogglePopover}
                role={isLocked ? undefined : "button"}
                tabIndex={isLocked ? undefined : 0}
                onKeyDown={(e) => {
                    if (!isLocked && (e.key === "Enter" || e.key === " ")) onTogglePopover();
                }}
                className={`w-14 h-14 rounded-full flex items-center justify-center font-extrabold text-lg relative z-10 transition-transform active:translate-y-1 select-none ${
                    isLocked
                        ? "bg-[#F7F7F7] border-4 border-[#E5E5E5] text-[#AFAFAF] cursor-not-allowed"
                        : isCompleted
                          ? "bg-[#FFC800] text-white cursor-pointer"
                          : "bg-[#58CC02] text-white cursor-pointer"
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

            {/* Tap-to-open popover */}
            {isPopoverOpen && !isLocked && (
                <div className="absolute bottom-[calc(100%+14px)] left-1/2 -translate-x-1/2 w-56 bg-white rounded-2xl shadow-xl border border-[#E5E5E5] px-4 py-3 z-30">
                    <p className="font-bold text-sm text-gray-900 mb-0.5 truncate">
                        {lesson.title}
                    </p>
                    <p className="text-xs text-[#AFAFAF] mb-3">
                        Урок {index + 1} из {total}
                    </p>
                    <Link
                        href={`/session/${lesson.lessonId}`}
                        onClick={onClosePopover}
                        className="block w-full text-center py-2.5 rounded-xl bg-[#58CC02] text-white text-sm font-bold btn-3d"
                    >
                        Приступить к прохождению
                    </Link>
                    {/* Arrow pointing down */}
                    <div className="absolute -bottom-2 left-1/2 -translate-x-1/2 w-4 h-2 overflow-hidden">
                        <div className="w-3 h-3 bg-white border-r border-b border-[#E5E5E5] rotate-45 -translate-y-1.5 mx-auto" />
                    </div>
                </div>
            )}
        </div>
    );

    return (
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
}

interface LessonPathProps {
    lessons: LessonSummary[];
}

export function LessonPath({ lessons }: LessonPathProps) {
    const [openPopoverIndex, setOpenPopoverIndex] = useState<number | null>(null);

    function togglePopover(index: number) {
        setOpenPopoverIndex((prev) => (prev === index ? null : index));
    }

    return (
        <div className="relative flex flex-col items-center gap-0 pb-8">
            {/* Static background path line */}
            <div
                className="absolute left-1/2 -translate-x-1/2 top-7 bottom-7 w-1 rounded-full bg-[#E5E5E5]"
                aria-hidden
            />

            {lessons.map((lesson, lessonIndex) => {
                const isPassedOrActive =
                    lesson.status === "completed" ||
                    lesson.status === "in_progress" ||
                    lesson.status === "available";
                const isCurrentlyActive =
                    lesson.status === "available" || lesson.status === "in_progress";

                return (
                    <div
                        key={lesson.lessonId}
                        className="relative w-full flex flex-col items-center pb-12"
                    >
                        {/* Completed segment: solid green */}
                        {lessonIndex < lessons.length - 1 && isPassedOrActive && !isCurrentlyActive && (
                            <div
                                className="absolute left-1/2 -translate-x-1/2 top-7 w-1 rounded-full bg-[#58CC02]"
                                style={{ height: "calc(100% - 28px)" }}
                                aria-hidden
                            />
                        )}
                        {/* Active segment: animated dashed SVG line */}
                        {lessonIndex < lessons.length - 1 && isCurrentlyActive && (
                            <svg
                                className="absolute left-1/2 -translate-x-1/2 top-7 overflow-visible"
                                width="4"
                                style={{ height: "calc(100% - 28px)" }}
                                aria-hidden
                            >
                                <line
                                    x1="2" y1="0" x2="2" y2="100%"
                                    stroke="#58CC02"
                                    strokeWidth="4"
                                    strokeLinecap="round"
                                    strokeDasharray="10 10"
                                    className="path-dash-animated"
                                />
                            </svg>
                        )}
                        <LessonNode
                            lesson={lesson}
                            index={lessonIndex}
                            total={lessons.length}
                            isPopoverOpen={openPopoverIndex === lessonIndex}
                            onTogglePopover={() => togglePopover(lessonIndex)}
                            onClosePopover={() => setOpenPopoverIndex(null)}
                        />
                    </div>
                );
            })}
        </div>
    );
}
