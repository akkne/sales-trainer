"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import type { LessonSummary } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

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
                <span className="absolute w-16 h-16 rounded-full bg-primary opacity-20 animate-ping" />
            )}

            <div
                onClick={isLocked ? undefined : onTogglePopover}
                role={isLocked ? undefined : "button"}
                tabIndex={isLocked ? undefined : 0}
                onKeyDown={(e) => {
                    if (!isLocked && (e.key === "Enter" || e.key === " ")) onTogglePopover();
                }}
                className={`w-14 h-14 rounded-full flex items-center justify-center font-bold text-lg relative z-10 transition-transform active:translate-y-1 select-none ${
                    isLocked
                        ? "bg-surface-container-high border-4 border-outline-variant text-on-surface-variant cursor-not-allowed"
                        : isCompleted
                          ? "bg-secondary text-on-secondary cursor-pointer"
                          : "bg-primary text-on-primary cursor-pointer"
                }`}
                style={{
                    boxShadow: isLocked
                        ? "0 4px 0 var(--color-outline-variant)"
                        : isCompleted
                          ? "0 4px 0 var(--color-on-secondary-container)"
                          : "0 4px 0 var(--color-primary-dim)",
                }}
            >
                {isLocked ? (
                    <Icon name="lock" size="md" />
                ) : isCompleted ? (
                    <Icon name="check" size="md" variant="filled" />
                ) : (
                    index + 1
                )}
            </div>

            {/* Tap-to-open popover */}
            {isPopoverOpen && !isLocked && (
                <div className="absolute bottom-[calc(100%+14px)] left-1/2 -translate-x-1/2 w-56 bg-surface-container-lowest rounded-2xl shadow-xl px-4 py-3 z-30">
                    <p className="font-bold text-sm text-on-surface mb-0.5 truncate">
                        {lesson.title}
                    </p>
                    <div className="flex items-center gap-2 mb-3">
                        <span className="text-xs text-on-surface-variant">
                            Урок {index + 1} из {total}
                        </span>
                    </div>
                    <Link
                        href={`/exercise/${lesson.lessonId}`}
                        onClick={onClosePopover}
                        className="flex items-center justify-center gap-1 w-full py-2.5 rounded-full bg-primary text-on-primary text-sm font-bold shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 tonal-transition"
                    >
                        {isCompleted ? "Повторить" : "Начать урок"}
                        <Icon name="arrow_forward" size="sm" />
                    </Link>
                    {/* Arrow pointing down */}
                    <div className="absolute -bottom-2 left-1/2 -translate-x-1/2 w-4 h-2 overflow-hidden">
                        <div className="w-3 h-3 bg-surface-container-lowest rotate-45 -translate-y-1.5 mx-auto" />
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
                    isLocked ? "text-on-surface-variant" : "text-on-surface"
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
                className="absolute left-1/2 -translate-x-1/2 top-7 bottom-7 w-1 rounded-full bg-surface-container-highest"
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
                        {/* Completed segment: solid primary */}
                        {lessonIndex < lessons.length - 1 && isPassedOrActive && !isCurrentlyActive && (
                            <div
                                className="absolute left-1/2 -translate-x-1/2 top-7 w-1 rounded-full bg-primary"
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
                                    stroke="var(--color-primary)"
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
