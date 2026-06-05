"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import type { LessonSummary } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";

const OFFSETS = [0, 80, 120, 80, 0, -80, -120];
const NODE_H = 104;

interface LessonNodeProps {
    lesson: LessonSummary;
    index: number;
    total: number;
    tone: string;
    isPopoverOpen: boolean;
    onTogglePopover: () => void;
    onClosePopover: () => void;
}

function LessonNode({
    lesson,
    index,
    total,
    tone,
    isPopoverOpen,
    onTogglePopover,
    onClosePopover,
}: LessonNodeProps) {
    const isLocked = lesson.status === "locked";
    const isCompleted = lesson.status === "completed";
    const isActive = lesson.status === "available" || lesson.status === "in_progress";
    const nodeRef = useRef<HTMLDivElement>(null);

    const toneColor = `var(--${tone})`;
    const size = 64;

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

    const palette = {
        completed: { bg: toneColor, fg: "white", border: "none" },
        active: { bg: "var(--indigo)", fg: "white", border: "none", pulse: true },
        locked: { bg: "var(--bg-2)", fg: "var(--ink-4)", border: "1px solid var(--line-2)" },
    };
    const p = isCompleted ? palette.completed : isActive ? palette.active : palette.locked;

    const icon = isCompleted ? "check" : isActive ? "play" : "lock";

    return (
        <div style={{ position: "relative" }} ref={nodeRef}>
            {/* Pulse ring */}
            {isActive && (
                <div
                    style={{
                        position: "absolute",
                        left: -10,
                        top: -10,
                        width: size + 20,
                        height: size + 20,
                        borderRadius: "50%",
                        background: "var(--indigo-soft)",
                        animation: "pulse 2s ease-in-out infinite",
                        pointerEvents: "none",
                    }}
                />
            )}

            <button
                onClick={isLocked ? undefined : onTogglePopover}
                disabled={isLocked}
                title={`Урок ${index + 1} · ${lesson.title}`}
                style={{
                    position: "relative",
                    zIndex: 2,
                    width: size,
                    height: size,
                    borderRadius: "50%",
                    background: p.bg,
                    color: p.fg,
                    border: p.border || "none",
                    boxShadow: isActive ? "var(--sh-3)" : "var(--sh-1)",
                    cursor: isLocked ? "not-allowed" : "pointer",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    transition: "transform 0.15s",
                    padding: 0,
                }}
            >
                <Icon name={icon} size={24} />
            </button>

            {/* Label */}
            <div
                style={{
                    position: "absolute",
                    top: size + 6,
                    left: "50%",
                    transform: "translateX(-50%)",
                    width: 140,
                    textAlign: "center",
                    pointerEvents: "none",
                }}
            >
                <div
                    style={{
                        fontSize: 9,
                        fontFamily: "var(--f-mono)",
                        color: "var(--ink-4)",
                        letterSpacing: 0.5,
                    }}
                >
                    УРОК {index + 1}
                </div>
                <div
                    style={{
                        fontSize: 11,
                        fontWeight: 500,
                        color: isLocked ? "var(--ink-4)" : "var(--ink)",
                        lineHeight: 1.3,
                        marginTop: 1,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                    }}
                >
                    {lesson.title}
                </div>
            </div>

            {/* Popover */}
            {isPopoverOpen && !isLocked && (
                <div
                    style={{
                        position: "absolute",
                        top: size + 52,
                        left: "50%",
                        transform: "translateX(-50%)",
                        width: 280,
                        background: "var(--surface)",
                        borderRadius: 16,
                        border: "1px solid var(--line-2)",
                        boxShadow: "var(--sh-3)",
                        padding: 16,
                        zIndex: 30,
                    }}
                >
                    {/* Arrow */}
                    <div
                        style={{
                            position: "absolute",
                            top: -7,
                            left: "50%",
                            transform: "translateX(-50%) rotate(45deg)",
                            width: 12,
                            height: 12,
                            background: "var(--surface)",
                            borderTop: "1px solid var(--line-2)",
                            borderLeft: "1px solid var(--line-2)",
                        }}
                    />

                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            marginBottom: 8,
                        }}
                    >
                        <div
                            style={{
                                fontSize: 11,
                                color: toneColor,
                                letterSpacing: 1.5,
                                textTransform: "uppercase",
                                fontWeight: 500,
                                fontFamily: "var(--f-mono)",
                            }}
                        >
                            Урок {index + 1}
                        </div>
                        {isCompleted && (
                            <Chip tone="olive" size="sm">
                                ✓
                            </Chip>
                        )}
                    </div>

                    <div
                        style={{
                            fontSize: 16,
                            fontWeight: 500,
                            letterSpacing: -0.2,
                            marginBottom: 10,
                        }}
                    >
                        {lesson.title}
                    </div>

                    <div
                        style={{
                            display: "flex",
                            gap: 14,
                            marginBottom: 14,
                            fontSize: 12,
                            color: "var(--ink-3)",
                            fontFamily: "var(--f-mono)",
                        }}
                    >
                        <span>
                            <Icon name="layers" size={11} /> 6 упр.
                        </span>
                        <span>
                            <Icon name="bolt" size={11} /> 60 XP
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
    );
}

