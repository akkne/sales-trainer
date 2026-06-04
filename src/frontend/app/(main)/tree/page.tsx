"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { LessonPath } from "@/components/ui/LessonPath";
import { StatsWidget } from "@/components/layout/StatsWidget";
import { useSkillTree, useSkills } from "@/lib/hooks/useSkillTree";
import { useLessonsForSkill } from "@/lib/hooks/useLesson";
import { useSelectedSkillStore } from "@/lib/store/selectedSkillStore";
import { Icon } from "@/components/ui/Icon";
import { ErrorState } from "@/components/ui/ErrorState";
import { Progress } from "@/components/ui/Progress";
import { Button } from "@/components/ui/Button";
import { SKILL_STAGES, getStageMeta } from "@/lib/skillStages";

// ── Skill row in sidebar ─────────────────────────────────────────────────────

interface SkillRowProps {
    skill: {
        skillId: string;
        slug: string;
        title: string;
        iconName: string;
        status: string;
        completedLessonCount: number;
        totalLessonCount: number;
    };
    selected: boolean;
    onClick: () => void;
}

function SkillRow({ skill, selected, onClick }: SkillRowProps) {
    const pct = skill.totalLessonCount > 0
        ? Math.round((skill.completedLessonCount / skill.totalLessonCount) * 100)
        : 0;
    const isDone = skill.status === "completed";
    const toneColor = "var(--indigo)";

    return (
        <button
            onClick={onClick}
            style={{
                display: "flex",
                alignItems: "center",
                gap: 12,
                padding: "12px",
                borderRadius: 12,
                background: selected ? "var(--surface)" : "transparent",
                border: selected ? "1px solid var(--line-2)" : "1px solid transparent",
                boxShadow: selected ? "var(--sh-1)" : "none",
                cursor: "pointer",
                textAlign: "left",
                fontFamily: "var(--f-sans)",
                transition: "background 0.15s",
                width: "100%",
            }}
        >
            <div
                style={{
                    width: 36,
                    height: 36,
                    borderRadius: 10,
                    background: isDone ? toneColor : `color-mix(in oklch, ${toneColor} 14%, var(--bg))`,
                    color: isDone ? "white" : toneColor,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    flexShrink: 0,
                }}
            >
                <Icon name={isDone ? "check" : "compass"} size="sm" />
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
                <div
                    style={{
                        fontSize: 14,
                        fontWeight: 500,
                        color: "var(--ink)",
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                    }}
                >
                    {skill.title}
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 3 }}>
                    <div
                        style={{
                            flex: 1,
                            height: 3,
                            background: "var(--line)",
                            borderRadius: 2,
                            overflow: "hidden",
                        }}
                    >
                        <div
                            style={{
                                height: "100%",
                                width: `${pct}%`,
                                background: toneColor,
                            }}
                        />
                    </div>
                    <div
                        style={{
                            fontSize: 10,
                            color: "var(--ink-3)",
                            fontFamily: "var(--f-mono)",
                            fontVariantNumeric: "tabular-nums",
                        }}
                    >
                        {skill.completedLessonCount}/{skill.totalLessonCount}
                    </div>
                </div>
            </div>
        </button>
    );
}

// ── Skill lesson view ─────────────────────────────────────────────────────────

