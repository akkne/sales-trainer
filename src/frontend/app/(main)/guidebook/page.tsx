"use client";

import { useEffect, useState, useDeferredValue } from "react";
import Link from "next/link";
import ReactMarkdown from "react-markdown";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { StatTile } from "@/shared/components/stat-tile";
import { Skeleton } from "@/shared/components/skeleton";
import {
    useTechniques,
    useTechniquesMeta,
    useTechnique,
    useMarkTechniqueSeen,
    type TechniqueCard,
    type TechniqueDetail,
} from "@/features/skills/hooks/use-techniques";

/**
 * Technique bodies are stored with escape sequences (`\n`, `\t`) instead of real
 * whitespace, and CommonMark collapses single newlines into spaces. Convert the
 * escapes to real whitespace and turn every newline into a hard break so the
 * formatting renders as authored.
 */
function normalizeMarkdown(raw: string): string {
    const INDENT = "    ";
    return raw
        .replace(/\\t/g, INDENT) // literal "\t"
        .replace(/\\r\\n|\\n|\\r/g, "\n") // literal "\n" / "\r\n"
        .replace(/\t/g, INDENT) // real tab
        .replace(/\r\n|\r/g, "\n") // normalise real CRLF
        .replace(/\n/g, "  \n"); // newline -> markdown hard break
}

function MasteryRing({ masteryLevel, masteryPercent }: { masteryLevel: number; masteryPercent: number }) {
    const radius = 24;
    const circumference = 2 * Math.PI * radius;
    const fraction = Math.max(0, Math.min(1, masteryPercent / 100));
    const display = masteryLevel > 0 ? `L${masteryLevel}` : "—";
    return (
        <div className="mastery-ring">
            <svg width="56" height="56" viewBox="0 0 56 56">
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
                />
            </svg>
            <span>{display}</span>
        </div>
    );
}