interface LessonPathProps {
    lessons: LessonSummary[];
    tone?: string;
}

export function LessonPath({ lessons, tone = "indigo" }: LessonPathProps) {
    const [openPopoverIndex, setOpenPopoverIndex] = useState<number | null>(null);
    const toneColor = `var(--${tone})`;

    function togglePopover(index: number) {
        setOpenPopoverIndex((prev) => (prev === index ? null : index));
    }

    return (
        <div style={{ position: "relative", height: lessons.length * NODE_H, marginBottom: 8 }}>
            {/* Connectors SVG */}
            <svg
                width="100%"
                height={lessons.length * NODE_H}
                style={{ position: "absolute", inset: 0, pointerEvents: "none", overflow: "visible" }}
                viewBox={`-240 0 480 ${lessons.length * NODE_H}`}
                preserveAspectRatio="xMidYMid meet"
            >
                {lessons.slice(0, -1).map((l, i) => {
                    const from = { x: OFFSETS[i % OFFSETS.length], y: i * NODE_H + 44 };
                    const to = { x: OFFSETS[(i + 1) % OFFSETS.length], y: (i + 1) * NODE_H + 44 };
                    const next = lessons[i + 1];
                    const dashed = next.status === "locked";
                    const isCurrentActive = l.status === "available" || l.status === "in_progress";
                    const stroke =
                        (l.status === "completed" || isCurrentActive) && next.status !== "locked"
                            ? toneColor
                            : "var(--line-2)";
                    const midY = (from.y + to.y) / 2;
                    const d = `M ${from.x} ${from.y} C ${from.x} ${midY}, ${to.x} ${midY}, ${to.x} ${to.y}`;
                    return (
                        <path
                            key={i}
                            d={d}
                            fill="none"
                            stroke={stroke}
                            strokeWidth={2.5}
                            strokeLinecap="round"
                            strokeDasharray={dashed ? "4 6" : isCurrentActive ? "10 10" : "none"}
                            opacity={dashed ? 0.5 : 1}
                            className={isCurrentActive ? "path-dash-animated" : undefined}
                        />
                    );
                })}
            </svg>

            {/* Lesson nodes */}
            {lessons.map((l, i) => (
                <div
                    key={l.lessonId}
                    style={{
                        position: "absolute",
                        top: i * NODE_H,
                        left: "50%",
                        transform: `translateX(calc(-50% + ${OFFSETS[i % OFFSETS.length]}px))`,
                    }}
                >
                    <LessonNode
                        lesson={l}
                        index={i}
                        total={lessons.length}
                        tone={tone}
                        isPopoverOpen={openPopoverIndex === i}
                        onTogglePopover={() => togglePopover(i)}
                        onClosePopover={() => setOpenPopoverIndex(null)}
                    />
                </div>
            ))}
        </div>
    );
}