function SkillLessonView({
    skillSlug,
    skillTitle,
}: {
    skillSlug: string;
    skillTitle: string;
}) {
    const { data: lessons, isLoading } = useLessonsForSkill(skillSlug);

    if (isLoading) {
        return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", padding: "80px 0" }}>
                <div
                    style={{
                        width: 40,
                        height: 40,
                        borderRadius: "50%",
                        border: "4px solid var(--indigo)",
                        borderTopColor: "transparent",
                        animation: "spin 0.8s linear infinite",
                    }}
                />
            </div>
        );
    }

    const sorted = (lessons ?? []).slice().sort((a, b) => a.orderInTopic - b.orderInTopic);
    const completedCount = sorted.filter((l) => l.status === "completed").length;
    const totalCount = sorted.length;
    const progressPercent = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

    return (
        <>
            {/* Skill header */}
            <div
                style={{
                    padding: "28px 48px 20px",
                    background: "var(--bg)",
                    borderBottom: "1px solid var(--line)",
                }}
            >
                <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 24 }}>
                    <div style={{ flex: 1, minWidth: 0 }}>
                        <div
                            style={{
                                fontSize: 12,
                                color: "var(--indigo)",
                                letterSpacing: 1.5,
                                textTransform: "uppercase",
                                fontWeight: 500,
                                marginBottom: 6,
                                fontFamily: "var(--f-mono)",
                            }}
                        >
                            НАВЫК · {completedCount}/{totalCount} уроков
                        </div>
                        <h1 style={{ margin: 0, fontSize: 32, letterSpacing: -0.8, fontWeight: 500 }}>
                            {skillTitle}
                        </h1>
                    </div>

                    <div style={{ minWidth: 220 }}>
                        <Progress value={completedCount} max={totalCount} tone="indigo" />
                        <div
                            style={{
                                marginTop: 6,
                                display: "flex",
                                justifyContent: "space-between",
                                fontSize: 11,
                                color: "var(--ink-3)",
                                fontFamily: "var(--f-mono)",
                            }}
                        >
                            <span>{progressPercent}% ПРОЙДЕНО</span>
                            <span>{totalCount - completedCount} осталось</span>
                        </div>
                    </div>
                </div>
            </div>

            {/* Lesson path */}
            <div
                style={{
                    flex: 1,
                    overflowY: "auto",
                    padding: "32px 40px 120px",
                    backgroundImage: "radial-gradient(var(--line) 1px, transparent 1px)",
                    backgroundSize: "24px 24px",
                    backgroundColor: "var(--surface)",
                }}
            >
                <div style={{ maxWidth: 480, margin: "0 auto" }}>
                    {sorted.length === 0 ? (
                        <div
                            style={{
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "center",
                                justifyContent: "center",
                                padding: "64px 0",
                                gap: 12,
                                textAlign: "center",
                            }}
                        >
                            <div
                                style={{
                                    width: 64,
                                    height: 64,
                                    borderRadius: "50%",
                                    background: "var(--bg-2)",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                }}
                            >
                                <Icon name="folder" size="xl" color="var(--ink-4)" />
                            </div>
                            <p style={{ fontSize: 18, fontWeight: 500, color: "var(--ink)" }}>
                                Уроки ещё не добавлены
                            </p>
                            <p style={{ fontSize: 13, color: "var(--ink-3)", maxWidth: 280 }}>
                                Попроси администратора добавить уроки
                            </p>
                        </div>
                    ) : (
                        <LessonPath lessons={sorted} />
                    )}
                </div>
            </div>
        </>
    );
}

// ── Left sidebar ──────────────────────────────────────────────────────────────

// ── Stage group ───────────────────────────────────────────────────────────────

interface StageGroupProps {
    stageKey: string;
    skills: NonNullable<ReturnType<typeof useSkills>["data"]>;
    selectedSlug: string | undefined;
    onSelect: (skill: { slug: string; title: string; iconName: string }) => void;
    defaultOpen: boolean;
}

