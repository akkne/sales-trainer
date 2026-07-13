"use client";

import Link from "next/link";
import { use } from "react";
import { useLessonsForSkill } from "@/features/exercise/hooks/use-lesson";
import { useSkills } from "@/features/skills/hooks/use-skill-tree";
import { Icon } from "@/shared/components/icon";
import type { LessonSummary } from "@/features/exercise/hooks/use-lesson";

interface SkillPageProps {
    params: Promise<{ id: string }>;
}

// ─── Chip colour map (DESIGN_SPEC §1.1) ─────────────────────────────────────
const CHIP_MAP: Record<string, { bg: string; color: string }> = {
    choice:     { bg: "#EAF2FF", color: "#2F6FE0" },
    blank:      { bg: "#E9F7EF", color: "#1F9E5A" },
    reorder:    { bg: "#FFF1E8", color: "#D9722E" },
    match:      { bg: "#F1ECFB", color: "#6C5BD9" },
    categorize: { bg: "#FDEBF3", color: "#C44E8A" },
    spot:       { bg: "#FDECEA", color: "#D9503E" },
    rewrite:    { bg: "#EAF6F8", color: "#1E8AA0" },
    dialogue:   { bg: "#EEF0FE", color: "#4658D6" },
    evaluate:   { bg: "#F4F0E6", color: "#9A7B2E" },
    free:       { bg: "#EFEFF2", color: "#6A6A72" },
    theory:     { bg: "#EFEAFE", color: "#6C5BD9" },
    practice:   { bg: "#E9F7EF", color: "#1F9E5A" },
};
function chipStyle(kind: string) {
    return CHIP_MAP[kind] ?? CHIP_MAP.free;
}

function lessonNodeClass(status: LessonSummary["status"]) {
    if (status === "completed") return "path-tl-node done";
    if (status === "in_progress") return "path-tl-node in-progress";
    if (status === "available") return "path-tl-node available";
    return "path-tl-node locked";
}
function lessonStatusLabel(status: LessonSummary["status"]) {
    if (status === "completed") return "Завершён";
    if (status === "in_progress") return "В процессе";
    if (status === "available") return "Доступен";
    return "Заблокирован";
}
function lessonStatusClass(status: LessonSummary["status"]) {
    if (status === "completed") return "path-tl-status done";
    if (status === "in_progress") return "path-tl-status in-progress";
    return "path-tl-status available";
}
function lessonActionLabel(lesson: LessonSummary) {
    if (lesson.status === "completed") return "Повторить";
    if (lesson.status === "in_progress") return "Продолжить";
    return "Начать";
}

function Spinner() {
    return (
        <div
            aria-label="Загрузка"
            style={{
                width: 36,
                height: 36,
                borderRadius: "50%",
                border: "3px solid var(--primary-soft)",
                borderTopColor: "var(--primary)",
                animation: "spin 0.8s linear infinite",
            }}
        />
    );
}

