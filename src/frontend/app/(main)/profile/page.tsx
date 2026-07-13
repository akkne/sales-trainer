"use client";

import { useState } from "react";
import Link from "next/link";
import { useProfile } from "@/features/profile/hooks/use-profile";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useSkills, useUpdateEnrolledSkills } from "@/features/skills/hooks/use-skill-tree";
import { useAvatarUpload } from "@/features/profile/hooks/use-avatar-upload";
import { useUpdateProfile } from "@/features/profile/hooks/use-update-profile";
import { resolveAvatarUrl } from "@/shared/utils/resolve-avatar-url";
import { useVoiceUsage } from "@/features/voice/hooks/use-voice-usage";
import { ManageSkillsModal } from "@/features/skills/components/manage-skills-modal";
import { EditProfileModal } from "@/features/profile/components/edit-profile-modal";

const ALWAYS_ENROLLED_SLUG = "sales-basics";

const PERSONA_LABELS: Record<string, string> = {
    sdr: "SDR",
    account_executive: "Account Executive",
    account_manager: "Account Manager",
    founder: "Основатель",
    other: "Другое",
};

/** Derive user initials from display name (up to 2 chars). */
function initials(name: string): string {
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export default function ProfilePage() {
    const { data: profileStats, isLoading: profileLoading } = useProfile();
    const { data: allSkills, isLoading: skillsLoading } = useSkills();
    const updateEnrolledMutation = useUpdateEnrolledSkills();
    const { data: voiceUsage } = useVoiceUsage();
    const { authenticatedUser } = useAuthStore();
    const { version, uploading, uploadError, fileInputRef, openFilePicker, handleFileChange } =
        useAvatarUpload();
    const updateProfileMutation = useUpdateProfile();
    const [manageOpen, setManageOpen] = useState(false);
    const [editOpen, setEditOpen] = useState(false);

    if (profileLoading) {
        return (
            <div className="pv2-scroll">
                <div className="pv2-cover" />
                <div className="pv2-inner">
                    <div style={{ padding: "0 28px" }}>
                        <div
                            className="pv2-skeleton"
                            style={{ width: 94, height: 94, borderRadius: 22, marginTop: -42 }}
                        />
                        <div className="pv2-skeleton" style={{ width: 180, height: 22, marginTop: 14, borderRadius: 8 }} />
                        <div className="pv2-skeleton" style={{ width: 120, height: 14, marginTop: 8, borderRadius: 6 }} />
                    </div>
                </div>
            </div>
        );
    }

    if (!profileStats) return null;

    const enrolledSlugs = new Set(
        (allSkills ?? [])
            .filter((s) => s.status !== "locked")
            .map((s) => s.slug)
    );

    function toggleEnrollment(slug: string) {
        if (slug === ALWAYS_ENROLLED_SLUG) return;
        const next = new Set(enrolledSlugs);
        if (next.has(slug)) next.delete(slug);
        else next.add(slug);
        updateEnrolledMutation.mutate(Array.from(next));
    }

    // Skills shown in the card: enrolled (non-locked), sorted by lastActivityAt DESC
    // (nulls last), then fill remaining slots by sortOrder. Max 6.
    const enrolledSkills = (allSkills ?? []).filter((s) => s.status !== "locked");
    const withActivity = enrolledSkills
        .filter((s) => s.lastActivityAt != null)
        .sort((a, b) => {
            // both non-null here
            return new Date(b.lastActivityAt!).getTime() - new Date(a.lastActivityAt!).getTime();
        });
    const withActivitySlugs = new Set(withActivity.map((s) => s.slug));
    const withoutActivity = enrolledSkills
        .filter((s) => !withActivitySlugs.has(s.slug))
        .sort((a, b) => a.sortOrder - b.sortOrder);
    const displaySkills = [...withActivity, ...withoutActivity].slice(0, 6);

    const personaLabel = profileStats.persona
        ? PERSONA_LABELS[profileStats.persona] ?? profileStats.persona
        : null;

    // Compute total lessons done from enrolled skills
    const totalLessonsDone = (allSkills ?? []).reduce(
        (sum, s) => sum + (s.completedLessonCount ?? 0),
        0
    );

    // Avatar URL: resolve against the API origin (identity service lives on a
    // different host in prod) and cache-bust after an upload.
    const avatarSrc = profileStats.avatarUrl
        ? `${resolveAvatarUrl(profileStats.avatarUrl)}${version > 0 ? `?v=${version}` : ""}`
        : profileStats.avatarUrl;

    const hasVoiceQuota =
        voiceUsage &&
        (voiceUsage.dailyLimitSeconds > 0 || voiceUsage.monthlyLimitSeconds > 0);

    return (
        <>
        <div className="pv2-scroll">
            {/* Cover band */}
            <div className="pv2-cover" />

            <div className="pv2-inner">
                {/* Identity row */}
                <div className="pv2-identity">
                    {/* Avatar with ring */}
                    <div style={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
                        <div className="pv2-avatar-ring">
                            <div className="pv2-avatar-inner">
                                {avatarSrc ? (
                                    // eslint-disable-next-line @next/next/no-img-element
                                    <img src={avatarSrc} alt={profileStats.displayName} />
                                ) : (
                                    initials(profileStats.displayName)
                                )}
                                {uploading && (
                                    <div className="pv2-avatar-overlay">
                                        <div className="pv2-spinner" />
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Name / persona / email */}
                    <div className="pv2-identity-meta">
                        <div className="pv2-identity-name">{profileStats.displayName}</div>
                        <div className="pv2-identity-sub">
                            {[personaLabel, profileStats.email].filter(Boolean).join(" · ")}
                        </div>
                        {uploadError && (
                            <p style={{ fontSize: 11, color: "var(--heart)", marginTop: 4 }}>
                                {uploadError}
                            </p>
                        )}
                    </div>

                    {/* Edit profile — opens a modal to change name, position and photo */}
                    <div className="pv2-identity-actions">
                        <button
                            type="button"
                            onClick={() => setEditOpen(true)}
                            className="pv2-edit-profile-button"
                        >
                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                            </svg>
                            Редактировать профиль
                        </button>
                    </div>
                </div>

                {/* 4 Stat tiles */}
                <div className="pv2-stats">
                    {/* Accuracy */}
                    <div className="pv2-stat">
                        <div className="pv2-stat-ic green">
                            {/* target / crosshair icon */}
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                                <circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/>
                            </svg>
                        </div>
                        <div>
                            <div className="pv2-stat-label">Точность</div>
                            <div className="pv2-stat-value">
                                {profileStats.averageExerciseScore}
                                <small>%</small>
                            </div>
                        </div>
                    </div>

                    {/* Best streak */}
                    <div className="pv2-stat">
                        <div className="pv2-stat-ic amber">
                            {/* trophy */}
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                                <path d="M6 9H3a1 1 0 0 0-1 1v1a5 5 0 0 0 5 5"/><path d="M18 9h3a1 1 0 0 1 1 1v1a5 5 0 0 1-5 5"/><path d="M7 21h10"/><path d="M12 17v4"/><path d="M7 4h10l-1 9a5 5 0 0 1-8 0L7 4z"/>
                            </svg>
                        </div>
                        <div>
                            <div className="pv2-stat-label">Лучшая серия</div>
                            <div className="pv2-stat-value">
                                {profileStats.longestStreakDayCount}
                                <small>д</small>
                            </div>
                        </div>
                    </div>

                    {/* Skills completed */}
                    <div className="pv2-stat">
                        <div className="pv2-stat-ic violet">
                            {/* check circle */}
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                                <circle cx="12" cy="12" r="10"/><path d="m9 12 2 2 4-4"/>
                            </svg>
                        </div>
                        <div>
                            <div className="pv2-stat-label">Навыки</div>
                            <div className="pv2-stat-value">{profileStats.completedSkillCount}</div>
                        </div>
                    </div>

                    {/* Lessons completed */}
                    <div className="pv2-stat">
                        <div className="pv2-stat-ic blue">
                            {/* book */}
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                                <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
                            </svg>
                        </div>
                        <div>
                            <div className="pv2-stat-label">Уроки</div>
                            <div className="pv2-stat-value">{totalLessonsDone}</div>
                        </div>
                    </div>
                </div>

                {/* Two-column body */}
                <div className="pv2-body">
                    {/* Left: Enrolled skills — recent activity, no toggles */}
                    <div className="pv2-card">
                        <div className="pv2-card-head">
                            <span className="pv2-card-title">Изучаемые навыки</span>
                            <button
                                type="button"
                                className="pv2-manage-link"
                                onClick={() => setManageOpen(true)}
                            >
                                Управлять
                                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                                    <path d="m9 18 6-6-6-6"/>
                                </svg>
                            </button>
                        </div>

                        {skillsLoading ? (
                            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                                {[1, 2, 3].map((i) => (
                                    <div key={i} className="pv2-skeleton" style={{ height: 52, borderRadius: 8 }} />
                                ))}
                            </div>
                        ) : !allSkills || allSkills.length === 0 ? (
                            <p style={{ fontSize: 13, color: "var(--ink-3)", margin: 0 }}>
                                Пока нет навыков. Обратись к администратору.
                            </p>
                        ) : displaySkills.length === 0 ? (
                            <p style={{ fontSize: 13, color: "var(--ink-3)", margin: 0 }}>
                                Пока нет изучаемых навыков.{" "}
                                <button
                                    type="button"
                                    className="pv2-manage-link"
                                    style={{ fontSize: 13 }}
                                    onClick={() => setManageOpen(true)}
                                >
                                    Управлять →
                                </button>
                            </p>
                        ) : (
                            <div>
                                {displaySkills.map((skill) => {
                                    const pct =
                                        skill.totalLessonCount > 0
                                            ? Math.round(
                                                  (skill.completedLessonCount /
                                                      skill.totalLessonCount) *
                                                      100
                                              )
                                            : 0;
                                    const isDone = pct === 100;

                                    return (
                                        <Link
                                            key={skill.skillId}
                                            href={`/skill/${skill.slug}`}
                                            className="pv2-skill-row pv2-skill-row-link"
                                        >
                                            <div className="pv2-skill-row-head">
                                                <span className="pv2-skill-name">{skill.title}</span>
                                                <span className="pv2-skill-pct">{pct}%</span>
                                            </div>
                                            <div className="pv2-skill-bar">
                                                <div
                                                    className={`pv2-skill-fill${isDone ? " done" : ""}`}
                                                    style={{ width: `${pct}%` }}
                                                />
                                            </div>
                                            <span style={{ fontSize: 11.5, color: "var(--ink-4)" }}>
                                                {skill.completedLessonCount}/{skill.totalLessonCount} уроков
                                            </span>
                                        </Link>
                                    );
                                })}
                            </div>
                        )}
                    </div>

                    {/* Right: Voice minutes */}
                    <div className="pv2-card">
                        <div className="pv2-card-head">
                            <span className="pv2-card-title">Минуты озвучки</span>
                        </div>

                        {!hasVoiceQuota ? (
                            <p style={{ fontSize: 13, color: "var(--ink-3)", margin: 0 }}>
                                Голосовые звонки без ограничений.
                            </p>
                        ) : (
                            <div className="pv2-quota-row">
                                {voiceUsage.dailyLimitSeconds > 0 && (
                                    <VoiceBar
                                        label="Сегодня"
                                        usedSeconds={voiceUsage.dailyUsedSeconds}
                                        limitSeconds={voiceUsage.dailyLimitSeconds}
                                        tone="violet"
                                        resetNote="Обновляется ежедневно"
                                    />
                                )}
                                {voiceUsage.monthlyLimitSeconds > 0 && (
                                    <VoiceBar
                                        label="В этом месяце"
                                        usedSeconds={voiceUsage.monthlyUsedSeconds}
                                        limitSeconds={voiceUsage.monthlyLimitSeconds}
                                        tone="green"
                                        resetNote="Обновляется ежемесячно"
                                    />
                                )}
                            </div>
                        )}
                    </div>
                </div>
            </div>
        </div>

        {manageOpen && allSkills && (
            <ManageSkillsModal
                skills={allSkills}
                onToggle={toggleEnrollment}
                isSaving={updateEnrolledMutation.isPending}
                isError={updateEnrolledMutation.isError}
                onClose={() => setManageOpen(false)}
            />
        )}

        {editOpen && (
            <EditProfileModal
                initialName={profileStats.displayName}
                initialPersona={profileStats.persona}
                avatarSrc={avatarSrc}
                avatarInitials={initials(profileStats.displayName)}
                uploading={uploading}
                uploadError={uploadError}
                fileInputRef={fileInputRef}
                onPickPhoto={openFilePicker}
                onFileChange={handleFileChange}
                isSaving={updateProfileMutation.isPending}
                isError={updateProfileMutation.isError}
                onSave={(displayName, persona) =>
                    updateProfileMutation.mutate(
                        { displayName, persona },
                        { onSuccess: () => setEditOpen(false) }
                    )
                }
                onClose={() => setEditOpen(false)}
            />
        )}
        </>
    );
}

function VoiceBar({
    label,
    usedSeconds,
    limitSeconds,
    tone,
    resetNote,
}: {
    label: string;
    usedSeconds: number;
    limitSeconds: number;
    tone: "violet" | "green";
    resetNote: string;
}) {
    const usedMin = Math.round(usedSeconds / 60);
    const limitMin = Math.round(limitSeconds / 60);
    const pct = Math.min(100, Math.round((usedSeconds / limitSeconds) * 100));
    const isExceeded = usedSeconds >= limitSeconds;
    const fillClass = isExceeded ? "red" : tone;

    return (
        <div className="pv2-quota-item">
            <div className="pv2-quota-head">
                <span>{label}</span>
                <span className={`pv2-quota-num${isExceeded ? " exceeded" : ""}`}>
                    {usedMin} / {limitMin} мин
                </span>
            </div>
            <div className="pv2-quota-bar">
                <div className={`pv2-quota-fill ${fillClass}`} style={{ width: `${pct}%` }} />
            </div>
            {isExceeded ? (
                <span className="pv2-quota-note warn">
                    Лимит исчерпан — звонки возобновятся{" "}
                    {label === "Сегодня" ? "завтра" : "в следующем месяце"}
                </span>
            ) : (
                <span className="pv2-quota-note">{resetNote}</span>
            )}
        </div>
    );
}