function StageGroup({ stageKey, skills, selectedSlug, onSelect, defaultOpen }: StageGroupProps) {
    const meta = getStageMeta(stageKey);
    const [open, setOpen] = useState(defaultOpen);
    useEffect(() => {
        if (defaultOpen) setOpen(true);
    }, [defaultOpen]);

    const totalLessons = skills.reduce((sum, s) => sum + s.totalLessonCount, 0);
    const completedLessons = skills.reduce((sum, s) => sum + s.completedLessonCount, 0);
    const skillsDone = skills.filter((s) => s.status === "completed").length;
    const pct = totalLessons > 0 ? Math.round((completedLessons / totalLessons) * 100) : 0;

    return (
        <div style={{ marginBottom: 10 }}>
            <button
                onClick={() => setOpen((v) => !v)}
                style={{
                    width: "100%",
                    display: "flex",
                    alignItems: "center",
                    gap: 10,
                    padding: "8px 8px 8px 6px",
                    background: "transparent",
                    border: "none",
                    cursor: "pointer",
                    textAlign: "left",
                    fontFamily: "var(--f-sans)",
                }}
                aria-expanded={open}
            >
                <span
                    style={{
                        width: 14,
                        height: 14,
                        display: "inline-flex",
                        alignItems: "center",
                        justifyContent: "center",
                        color: "var(--ink-3)",
                        transition: "transform 0.18s",
                        transform: open ? "rotate(90deg)" : "rotate(0deg)",
                    }}
                >
                    <Icon name="chevron-right" size="xs" />
                </span>
                <span
                    style={{
                        width: 8,
                        height: 8,
                        borderRadius: 4,
                        background: meta.accent,
                        flexShrink: 0,
                    }}
                />
                <span
                    style={{
                        flex: 1,
                        fontSize: 11,
                        color: "var(--ink-2)",
                        letterSpacing: 1.2,
                        textTransform: "uppercase",
                        fontWeight: 600,
                        fontFamily: "var(--f-mono)",
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                    }}
                >
                    {meta.label}
                </span>
                <span
                    style={{
                        fontSize: 10,
                        color: "var(--ink-3)",
                        fontFamily: "var(--f-mono)",
                        fontVariantNumeric: "tabular-nums",
                    }}
                >
                    {skillsDone}/{skills.length}
                </span>
            </button>

            <div style={{ paddingLeft: 18, paddingRight: 2, marginTop: 2, marginBottom: open ? 6 : 0 }}>
                <div
                    style={{
                        height: 2,
                        background: "var(--line)",
                        borderRadius: 2,
                        overflow: "hidden",
                    }}
                >
                    <div
                        style={{
                            height: "100%",
                            width: `${pct}%`,
                            background: meta.accent,
                            transition: "width 0.25s",
                        }}
                    />
                </div>
            </div>

            {open && (
                <div style={{ display: "flex", flexDirection: "column", gap: 2, marginTop: 4 }}>
                    {skills.map((skill) => (
                        <SkillRow
                            key={skill.skillId}
                            skill={skill}
                            selected={selectedSlug === skill.slug}
                            onClick={() =>
                                onSelect({
                                    slug: skill.slug,
                                    title: skill.title,
                                    iconName: skill.iconName,
                                })
                            }
                        />
                    ))}
                </div>
            )}
        </div>
    );
}

