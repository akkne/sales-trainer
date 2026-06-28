"use client";

import { useEffect, useState, useDeferredValue, useCallback } from "react";
import Link from "next/link";
import ReactMarkdown from "react-markdown";
import { Icon } from "@/shared/components/icon";
import { Skeleton } from "@/shared/components/skeleton";
import {
    useTechniques,
    useTechniquesMeta,
    useTechnique,
    useMarkTechniqueSeen,
    type TechniqueCard,
    type TechniqueDetail,
    type TechniqueCoach,
} from "@/features/skills/hooks/use-techniques";

/**
 * Normalise escape sequences stored in technique bodies so ReactMarkdown
 * renders them correctly (see original implementation for rationale).
 */
function normalizeMarkdown(raw: string): string {
    const INDENT = "    ";
    return raw
        .replace(/\\t/g, INDENT)
        .replace(/\\r\\n|\\n|\\r/g, "\n")
        .replace(/\t/g, INDENT)
        .replace(/\r\n|\r/g, "\n")
        .replace(/\n/g, "  \n");
}

/* ── Shared helpers ── */

function difficultyBadgeClass(difficulty: number): string {
    if (difficulty <= 1) return "badge badge-easy";
    if (difficulty === 2) return "badge badge-medium";
    return "badge badge-hard";
}

function difficultyLabel(name: string): string {
    return name || "—";
}

function MasteryRing({
    masteryLevel,
    masteryPercent,
}: {
    masteryLevel: number;
    masteryPercent: number;
}) {
    const radius = 24;
    const circumference = 2 * Math.PI * radius;
    const fraction = Math.max(0, Math.min(1, masteryPercent / 100));
    const display = masteryLevel > 0 ? `L${masteryLevel}` : "—";
    return (
        <div className="mastery-ring" aria-label={`Mastery: level ${masteryLevel}, ${masteryPercent}%`}>
            <svg width="56" height="56" viewBox="0 0 56 56" aria-hidden="true">
                <circle cx="28" cy="28" r={radius} fill="none" stroke="var(--line)" strokeWidth="4" />
                <circle
                    cx="28"
                    cy="28"
                    r={radius}
                    fill="none"
                    stroke="var(--primary)"
                    strokeWidth="4"
                    strokeLinecap="round"
                    strokeDasharray={circumference}
                    strokeDashoffset={circumference * (1 - fraction)}
                    transform="rotate(-90 28 28)"
                    style={{ transition: "stroke-dashoffset 0.4s ease" }}
                />
            </svg>
            <span>{display}</span>
        </div>
    );
}

function MasteryRingSm({
    masteryLevel,
    masteryPercent,
}: {
    masteryLevel: number;
    masteryPercent: number;
}) {
    const radius = 12;
    const circumference = 2 * Math.PI * radius;
    const fraction = Math.max(0, Math.min(1, masteryPercent / 100));
    const display = masteryLevel > 0 ? `L${masteryLevel}` : "—";
    return (
        <div className="mastery-ring-sm" aria-label={`Level ${masteryLevel}`}>
            <svg width="32" height="32" viewBox="0 0 32 32" aria-hidden="true">
                <circle cx="16" cy="16" r={radius} fill="none" stroke="var(--line)" strokeWidth="3" />
                <circle
                    cx="16"
                    cy="16"
                    r={radius}
                    fill="none"
                    stroke="var(--primary)"
                    strokeWidth="3"
                    strokeLinecap="round"
                    strokeDasharray={circumference}
                    strokeDashoffset={circumference * (1 - fraction)}
                    transform="rotate(-90 16 16)"
                />
            </svg>
            <span>{display}</span>
        </div>
    );
}

function CoachInitials(name: string): string {
    return name
        .split(" ")
        .slice(0, 2)
        .map((w) => w[0]?.toUpperCase() ?? "")
        .join("");
}

/* ── Main page ── */

