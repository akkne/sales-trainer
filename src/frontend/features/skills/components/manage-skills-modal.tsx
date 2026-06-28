"use client";

import { useEffect } from "react";
import { Icon } from "@/shared/components/icon";
import type { SkillTreeNode } from "@/features/skills/hooks/use-skill-tree";

const ALWAYS_ENROLLED_SLUG = "sales-basics";

interface ManageSkillsModalProps {
    skills: SkillTreeNode[];
    onToggle: (slug: string) => void;
    isSaving: boolean;
    isError: boolean;
    onClose: () => void;
}

function statusLabel(skill: SkillTreeNode): { text: string; cls: string } {
    if (skill.slug === ALWAYS_ENROLLED_SLUG) return { text: "Core", cls: "msm-badge core" };
    if (skill.status === "completed") return { text: "Completed", cls: "msm-badge completed" };
    if (skill.status === "in_progress") return { text: "In progress", cls: "msm-badge inprogress" };
    if (skill.status === "locked") return { text: "Locked", cls: "msm-badge locked" };
    return { text: "Available", cls: "msm-badge available" };
}

export function ManageSkillsModal({
    skills,
    onToggle,
    isSaving,
    isError,
    onClose,
}: ManageSkillsModalProps) {
    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") onClose();
        };
        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [onClose]);

    const enrolledCount = skills.filter((s) => s.status !== "locked").length;
    const totalCount = skills.length;
    const completedCount = skills.filter((s) => s.status === "completed").length;

    // Sort: enrolled (non-locked) first, then by sortOrder
    const sorted = [...skills].sort((a, b) => {
        const aEnrolled = a.status !== "locked" ? 0 : 1;
        const bEnrolled = b.status !== "locked" ? 0 : 1;
        if (aEnrolled !== bEnrolled) return aEnrolled - bEnrolled;
        return a.sortOrder - b.sortOrder;
    });

    return (
        <div
            className="modal-overlay"
            onClick={onClose}
            role="dialog"
            aria-modal="true"
            aria-label="Manage skills"
        >
            <div
                className="modal fade-up"
                style={{ maxWidth: 560 }}
                onClick={(e) => e.stopPropagation()}
            >
                <div className="modal-head">
                    <div>
                        <h3 className="h3" style={{ margin: 0 }}>Manage skills</h3>
                        <p className="msm-subline">
                            {enrolledCount} of {totalCount} enrolled · {completedCount} completed
                        </p>
                    </div>
                    <button className="icon-btn" onClick={onClose} aria-label="Close">
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="modal-body" style={{ maxHeight: "60vh" }}>
                    {sorted.map((skill) => {
                        const isAlwaysOn = skill.slug === ALWAYS_ENROLLED_SLUG;
                        const isEnrolled = skill.status !== "locked";
                        const isLocked = skill.status === "locked";
                        const toggleDisabled = isAlwaysOn || isLocked || isSaving;
                        const pct =
                            skill.totalLessonCount > 0
                                ? Math.round(
                                      (skill.completedLessonCount / skill.totalLessonCount) * 100
                                  )
                                : 0;
                        const isDone = pct === 100;
                        const badge = statusLabel(skill);

                        return (
                            <div key={skill.skillId} className="msm-skill-row">
                                <div className="msm-skill-row-top">
                                    <span className="pv2-skill-name">{skill.title}</span>
                                    <span className={badge.cls}>{badge.text}</span>
                                    <button
                                        type="button"
                                        className="pv2-skill-toggle msm-toggle-btn"
                                        onClick={() => !toggleDisabled && onToggle(skill.slug)}
                                        disabled={toggleDisabled}
                                        aria-label={
                                            isAlwaysOn
                                                ? `${skill.title} — core skill, always on`
                                                : isEnrolled
                                                ? `Unenroll from ${skill.title}`
                                                : `Enroll in ${skill.title}`
                                        }
                                    >
                                        <div
                                            className={`pv2-switch ${
                                                isAlwaysOn
                                                    ? "always"
                                                    : isEnrolled
                                                    ? "on"
                                                    : "off"
                                            }`}
                                        />
                                    </button>
                                </div>
                                <div className="pv2-skill-bar">
                                    <div
                                        className={`pv2-skill-fill${isDone ? " done" : ""}`}
                                        style={{ width: `${pct}%` }}
                                    />
                                </div>
                                <div style={{ display: "flex", justifyContent: "space-between" }}>
                                    <span style={{ fontSize: 11.5, color: "var(--ink-4)" }}>
                                        {isAlwaysOn
                                            ? "Core — always on"
                                            : `${skill.completedLessonCount}/${skill.totalLessonCount} lessons`}
                                    </span>
                                    <span className="pv2-skill-pct">{pct}%</span>
                                </div>
                            </div>
                        );
                    })}

                    {isError && (
                        <p style={{ fontSize: 12, color: "var(--heart)", marginTop: 12, textAlign: "center" }}>
                            Couldn&apos;t save. Please try again.
                        </p>
                    )}
                </div>
            </div>
        </div>
    );
}
