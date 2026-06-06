"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import type { LessonSummary } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { Chip } from "@/shared/components/chip";

// serpentine: center → left → right → left → right
const OFFSETS = [0, -1, 1, -1, 1] as const;

interface LessonNodeProps {
    lesson: LessonSummary;
    index: number;
    offset: number;
    isPopoverOpen: boolean;
    onTogglePopover: () => void;
    onClosePopover: () => void;
}

function LessonNode({
    lesson,
    index,
    offset,
    isPopoverOpen,
    onTogglePopover,
    onClosePopover,
}: LessonNodeProps) {
    const isLocked = lesson.status === "locked";
    const isCompleted = lesson.status === "completed";
    const isActive = lesson.status === "available" || lesson.status === "in_progress";
    const wrapRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!isPopoverOpen) return;
        function handleOutsideClick(e: MouseEvent) {
            if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) {
                onClosePopover();
            }
        }
        document.addEventListener("mousedown", handleOutsideClick);
        return () => document.removeEventListener("mousedown", handleOutsideClick);
    }, [isPopoverOpen, onClosePopover]);

    const cfg = isCompleted
        ? { bg: "var(--success)", color: "#fff", icon: "check" as const, shadow: "0 8px 20px var(--success-soft), var(--sh-1)" }
        : isActive
            ? { bg: "var(--primary)", color: "#fff", icon: "play" as const, shadow: "0 8px 20px var(--primary-soft), var(--sh-1)" }
            : { bg: "var(--surface-2)", color: "var(--ink-4)", icon: "lock" as const, shadow: "var(--sh-inner)" };

    const justify = offset === 1 ? "flex-end" : offset === -1 ? "flex-start" : "center";

    return (
        <div className={"lp-row" + (offset === 1 ? " right" : "")} style={{ justifyContent: justify }}>
            <div className="lp-node-wrap" ref={wrapRef}>
                {isActive && <span className="lp-pulse" />}
                <button
                    className={"lp-node" + (isLocked ? " locked" : "")}
                    style={{ background: cfg.bg, color: cfg.color, boxShadow: cfg.shadow }}
                    onClick={isLocked ? undefined : onTogglePopover}
                    disabled={isLocked}
                    title={`Урок ${index + 1} · ${lesson.title}`}
                >
                    <Icon name={cfg.icon} size={28} />
                </button>
                <div className="lp-meta">
                    <span className="lp-type">Урок {index + 1}</span>
                    <span className="lp-title" style={isLocked ? { color: "var(--ink-4)" } : undefined}>
                        {lesson.title}
                    </span>
                    <span className="lp-xp">
                        <Icon name="bolt" size={13} />
                        60 XP
                    </span>
                </div>

                {isPopoverOpen && !isLocked && (
                    <div
                        className="card fade-up"
                        style={{
                            position: "absolute",
                            top: 84,
                            ...(offset === 1 ? { right: 0 } : { left: 0 }),
                            width: 280,
                            boxShadow: "var(--sh-3)",
                            padding: 16,
                            zIndex: 30,
                        }}
                    >
                        <div className="row between" style={{ marginBottom: 8 }}>
                            <span className="eyebrow">Урок {index + 1}</span>
                            {isCompleted && (
                                <Chip tone="olive" size="sm">
                                    ✓
                                </Chip>
                            )}
                        </div>

                        <div className="h4" style={{ marginBottom: 10 }}>
                            {lesson.title}
                        </div>

                        <div className="row gap-4 small num" style={{ marginBottom: 14 }}>
                            <span className="row gap-1">
                                <Icon name="layers" size={13} /> 6 упр.
                            </span>
                            <span className="row gap-1">
                                <Icon name="bolt" size={13} /> 60 XP
                            </span>
                        </div>

                        <Link href={`/session/${lesson.lessonId}`} onClick={onClosePopover}>
                            <Button
                                variant={isCompleted ? "secondary" : "accent"}
                                size="md"
                                fullWidth
                                iconRightName="arrow-right"
                            >
                                {isCompleted ? "Повторить" : "Продолжить"}
                            </Button>
                        </Link>
                    </div>
                )}
            </div>
        </div>
    );
}

interface LessonPathProps {
    lessons: LessonSummary[];
    tone?: string;
}

export function LessonPath({ lessons }: LessonPathProps) {
    const [openPopoverIndex, setOpenPopoverIndex] = useState<number | null>(null);

    function togglePopover(index: number) {
        setOpenPopoverIndex((prev) => (prev === index ? null : index));
    }

    return (
        <div className="lesson-path">
            {lessons.map((l, i) => (
                <LessonNode
                    key={l.lessonId}
                    lesson={l}
                    index={i}
                    offset={OFFSETS[i % OFFSETS.length]}
                    isPopoverOpen={openPopoverIndex === i}
                    onTogglePopover={() => togglePopover(i)}
                    onClosePopover={() => setOpenPopoverIndex(null)}
                />
            ))}
            <div className="lp-row" style={{ justifyContent: "center" }}>
                <div className="lp-trophy">
                    <span className="ic">
                        <Icon name="trophy" size={26} />
                    </span>
                    <span>Босс навыка</span>
                </div>
            </div>
        </div>
    );
}