export default function GuidebookPage() {
    const [selectedSkill, setSelectedSkill] = useState<string | null>(null);
    const [searchInput, setSearchInput] = useState("");
    const [activeTags, setActiveTags] = useState<string[]>([]);
    const [selectedSlug, setSelectedSlug] = useState<string | null>(null);

    const deferredSearch = useDeferredValue(searchInput);

    const { data: meta } = useTechniquesMeta();
    const { data: cards = [], isLoading } = useTechniques({
        skill: selectedSkill ?? undefined,
        search: deferredSearch || undefined,
        tags: activeTags,
    });

    const { data: detail } = useTechnique(selectedSlug);
    const markSeen = useMarkTechniqueSeen();

    useEffect(() => {
        if (!selectedSlug) return;
        const card = cards.find((c) => c.slug === selectedSlug);
        if (card?.isNew) {
            markSeen.mutate(selectedSlug);
        }
    }, [selectedSlug, cards, markSeen]);

    const skills = meta?.skills ?? [];

    const toggleTag = useCallback((tag: string) => {
        setActiveTags((prev) =>
            prev.includes(tag) ? prev.filter((t) => t !== tag) : [...prev, tag]
        );
    }, []);

    const selectCard = useCallback(
        (slug: string) => setSelectedSlug((prev) => (prev === slug ? null : slug)),
        []
    );

    const closePanel = useCallback(() => setSelectedSlug(null), []);

    const selectedCard = cards.find((c) => c.slug === selectedSlug) ?? null;

    return (
        <div className="page" style={{ display: "flex", flexDirection: "column", overflow: "hidden" }}>
            {/* Header — always visible, outside the scrolling area */}
            <div className="ref-header">
                <h1 className="ref-title">Technique guidebook</h1>
                <p className="ref-subtitle">
                    {meta?.totalCount ?? 0} techniques · mastered {meta?.userCounts.mastered ?? 0}
                </p>

                <div className="ref-tools">
                    {/* Search */}
                    <div className="ref-search-wrap">
                        <span className="ref-search-ic" aria-hidden="true">
                            <Icon name="search" size="sm" />
                        </span>
                        <input
                            className="ref-search"
                            placeholder="Technique, tag, skill…"
                            value={searchInput}
                            onChange={(e) => setSearchInput(e.target.value)}
                            aria-label="Search techniques"
                        />
                    </div>

                    {/* Skill filter chips */}
                    <div className="ref-tags" role="group" aria-label="Filter by skill">
                        <button
                            className={"ref-tag" + (selectedSkill === null ? " active" : "")}
                            onClick={() => setSelectedSkill(null)}
                        >
                            All
                        </button>
                        {skills.map((skill) => (
                            <button
                                key={skill.iconicName}
                                className={"ref-tag" + (selectedSkill === skill.iconicName ? " active" : "")}
                                onClick={() => setSelectedSkill(skill.iconicName)}
                            >
                                {skill.title}
                                <span style={{ opacity: 0.5, marginLeft: 4 }}>{skill.techniqueCount}</span>
                            </button>
                        ))}
                    </div>

                    {/* Active tag filters */}
                    {activeTags.length > 0 && (
                        <div className="ref-active-tags">
                            {activeTags.map((tag) => (
                                <button
                                    key={tag}
                                    className="ref-active-tag"
                                    onClick={() => toggleTag(tag)}
                                    aria-label={`Remove filter: ${tag}`}
                                >
                                    #{tag}
                                    <Icon name="close" size="xs" />
                                </button>
                            ))}
                            <button className="ref-clear-tags" onClick={() => setActiveTags([])}>
                                Clear
                            </button>
                        </div>
                    )}
                </div>
            </div>

            {/* Two-panel layout */}
            <div className="ref-layout">
                {/* Left: cards grid */}
                <div className="ref-main">
                    <div className="ref-main-scroll">
                        {isLoading ? (
                            <div className="ref-grid">
                                {[1, 2, 3, 4, 5, 6].map((i) => (
                                    <Skeleton key={i} height={160} rounded={15} />
                                ))}
                            </div>
                        ) : cards.length === 0 ? (
                            <div className="ref-empty">
                                <Icon name="search" size="lg" color="var(--ink-4)" />
                                <p>Nothing found</p>
                                <span>Try a different query or skill</span>
                            </div>
                        ) : (
                            <div className="ref-grid">
                                {cards.map((card) => (
                                    <TechniqueCardItem
                                        key={card.id}
                                        card={card}
                                        isSelected={selectedSlug === card.slug}
                                        activeTags={activeTags}
                                        onSelect={() => selectCard(card.slug)}
                                        onTagClick={toggleTag}
                                    />
                                ))}
                            </div>
                        )}
                    </div>
                </div>

                {/* Right: detail panel — rendered only when a card is selected */}
                {selectedSlug && (
                    <DetailPanel
                        card={selectedCard}
                        detail={detail ?? null}
                        onClose={closePanel}
                    />
                )}
            </div>
        </div>
    );
}

/* ── Technique card ── */