function SkillSidebar() {
    const { data: allSkills, isLoading } = useSkills();
    const { selectedSkill, setSelectedSkill } = useSelectedSkillStore();

    const enrolledSkills = (allSkills ?? []).filter((s) => s.status !== "locked");

    useEffect(() => {
        if (!selectedSkill && enrolledSkills.length > 0) {
            const first = enrolledSkills[0];
            setSelectedSkill({
                slug: first.slug,
                title: first.title,
                iconName: first.iconName,
            });
        }
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [allSkills]);

    if (isLoading) {
        return (
            <div style={{ display: "flex", flexDirection: "column", gap: 8, paddingTop: 4 }}>
                {[1, 2, 3].map((i) => (
                    <div
                        key={i}
                        style={{
                            height: 56,
                            borderRadius: 12,
                            background: "var(--bg-2)",
                            animation: "pulse 2s ease-in-out infinite",
                        }}
                    />
                ))}
            </div>
        );
    }

    if (enrolledSkills.length === 0) {
        return (
            <div style={{ textAlign: "center", paddingTop: 16 }}>
                <p style={{ fontSize: 12, color: "var(--ink-3)", lineHeight: 1.5 }}>
                    Нет активных навыков.{" "}
                    <Link href="/profile" style={{ color: "var(--indigo)", fontWeight: 600 }}>
                        Добавь в профиле
                    </Link>
                </p>
            </div>
        );
    }

    // Group by stage. Known stages first (in fixed order), then any unknown stages alphabetically.
    const byStage = new Map<string, typeof enrolledSkills>();
    for (const skill of enrolledSkills) {
        const key = skill.stage || "general";
        const bucket = byStage.get(key) ?? [];
        bucket.push(skill);
        byStage.set(key, bucket);
    }
    const knownOrder = SKILL_STAGES.map((s) => s.key);
    const orderedStages: string[] = [
        ...knownOrder.filter((k) => byStage.has(k)),
        ...Array.from(byStage.keys()).filter((k) => !knownOrder.includes(k)).sort(),
    ];

    return (
        <div style={{ display: "flex", flexDirection: "column" }}>
            {orderedStages.map((stageKey) => {
                const stageSkills = (byStage.get(stageKey) ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
                const containsSelected = stageSkills.some((s) => s.slug === selectedSkill?.slug);
                return (
                    <StageGroup
                        key={stageKey}
                        stageKey={stageKey}
                        skills={stageSkills}
                        selectedSlug={selectedSkill?.slug}
                        onSelect={setSelectedSkill}
                        defaultOpen={containsSelected}
                    />
                );
            })}
        </div>
    );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function SkillTreePage() {
    const { data: skillTreeData, isLoading, isError, refetch } = useSkillTree();
    const { selectedSkill } = useSelectedSkillStore();

    if (isLoading) {
        return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh" }}>
                <div
                    style={{
                        width: 40,
                        height: 40,
                        borderRadius: "50%",
                        border: "4px solid var(--indigo)",
                        borderTopColor: "transparent",
                        animation: "spin 0.8s linear infinite",
                    }}
                />
            </div>
        );
    }

    if (isError || !skillTreeData) {
        return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh" }}>
                <ErrorState
                    title="Не удалось загрузить дерево навыков"
                    onRetry={() => refetch()}
                />
            </div>
        );
    }

    return (
        <div style={{ minHeight: "100vh", background: "var(--bg)" }}>
            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "300px 1fr 320px",
                    gap: 0,
                    height: "calc(100vh - 60px)",
                }}
                className="hidden md:grid"
            >
                {/* LEFT — Skills list */}
                <aside
                    style={{
                        borderRight: "1px solid var(--line)",
                        background: "var(--surface-2)",
                        padding: "28px 20px",
                        overflowY: "auto",
                    }}
                >
                    <div
                        style={{
                            fontSize: 11,
                            color: "var(--ink-3)",
                            letterSpacing: 1.5,
                            textTransform: "uppercase",
                            fontWeight: 500,
                            marginBottom: 14,
                            fontFamily: "var(--f-mono)",
                            paddingLeft: 6,
                        }}
                    >
                        НАВЫКИ
                    </div>
                    <SkillSidebar />
                </aside>

                {/* CENTER — Lesson path */}
                <main style={{ display: "flex", flexDirection: "column", overflow: "hidden", position: "relative" }}>
                    {selectedSkill ? (
                        <SkillLessonView skillSlug={selectedSkill.slug} skillTitle={selectedSkill.title} />
                    ) : (
                        <div
                            style={{
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "center",
                                justifyContent: "center",
                                flex: 1,
                                gap: 16,
                                textAlign: "center",
                            }}
                        >
                            <div
                                style={{
                                    width: 80,
                                    height: 80,
                                    borderRadius: "50%",
                                    background: "var(--bg-2)",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                }}
                            >
                                <Icon name="compass" size="xl" color="var(--ink-4)" />
                            </div>
                            <p style={{ fontSize: 20, fontWeight: 500, color: "var(--ink)" }}>Выбери навык</p>
                            <p style={{ fontSize: 13, color: "var(--ink-3)", maxWidth: 280 }}>
                                Нажми на навык слева, чтобы увидеть уроки
                            </p>
                        </div>
                    )}
                </main>

                {/* RIGHT — Stats */}
                <aside
                    style={{
                        padding: "28px 28px 28px 24px",
                        borderLeft: "1px solid var(--line)",
                        background: "var(--surface-2)",
                        overflowY: "auto",
                    }}
                >
                    <StatsWidget
                        currentStreakDayCount={skillTreeData.currentStreakDayCount}
                        totalXpAmount={skillTreeData.totalXpAmount}
                        weeklyXpAmount={skillTreeData.weeklyXpAmount}
                    />
                </aside>
            </div>

            {/* Mobile layout */}
            <div className="md:hidden" style={{ padding: "0" }}>
                <div style={{ padding: "16px", paddingBottom: 32 }}>
                    <div
                        style={{
                            fontSize: 11,
                            color: "var(--ink-3)",
                            letterSpacing: 1.5,
                            textTransform: "uppercase",
                            fontWeight: 500,
                            marginBottom: 12,
                            fontFamily: "var(--f-mono)",
                        }}
                    >
                        НАВЫКИ
                    </div>
                    <SkillSidebar />
                </div>
                {selectedSkill && (
                    <div style={{ padding: "0 16px 32px" }}>
                        <SkillLessonView skillSlug={selectedSkill.slug} skillTitle={selectedSkill.title} />
                    </div>
                )}
            </div>
        </div>
    );
}
