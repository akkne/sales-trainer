"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { LessonPath } from "@/shared/components/lesson-path";
import { StatsWidget } from "@/features/layout/components/stats-widget";
import { useSkillTree, useSkills, type SkillTreeNode } from "@/features/skills/hooks/use-skill-tree";
import { useLessonsForSkill } from "@/features/exercise/hooks/use-lesson";
import { useSelectedSkillStore } from "@/shared/stores/selected-skill-store";
import { Icon } from "@/shared/components/icon";
import { Progress } from "@/shared/components/progress";
import { ErrorState } from "@/shared/components/error-state";
import { SKILL_STAGES, getStageMeta } from "@/features/skills/constants/skill-stages";

function Spinner() {
    return (
        <div
            style={{
                width: 40,
                height: 40,
                borderRadius: "50%",
                border: "4px solid var(--primary)",
                borderTopColor: "transparent",
                animation: "spin 0.8s linear infinite",
            }}
        />
    );
}

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
            <div className="row center" style={{ padding: "80px 0" }}>
                <Spinner />
            </div>
        );
    }

    const sorted = (lessons ?? []).slice().sort((a, b) => a.orderInTopic - b.orderInTopic);
    const completedCount = sorted.filter((l) => l.status === "completed").length;
    const totalCount = sorted.length;
    const progressPercent = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

    return (
        <>
            {/* Skill band header */}
            <div className="card-pad" style={{ borderBottom: "1px solid var(--line)" }}>
                <div className="skill-band">
                    <div>
                        <span className="eyebrow">
                            Навык<span className="dot">·</span>
                            <span className="num">{completedCount}/{totalCount} уроков</span>
                        </span>
                        <h2 className="h2" style={{ margin: "10px 0 0" }}>{skillTitle}</h2>
                    </div>
                    <div className="skill-band-prog">
                        <div className="row between small" style={{ marginBottom: 8 }}>
                            <span>{progressPercent}% пройдено</span>
                            <span className="num">{totalCount - completedCount} осталось</span>
                        </div>
                        <Progress value={completedCount} max={Math.max(1, totalCount)} tone="indigo" height={10} />
                    </div>
                </div>
            </div>

            {/* Lesson path */}
            <div className="lp-scroll dotted">
                {sorted.length === 0 ? (
                    <div className="empty">
                        <div className="ic">
                            <Icon name="folder" size="xl" />
                        </div>
                        <p className="h4" style={{ marginBottom: 6 }}>Уроки ещё не добавлены</p>
                        <p className="small" style={{ maxWidth: 280, margin: "0 auto" }}>
                            Попроси администратора добавить уроки
                        </p>
                    </div>
                ) : (
                    <LessonPath lessons={sorted} />
                )}
            </div>
        </>
    );
}

function SkillRow({
    skill,
    selected,
    onClick,
}: {
    skill: SkillTreeNode;
    selected: boolean;
    onClick: () => void;
}) {
    const completed = skill.totalLessonCount > 0 && skill.completedLessonCount === skill.totalLessonCount;
    return (
        <button className={"skill-row" + (selected ? " active" : "")} onClick={onClick}>
            <span className="skill-ic">
                <Icon
                    name={skill.iconName as Parameters<typeof Icon>[0]["name"]}
                    size={16}
                    color={selected ? "var(--primary)" : "var(--ink-3)"}
                />
            </span>
            <span className="skill-name" style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {skill.title}
            </span>
            {completed && <Icon name="check" size={16} color="var(--success)" />}
        </button>
    );
}

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

    const skillsDone = skills.filter((s) => s.status === "completed").length;

    return (
        <div className="stage-group">
            <button className="stage-head" onClick={() => setOpen((v) => !v)} aria-expanded={open}>
                <span className="chev" style={{ transform: open ? "rotate(90deg)" : "none", display: "inline-flex" }}>
                    <Icon name="chevron-right" size={16} />
                </span>
                <span className="stage-dot" style={{ background: meta.accent }} />
                <span className="stage-name">{meta.label}</span>
                <span className="stage-ratio num">
                    {skillsDone}/{skills.length}
                </span>
            </button>

            {open && (
                <div className="stage-skills">
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
            <div className="col gap-2" style={{ paddingTop: 4 }}>
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
            <p className="small" style={{ textAlign: "center", paddingTop: 16, lineHeight: 1.5 }}>
                Нет активных навыков.{" "}
                <Link href="/profile" style={{ color: "var(--primary)", fontWeight: 600 }}>
                    Добавь в профиле
                </Link>
            </p>
        );
    }

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
        <div className="col gap-1" style={{ marginTop: 14 }}>
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

export default function SkillTreePage() {
    const { data: skillTreeData, isLoading, isError, refetch } = useSkillTree();
    const { selectedSkill } = useSelectedSkillStore();

    if (isLoading) {
        return (
            <div className="row center" style={{ minHeight: "100vh" }}>
                <Spinner />
            </div>
        );
    }

    if (isError || !skillTreeData) {
        return (
            <div className="row center" style={{ minHeight: "100vh" }}>
                <ErrorState
                    title="Не удалось загрузить дерево навыков"
                    onRetry={() => refetch()}
                />
            </div>
        );
    }

    return (
        <div className="page">
            <div className="container">
                <div className="tree-grid-a">
                    {/* LEFT — Skills sidebar */}
                    <aside className="card card-pad">
                        <span className="eyebrow muted">Навыки</span>
                        <SkillSidebar />
                    </aside>

                    {/* CENTER — Lesson path */}
                    <main className="tree-center card">
                        {selectedSkill ? (
                            <SkillLessonView skillSlug={selectedSkill.slug} skillTitle={selectedSkill.title} />
                        ) : (
                            <div className="empty">
                                <div className="ic">
                                    <Icon name="compass" size="xl" />
                                </div>
                                <p className="h4" style={{ marginBottom: 6 }}>Выбери навык</p>
                                <p className="small" style={{ maxWidth: 280, margin: "0 auto" }}>
                                    Нажми на навык слева, чтобы увидеть уроки
                                </p>
                            </div>
                        )}
                    </main>

                    {/* RIGHT — Stats */}
                    <aside>
                        <StatsWidget
                            currentStreakDayCount={skillTreeData.currentStreakDayCount}
                            totalXpAmount={skillTreeData.totalXpAmount}
                            weeklyXpAmount={skillTreeData.weeklyXpAmount}
                        />
                    </aside>
                </div>
            </div>
        </div>
    );
}