function TechniqueCardItem({
    card,
    isSelected,
    activeTags,
    onSelect,
    onTagClick,
}: {
    card: TechniqueCard;
    isSelected: boolean;
    activeTags: string[];
    onSelect: () => void;
    onTagClick: (tag: string) => void;
}) {
    return (
        <article
            className={"ref-card" + (isSelected ? " selected" : "")}
            onClick={onSelect}
            onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && onSelect()}
            tabIndex={0}
            role="button"
            aria-pressed={isSelected}
            aria-label={card.name}
        >
            <div className="ref-card-inner">
                {/* Badges row */}
                <div className="ref-card-badges">
                    <span className={difficultyBadgeClass(card.difficulty)}>
                        {difficultyLabel(card.difficultyName)}
                    </span>
                    {card.isNew && <span className="badge badge-new">NEW</span>}
                    {/* Small mastery ring on card */}
                    <MasteryRingSm
                        masteryLevel={card.masteryLevel}
                        masteryPercent={card.masteryPercent}
                    />
                </div>

                {/* Name */}
                <div className="ref-card-name">{card.name}</div>

                {/* Description */}
                <div className="ref-card-desc">{card.summary}</div>

                {/* Hashtag chips */}
                {card.tags.length > 0 && (
                    <div className="ref-card-hashtags">
                        {card.tags.slice(0, 3).map((tag) => (
                            <span
                                key={tag}
                                className={"chip" + (activeTags.includes(tag) ? " primary" : "")}
                                style={{ cursor: "pointer", fontSize: 11 }}
                                onClick={(e) => {
                                    e.stopPropagation();
                                    onTagClick(tag);
                                }}
                                role="button"
                                tabIndex={0}
                                onKeyDown={(e) => {
                                    if (e.key === "Enter" || e.key === " ") {
                                        e.stopPropagation();
                                        onTagClick(tag);
                                    }
                                }}
                                aria-label={`Filter by tag ${tag}`}
                            >
                                #{tag}
                            </span>
                        ))}
                    </div>
                )}
            </div>

            {/* Footer */}
            <div className="ref-card-footer">
                <span className="ref-card-skill">
                    {card.primarySkillTitle ?? "Technique"}
                </span>
                <span className="ref-card-read">
                    Read →
                </span>
            </div>
        </article>
    );
}

/* ── Detail panel ── */