export default function GuidebookPage() {
    const [selectedSkill, setSelectedSkill] = useState<string | null>(null);
    const [searchInput, setSearchInput] = useState("");
    const [activeTags, setActiveTags] = useState<string[]>([]);
    const [expandedSlug, setExpandedSlug] = useState<string | null>(null);

    const deferredSearch = useDeferredValue(searchInput);

    const { data: meta } = useTechniquesMeta();
    const { data: cards = [], isLoading } = useTechniques({
        skill: selectedSkill ?? undefined,
        search: deferredSearch || undefined,
        tags: activeTags,
    });

    const { data: expandedDetail } = useTechnique(expandedSlug);
    const markSeen = useMarkTechniqueSeen();

    useEffect(() => {
        if (!expandedSlug) return;
        const expandedCard = cards.find((card) => card.slug === expandedSlug);
        if (expandedCard?.isNew) {
            markSeen.mutate(expandedSlug);
        }
    }, [expandedSlug, cards, markSeen]);

    function toggleExpand(slug: string) {
        setExpandedSlug((previous) => (previous === slug ? null : slug));
    }

    const skills = meta?.skills ?? [];
    const totalCount = meta?.totalCount ?? 0;
    const userCounts = meta?.userCounts ?? { mastered: 0, master: 0, unseen: 0 };

    function toggleActiveTag(tag: string) {
        setActiveTags((current) =>
            current.includes(tag) ? current.filter((existing) => existing !== tag) : [...current, tag]
        );
    }

    return (
        <div className="page">
            <div className="container">
                <div className="hero-head">
                    <div className="hh-left">
                        <span className="eyebrow">
                            Справочник
                            <span className="dot">·</span>
                            <span>{totalCount} техник</span>
                        </span>
                        <h1 className="h1 hh-title">Коллекция.</h1>
                        <p className="lead" style={{ textWrap: "pretty" }}>
                            Не каталог — коллекция. Каждая техника живёт с уровнем мастерства, примером диалога и
                            кейсом.
                        </p>
                    </div>
                    <div className="hero-stats">
                        <StatTile
                            label="Освоено"
                            value={`${userCounts.mastered}`}
                            unit={`/${totalCount}`}
                            icon={<Icon name="check" size="sm" />}
                            tone="success"
                        />
                        <StatTile
                            label="Мастер"
                            value={`${userCounts.master}`}
                            icon={<Icon name="trophy" size="sm" />}
                            tone="amber"
                        />
                        <StatTile
                            label="Новых"
                            value={`${userCounts.unseen}`}
                            icon={<Icon name="sparkle" size="sm" />}
                            tone="violet"
                        />
                    </div>
                </div>

                <div className="gb-tools">
                    <div className="field-wrap has-ic" style={{ maxWidth: 360 }}>
                        <Icon name="search" className="lead-ic" />
                        <input
                            className="field"
                            placeholder="Техника, тег, навык…"
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                        />
                    </div>
                    <div className="row gap-2 wrap">
                        <button
                            className={"chip" + (selectedSkill === null ? " solid" : "")}
                            onClick={() => setSelectedSkill(null)}
                            style={{ cursor: "pointer", height: 34 }}
                        >
                            Все
                        </button>
                        {skills.map((skill) => (
                            <button
                                key={skill.iconicName}
                                className={"chip" + (selectedSkill === skill.iconicName ? " solid" : "")}
                                onClick={() => setSelectedSkill(skill.iconicName)}
                                style={{ cursor: "pointer", height: 34 }}
                            >
                                {skill.title}
                                <span style={{ opacity: 0.6 }}>{skill.techniqueCount}</span>
                            </button>
                        ))}
                    </div>
                </div>

                {activeTags.length > 0 && (
                    <div className="row gap-2 wrap" style={{ marginBottom: 18 }}>
                        {activeTags.map((tag) => (
                            <button
                                key={tag}
                                className="chip primary"
                                onClick={() => toggleActiveTag(tag)}
                                style={{ cursor: "pointer", height: 34 }}
                                aria-label={`Убрать фильтр по тегу ${tag}`}
                            >
                                #{tag}
                                <Icon name="close" size="xs" />
                            </button>
                        ))}
                        <button
                            onClick={() => setActiveTags([])}
                            className="eyebrow muted"
                            style={{ background: "none", border: "none", cursor: "pointer" }}
                        >
                            Очистить
                        </button>
                    </div>
                )}

                {isLoading ? (
                    <div className="gb-grid">
                        {[1, 2, 3, 4].map((i) => (
                            <Skeleton key={i} height={150} rounded={20} />
                        ))}
                    </div>
                ) : cards.length === 0 ? (
                    <div className="empty">
                        <span className="ic">
                            <Icon name="search" size="lg" color="var(--ink-3)" />
                        </span>
                        <p className="h4">Ничего не найдено</p>
                        <p className="body muted">Попробуй другой запрос или навык</p>
                    </div>
                ) : (
                    <div className="gb-grid">
                        {cards.map((card) => (
                            <TechniqueCardView
                                key={card.id}
                                card={card}
                                isExpanded={expandedSlug === card.slug}
                                detail={expandedSlug === card.slug ? expandedDetail ?? null : null}
                                onToggle={() => toggleExpand(card.slug)}
                                onTagClick={toggleActiveTag}
                                activeTags={activeTags}
                            />
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}

function TechniqueCardView({
    card,
    isExpanded,
    detail,
    onToggle,
    onTagClick,
    activeTags,
}: {
    card: TechniqueCard;
    isExpanded: boolean;
    detail: TechniqueDetail | null;
    onToggle: () => void;
    onTagClick: (tag: string) => void;
    activeTags: string[];
}) {
    return (
        <div className={"card gb-card" + (isExpanded ? " open" : "")}>
            <button className="gb-top" onClick={onToggle}>
                <MasteryRing masteryLevel={card.masteryLevel} masteryPercent={card.masteryPercent} />

                <div className="grow" style={{ textAlign: "left" }}>
                    <div className="row gap-2 wrap" style={{ marginBottom: 8 }}>
                        {card.primarySkillTitle && <span className="chip">{card.primarySkillTitle}</span>}
                        {card.tags.slice(0, 2).map((tag) => (
                            <span
                                key={tag}
                                className={"chip" + (activeTags.includes(tag) ? " primary" : "")}
                                onClick={(event) => {
                                    event.stopPropagation();
                                    onTagClick(tag);
                                }}
                                style={{
                                    background: activeTags.includes(tag) ? undefined : "transparent",
                                    cursor: "pointer",
                                }}
                            >
                                #{tag}
                            </span>
                        ))}
                        {card.isNew && (
                            <span
                                className="badge"
                                style={{ background: "var(--violet-soft)", color: "var(--violet)" }}
                            >
                                Новое
                            </span>
                        )}
                    </div>
                    <h3 className="h3">{card.name}</h3>
                    <p className="body" style={{ marginTop: 6, textWrap: "pretty" }}>
                        {card.summary}
                    </p>
                </div>

                <div className="gb-right">
                    <span className="stat-label">Уровень</span>
                    <span style={{ color: "var(--primary)", fontWeight: 700, fontSize: 14 }}>
                        {card.difficultyName}
                    </span>
                    <Icon
                        name={isExpanded ? "chevron-up" : "chevron-down"}
                        style={{ color: "var(--ink-4)", marginTop: 6 }}
                    />
                </div>
            </button>

            {isExpanded && (
                <TechniqueBody card={card} detail={detail} />
            )}
        </div>
    );
}

function TechniqueBody({ card, detail }: { card: TechniqueCard; detail: TechniqueDetail | null }) {
    return (
        <div className="gb-body">
            <div className="hr" style={{ margin: "0 0 20px" }} />

            {detail?.body && (
                <div className="body" style={{ color: "var(--ink)" }}>
                    <ReactMarkdown>{normalizeMarkdown(detail.body)}</ReactMarkdown>
                </div>
            )}

            {detail && detail.dialogTurns.length > 0 && (
                <>
                    <span className="eyebrow muted" style={{ display: "block", margin: "20px 0 12px" }}>
                        Пример диалога
                    </span>
                    <div className="dlg-example">
                        {detail.dialogTurns.map((turn) => {
                            const anno = turn.annotations.map((a) => a.label).join(" · ");
                            return (
                                <div
                                    key={turn.orderIndex}
                                    className={"dx " + (turn.side === "me" ? "out" : "in")}
                                >
                                    {turn.text}
                                    {anno && <span className="anno">[{anno}]</span>}
                                </div>
                            );
                        })}
                    </div>
                </>
            )}

            {detail?.case && (
                <div className="case-box">
                    <b>{detail.case.title}</b> · {detail.case.body}
                    {detail.case.metrics && (
                        <div className="case-metrics">
                            {Object.entries(detail.case.metrics)
                                .map(([key, value]) => `${key}: ${String(value)}`)
                                .join(" · ")}
                        </div>
                    )}
                </div>
            )}

            {detail?.coach && (
                <div
                    style={{
                        background: "var(--ink)",
                        color: "var(--bg)",
                        borderRadius: "var(--r-md)",
                        padding: 20,
                        marginTop: 20,
                    }}
                >
                    <div className="row gap-3" style={{ alignItems: "center", marginBottom: 14 }}>
                        <GeoAvatar seed={detail.coach.avatarSeed} size={44} />
                        <div>
                            <div className="h4" style={{ fontSize: 14 }}>
                                {detail.coach.name}
                            </div>
                            <span className="eyebrow muted" style={{ color: "var(--ink-4)" }}>
                                {detail.coach.role}
                            </span>
                        </div>
                    </div>
                    <p className="body" style={{ color: "var(--ink-2)" }}>
                        {detail.coach.quote}
                    </p>
                    {detail.coach.challenges.length > 0 && (
                        <div className="col gap-2" style={{ marginTop: 14 }}>
                            <span className="eyebrow muted" style={{ color: "var(--ink-4)" }}>
                                Практика
                                <span className="dot">·</span>
                                <span>{detail.coach.challenges.length} микро-вызова</span>
                            </span>
                            {detail.coach.challenges.map((challenge, index) => (
                                <div
                                    key={index}
                                    className="small"
                                    style={{
                                        padding: "8px 12px",
                                        borderRadius: "var(--r-sm)",
                                        background: "var(--ink-2)",
                                        color: "var(--bg)",
                                        fontFamily: "var(--font-mono)",
                                    }}
                                >
                                    {challenge.label}
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            )}

            {card.primarySkillIconicName && (
                <div className="row gap-2 wrap" style={{ marginTop: 20 }}>
                    <Link href={`/skill/${card.primarySkillIconicName}`}>
                        <Button variant="ghost" size="md">
                            Связанный навык →
                        </Button>
                    </Link>
                </div>
            )}
        </div>
    );
}