export default function SkillPage({ params }: SkillPageProps) {
    const { id: skillSlug } = use(params);
    const { data: lessonSummaries, isLoading: lessonsLoading } = useLessonsForSkill(skillSlug);
    const { data: skills, isLoading: skillsLoading } = useSkills();

    const isLoading = lessonsLoading || skillsLoading;

    if (isLoading) {
        return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh" }}>
                <Spinner />
            </div>
        );
    }

    const skill = skills?.find((s) => s.slug === skillSlug);
    const lessons = (lessonSummaries ?? [])
        .slice()
        .sort((a, b) => a.topicOrder - b.topicOrder || a.orderInTopic - b.orderInTopic);
    const completedCount = lessons.filter((l) => l.status === "completed").length;
    const totalCount = lessons.length;
    const progressPct = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;

    const skillTitle = skill?.title ?? skillSlug;

    // FAB: first resumable/available lesson
    const fabLesson = lessons.find((l) => l.status === "in_progress" || l.status === "available");

    function skillStatusLabel(s: string | undefined) {
        if (s === "completed") return "Освоено";
        if (s === "in_progress") return "В процессе";
        return "Доступен";
    }
    function skillStatusClass(s: string | undefined) {
        if (s === "completed") return "path-skill-status done";
        if (s === "in_progress") return "path-skill-status in-progress";
        return "path-skill-status available";
    }

    return (
        <div className="skill-page-root">
            <div className="skill-page-scroll">
                {/* Back link */}
                <Link
                    href="/tree"
                    className="skill-back-link"
                >
                    <Icon name="arrow-left" size={14} />
                    Назад к навыкам
                </Link>

                {/* Skill header card — matches path-skill-header */}
                <div className="path-skill-header" style={{ marginBottom: 20 }}>
                    <div className="path-skill-header-top">
                        <div style={{ minWidth: 0 }}>
                            <h1 className="path-skill-header-title">{skillTitle}</h1>
                        </div>
                        <span className={skillStatusClass(skill?.status)}>
                            {skillStatusLabel(skill?.status)}
                        </span>
                    </div>

                    {/* Map link */}
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", marginBottom: 8 }}>
                        <Link
                            href={`/skill/${skillSlug}/map`}
                            style={{ fontSize: 12, fontWeight: 700, color: "var(--primary)", textDecoration: "none" }}
                        >
                            Карта курса →
                        </Link>
                    </div>

                    {/* Progress bar */}
                    <div className="path-prog-row">
                        <span className="path-prog-pct">{progressPct}%</span>
                        <div
                            className="path-prog-bar"
                            role="progressbar"
                            aria-valuenow={progressPct}
                            aria-valuemin={0}
                            aria-valuemax={100}
                        >
                            <div
                                className={"path-prog-fill" + (progressPct === 100 ? " complete" : "")}
                                style={{ width: `${progressPct}%` }}
                            />
                        </div>
                    </div>

                    {/* 4-cell stat grid */}
                    <div className="path-stat-grid" role="list">
                        <div className="path-stat-cell" role="listitem">
                            <div className="path-stat-label">Уроки</div>
                            <div className="path-stat-val">{totalCount}</div>
                        </div>
                        <div className="path-stat-cell" role="listitem">
                            <div className="path-stat-label">Завершено</div>
                            <div className="path-stat-val">{completedCount}</div>
                        </div>
                        <div className="path-stat-cell" role="listitem">
                            <div className="path-stat-label">Точность</div>
                            <div className="path-stat-val" aria-label="Нет данных">—</div>
                        </div>
                        <div className="path-stat-cell" role="listitem">
                            <div className="path-stat-label">Время</div>
                            <div className="path-stat-val" aria-label="Нет данных">—</div>
                        </div>
                    </div>
                </div>

                {/* Lesson timeline */}
                {lessons.length === 0 ? (
                    <div style={{ textAlign: "center", padding: "48px 0", color: "var(--ink-4)" }}>
                        <div style={{ fontSize: 32, marginBottom: 12 }}>📂</div>
                        <p style={{ fontSize: 14, fontWeight: 600, marginBottom: 6 }}>Уроки пока не добавлены</p>
                        <p style={{ fontSize: 13, color: "var(--ink-4)" }}>Попроси администратора добавить уроки</p>
                    </div>
                ) : (
                    <div className="path-timeline" role="list">
                        <div className="path-tl-line" aria-hidden="true" />

                        {lessons.map((lesson, i) => {
                            const isActive = lesson.status === "in_progress" || lesson.status === "available";
                            const chipKind = lesson.kind ?? "practice";

                            return (
                                <div
                                    key={lesson.lessonId}
                                    className="path-tl-item"
                                    role="listitem"
                                >
                                    {/* Node column */}
                                    <div className="path-tl-node-col">
                                        <div
                                            className={lessonNodeClass(lesson.status)}
                                            aria-label={`Урок ${i + 1}: ${lessonStatusLabel(lesson.status)}`}
                                        >
                                            {lesson.status === "completed" ? "✓" : i + 1}
                                        </div>
                                    </div>

                                    {/* Card */}
                                    <div className={"path-tl-card" + (lesson.status === "in_progress" ? " active" : "")}>
                                        <div className="path-tl-card-top">
                                            <span className="path-tl-eyebrow">УРОК {i + 1}</span>
                                            <span className={lessonStatusClass(lesson.status)}>
                                                {lessonStatusLabel(lesson.status)}
                                            </span>
                                            {isActive && (
                                                <span className="path-tl-action">
                                                    <Link href={`/session/${lesson.lessonId}`}>
                                                        <button
                                                            className="btn btn-accent"
                                                            style={{ padding: "5px 13px", fontSize: 12, fontWeight: 700 }}
                                                        >
                                                            {lessonActionLabel(lesson)} →
                                                        </button>
                                                    </Link>
                                                </span>
                                            )}
                                            {lesson.status === "completed" && (
                                                <span className="path-tl-action">
                                                    <Link href={`/session/${lesson.lessonId}`}>
                                                        <button
                                                            className="btn btn-secondary"
                                                            style={{ padding: "5px 13px", fontSize: 12, fontWeight: 700 }}
                                                        >
                                                            Повторить
                                                        </button>
                                                    </Link>
                                                </span>
                                            )}
                                        </div>

                                        <p className="path-tl-title">{lesson.title}</p>

                                        <div className="path-tl-chips">
                                            <span className="path-tl-chip" style={chipStyle(chipKind)}>
                                                {chipKind === "theory" ? "Теория" : "Практика"}
                                            </span>
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>

            {/* Floating action bar */}
            {fabLesson && (
                <div className="path-fab" role="complementary" aria-label="Быстрый старт">
                    <div className="path-fab-text">
                        <span className="path-fab-eyebrow">Начать следующий урок</span>
                        <span className="path-fab-lesson">{fabLesson.title}</span>
                    </div>
                    <Link href={`/session/${fabLesson.lessonId}`}>
                        <button className="path-fab-btn">Начать →</button>
                    </Link>
                </div>
            )}
        </div>
    );
}