function DetailPanel({
    card,
    detail,
    onClose,
}: {
    card: TechniqueCard | null;
    detail: TechniqueDetail | null;
    onClose: () => void;
}) {
    const data = detail?.card ?? card;

    return (
        <aside className="ref-panel" aria-label="Technique details">
            {/* Panel header */}
            <div className="ref-panel-head">
                <span className="ref-panel-eyebrow">Technique</span>
                <button
                    className="ref-panel-close"
                    onClick={onClose}
                    aria-label="Close panel"
                >
                    <Icon name="close" size="sm" />
                </button>
            </div>

            <div className="ref-panel-scroll">
                {!data ? (
                    /* Loading skeleton */
                    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                        <Skeleton height={24} rounded={8} />
                        <Skeleton height={40} rounded={8} />
                        <Skeleton height={80} rounded={8} />
                        <Skeleton height={120} rounded={8} />
                    </div>
                ) : (
                    <>
                        {/* Pills row: difficulty + NEW + skill */}
                        <div className="ref-panel-pills">
                            <span className={difficultyBadgeClass(data.difficulty)}>
                                {difficultyLabel(data.difficultyName)}
                            </span>
                            {data.isNew && <span className="badge badge-new">NEW</span>}
                            {data.primarySkillTitle && (
                                <span
                                    className="chip"
                                    style={{
                                        background: "var(--primary-soft)",
                                        color: "var(--primary-strong)",
                                        border: "1px solid var(--primary-tint-border-2)",
                                    }}
                                >
                                    {data.primarySkillTitle}
                                </span>
                            )}
                        </div>

                        {/* Technique name */}
                        <div className="ref-panel-name">{data.name}</div>

                        {/* Mastery ring block */}
                        <div className="ref-panel-mastery">
                            <MasteryRing
                                masteryLevel={data.masteryLevel}
                                masteryPercent={data.masteryPercent}
                            />
                            <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                                <span className="ref-panel-mastery-label">Mastery level</span>
                                <span className="ref-panel-mastery-level">
                                    {data.masteryLevel > 0 ? `Level ${data.masteryLevel}` : "Not started"}
                                </span>
                                <span className="ref-panel-mastery-pct">{data.masteryPercent}%</span>
                            </div>
                        </div>

                        {/* Hashtags */}
                        {data.tags.length > 0 && (
                            <div className="ref-panel-hashtags">
                                {data.tags.map((tag) => (
                                    <span key={tag} className="chip" style={{ fontSize: 11 }}>
                                        #{tag}
                                    </span>
                                ))}
                            </div>
                        )}

                        {/* How it works */}
                        {detail?.body && (
                            <section>
                                <p className="ref-section-label">How it works</p>
                                <div className="ref-body-text">
                                    <ReactMarkdown>{normalizeMarkdown(detail.body)}</ReactMarkdown>
                                </div>
                            </section>
                        )}

                        {/* Example dialogue */}
                        {detail && detail.dialogTurns.length > 0 && (
                            <section>
                                <p className="ref-section-label">Example dialogue</p>
                                <div className="ref-dlg-box">
                                    {detail.dialogTurns.map((turn) => {
                                        const isOut = turn.side === "me";
                                        const speaker = isOut ? "You" : "Prospect";
                                        const anno = turn.annotations.map((a) => a.label).join(" · ");
                                        return (
                                            <div
                                                key={turn.orderIndex}
                                                className={"ref-bubble-wrap " + (isOut ? "out" : "in")}
                                            >
                                                <div className="ref-bubble-speaker">{speaker}</div>
                                                <div className={"ref-bubble " + (isOut ? "out" : "in")}>
                                                    {turn.text}
                                                </div>
                                                {anno && (
                                                    <div className="ref-anno">
                                                        <span style={{ opacity: 0.6 }}>↳</span>
                                                        {anno}
                                                    </div>
                                                )}
                                            </div>
                                        );
                                    })}
                                </div>
                            </section>
                        )}

                        {/* Case study */}
                        {detail?.case && (
                            <section>
                                <p className="ref-section-label">Case study</p>
                                <div className="ref-case-text">
                                    <strong style={{ color: "var(--ink-heading)" }}>
                                        {detail.case.title}
                                    </strong>
                                    {" · "}
                                    {detail.case.body}
                                </div>
                                {detail.case.metrics && (
                                    <CaseMetrics metrics={detail.case.metrics} />
                                )}
                            </section>
                        )}

                        {/* Coach */}
                        {detail?.coach && (
                            <>
                                <div className="ref-divider" />
                                <CoachBlock
                                    coach={detail.coach}
                                    practiceSlug={data.primarySkillIconicName}
                                />
                            </>
                        )}

                        {/* Fallback practice link when no coach block */}
                        {!detail?.coach && data.primarySkillIconicName && (
                            <Link href={`/skill/${data.primarySkillIconicName}`} style={{ display: "block" }}>
                                <button className="ref-practice-btn">
                                    Practise technique →
                                </button>
                            </Link>
                        )}
                    </>
                )}
            </div>
        </aside>
    );
}

/* ── Case metrics tiles ── */

function CaseMetrics({ metrics }: { metrics: Record<string, unknown> }) {
    const entries = Object.entries(metrics).slice(0, 4);
    if (entries.length === 0) return null;
    return (
        <div className="ref-metrics">
            {entries.map(([key, value]) => (
                <div key={key} className="ref-metric-tile">
                    <span className="ref-metric-value">{String(value)}</span>
                    <span className="ref-metric-label">{key}</span>
                </div>
            ))}
        </div>
    );
}

/* ── Coach block ── */

function CoachBlock({
    coach,
    practiceSlug,
}: {
    coach: TechniqueCoach;
    practiceSlug: string | null;
}) {
    return (
        <div className="ref-coach">
            {/* Identity */}
            <div className="ref-coach-identity">
                <div className="ref-coach-avatar" aria-hidden="true">
                    {CoachInitials(coach.name)}
                </div>
                <div>
                    <div className="ref-coach-name">{coach.name}</div>
                    <div className="ref-coach-role">{coach.role}</div>
                </div>
            </div>

            {/* Quote */}
            <blockquote className="ref-coach-quote" style={{ margin: 0 }}>
                &laquo;{coach.quote}&raquo;
            </blockquote>

            {/* Challenges */}
            {coach.challenges.length > 0 && (
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                    {coach.challenges.map((challenge, idx) => (
                        <div key={idx} className="ref-coach-challenge">
                            <span className="ref-coach-challenge-icon">→</span>
                            <span>{challenge.label}</span>
                        </div>
                    ))}
                </div>
            )}

            {/* Practice button — deep links to Practice (dialog) screen */}
            {practiceSlug ? (
                <Link href={`/skill/${practiceSlug}`} style={{ display: "block" }}>
                    <button className="ref-practice-btn">
                        Practise technique
                    </button>
                </Link>
            ) : (
                <Link href="/dialog" style={{ display: "block" }}>
                    <button className="ref-practice-btn">
                        Practise technique
                    </button>
                </Link>
            )}
        </div>
    );
}
