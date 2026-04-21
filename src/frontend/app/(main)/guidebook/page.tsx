"use client";

import { useEffect, useState, useDeferredValue } from "react";
import Link from "next/link";
import ReactMarkdown from "react-markdown";
import {
    useTechniques,
    useTechniquesMeta,
    useTechnique,
    useMarkTechniqueSeen,
    type TechniqueCard as TechniqueCardData,
    type TechniqueDetail,
} from "@/lib/hooks/useTechniques";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";
import { StatTile } from "@/components/ui/StatTile";
import { GeoAvatar } from "@/components/ui/GeoAvatar";

function MasteryRing({ masteryLevel, masteryPercent }: { masteryLevel: number; masteryPercent: number }) {
    const size = 56;
    const radius = 24;
    const circumference = 2 * Math.PI * radius;
    const fraction = Math.max(0, Math.min(1, masteryPercent / 100));
    const display = masteryLevel === 0 ? "—" : `L${masteryLevel}`;
    return (
        <div style={{ position: "relative", width: size, height: size, flexShrink: 0 }}>
            <svg width={size} height={size} style={{ transform: "rotate(-90deg)" }}>
                <circle cx={size / 2} cy={size / 2} r={radius} fill="none" stroke="var(--bg-2)" strokeWidth={4} />
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    stroke="var(--indigo)"
                    strokeWidth={4}
                    strokeDasharray={circumference}
                    strokeDashoffset={circumference * (1 - fraction)}
                    strokeLinecap="round"
                />
            </svg>
            <div
                style={{
                    position: "absolute",
                    inset: 0,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontFamily: "var(--f-mono)",
                    fontSize: 12,
                    fontWeight: 500,
                    color: "var(--indigo)",
                }}
            >
                {display}
            </div>
        </div>
    );
}

function Chip({ children, tone = "neutral", size = "sm" }: { children: React.ReactNode; tone?: string; size?: string }) {
    const toneStyles: Record<string, { background: string; color: string }> = {
        neutral: { background: "var(--bg-2)", color: "var(--ink-2)" },
        ghost: { background: "transparent", color: "var(--ink-3)" },
        rust: { background: "var(--rust-soft)", color: "var(--rust)" },
        olive: { background: "var(--olive-soft)", color: "var(--olive)" },
        indigo: { background: "var(--indigo-soft)", color: "var(--indigo)" },
        good: { background: "var(--good-soft)", color: "var(--good)" },
        warn: { background: "var(--warn-soft)", color: "var(--warn)" },
    };
    const style = toneStyles[tone] ?? toneStyles.neutral;
    return (
        <span
            style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 4,
                padding: size === "sm" ? "4px 10px" : "6px 14px",
                borderRadius: 999,
                fontSize: size === "sm" ? 11 : 13,
                fontWeight: 500,
                background: style.background,
                color: style.color,
                border: tone === "ghost" ? "1px solid var(--line)" : "none",
            }}
        >
            {children}
        </span>
    );
}

function SectionLabel({ children }: { children: React.ReactNode }) {
    return (
        <div
            style={{
                fontSize: 11,
                color: "var(--ink-3)",
                letterSpacing: 1.5,
                textTransform: "uppercase",
                fontWeight: 500,
                fontFamily: "var(--f-mono)",
                marginBottom: 10,
            }}
        >
            {children}
        </div>
    );
}

function TagChip({
    tag,
    onClick,
    isActive = false,
}: {
    tag: string;
    onClick: (tag: string) => void;
    isActive?: boolean;
}) {
    return (
        <button
            onClick={(event) => {
                event.stopPropagation();
                onClick(tag);
            }}
            style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 4,
                padding: "4px 10px",
                borderRadius: 999,
                fontSize: 11,
                fontWeight: 500,
                fontFamily: "var(--f-sans)",
                background: isActive ? "var(--bg-2)" : "transparent",
                color: isActive ? "var(--ink)" : "var(--ink-3)",
                border: `1px solid ${isActive ? "var(--line-2)" : "var(--line)"}`,
                cursor: "pointer",
            }}
        >
            #{tag}
        </button>
    );
}

function ActiveTagPill({ tag, onRemove }: { tag: string; onRemove: (tag: string) => void }) {
    return (
        <button
            onClick={() => onRemove(tag)}
            style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 8,
                padding: "0 10px 0 14px",
                borderRadius: 999,
                fontSize: 13,
                fontWeight: 500,
                fontFamily: "var(--f-sans)",
                background: "var(--bg-2)",
                color: "var(--ink-2)",
                border: "1px solid var(--line)",
                cursor: "pointer",
                height: 34,
            }}
            aria-label={`Убрать фильтр по тегу ${tag}`}
        >
            <span style={{ color: "var(--ink-4)", marginRight: -4 }}>#</span>
            {tag}
            <span
                aria-hidden
                style={{
                    display: "inline-flex",
                    alignItems: "center",
                    justifyContent: "center",
                    width: 18,
                    height: 18,
                    borderRadius: "50%",
                    color: "var(--ink-3)",
                    fontSize: 14,
                    lineHeight: 1,
                    transition: "background 0.15s, color 0.15s",
                }}
                onMouseEnter={(event) => {
                    event.currentTarget.style.background = "var(--line)";
                    event.currentTarget.style.color = "var(--ink)";
                }}
                onMouseLeave={(event) => {
                    event.currentTarget.style.background = "transparent";
                    event.currentTarget.style.color = "var(--ink-3)";
                }}
            >
                ×
            </span>
        </button>
    );
}

function Bubble({
    side,
    children,
    annotation,
}: {
    side: "me" | "them";
    children: React.ReactNode;
    annotation?: string;
}) {
    return (
        <div
            style={{
                padding: "10px 14px",
                borderRadius: side === "me" ? "14px 14px 4px 14px" : "4px 14px 14px 14px",
                background: side === "me" ? "var(--indigo)" : "var(--bg-2)",
                color: side === "me" ? "white" : "var(--ink)",
                fontSize: 14,
                lineHeight: 1.4,
                maxWidth: "85%",
                alignSelf: side === "me" ? "flex-end" : "flex-start",
            }}
        >
            {children}
            {annotation && (
                <span style={{ marginLeft: 6, color: side === "me" ? "white" : "var(--indigo)", opacity: 0.8 }}>
                    [{annotation}]
                </span>
            )}
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

    function removeActiveTag(tag: string) {
        setActiveTags((current) => current.filter((existing) => existing !== tag));
    }

    return (
        <div style={{ minHeight: "100vh", background: "var(--bg)" }}>
            <div
                style={{
                    padding: "40px 60px 32px",
                    borderBottom: "1px solid var(--line)",
                    background: "var(--surface-2)",
                }}
            >
                <div
                    style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "flex-end",
                        gap: 32,
                        maxWidth: 1200,
                        margin: "0 auto",
                    }}
                >
                    <div>
                        <div
                            style={{
                                fontSize: 12,
                                color: "var(--indigo)",
                                letterSpacing: 2,
                                textTransform: "uppercase",
                                fontWeight: 500,
                                marginBottom: 10,
                                fontFamily: "var(--f-mono)",
                            }}
                        >
                            СПРАВОЧНИК · {totalCount} ТЕХНИК
                        </div>
                        <h1 style={{ margin: 0, fontSize: 48, letterSpacing: -1.5, fontWeight: 500, lineHeight: 1 }}>
                            Коллекция.
                        </h1>
                        <p style={{ fontSize: 16, color: "var(--ink-3)", marginTop: 10, maxWidth: 520 }}>
                            Не каталог — коллекция. Каждая техника — живой организм с уровнем мастерства, примером диалога и кейсом.
                        </p>
                    </div>
                    <div style={{ display: "flex", gap: 8 }}>
                        <StatTile
                            big
                            label="Освоено"
                            value={`${userCounts.mastered}`}
                            unit={`/ ${totalCount}`}
                            icon={<Icon name="check" size="xs" />}
                            tone="olive"
                        />
                        <StatTile
                            big
                            label="Мастер"
                            value={`${userCounts.master}`}
                            icon={<Icon name="trophy" size="xs" />}
                            tone="indigo"
                        />
                        <StatTile
                            big
                            label="Новых"
                            value={`${userCounts.unseen}`}
                            icon={<Icon name="sparkle" size="xs" />}
                            tone="indigo"
                        />
                    </div>
                </div>
            </div>

            <div style={{ padding: "24px 60px 0", maxWidth: 1200, margin: "0 auto" }}>
                <div style={{ display: "flex", gap: 16, alignItems: "center", flexWrap: "wrap" }}>
                    <div
                        style={{
                            flex: "0 1 360px",
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            padding: "0 14px",
                            height: 42,
                            background: "var(--surface)",
                            border: "1px solid var(--line-2)",
                            borderRadius: 12,
                        }}
                    >
                        <Icon name="search" size="sm" color="var(--ink-3)" />
                        <input
                            placeholder="Техника, тег, навык…"
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                            style={{
                                flex: 1,
                                border: "none",
                                outline: "none",
                                background: "transparent",
                                fontFamily: "var(--f-sans)",
                                fontSize: 14,
                                color: "var(--ink)",
                            }}
                        />
                        {searchInput && (
                            <button
                                onClick={() => setSearchInput("")}
                                style={{
                                    background: "transparent",
                                    border: "none",
                                    cursor: "pointer",
                                    color: "var(--ink-3)",
                                }}
                            >
                                <Icon name="close" size="sm" />
                            </button>
                        )}
                    </div>

                    {activeTags.length > 0 && (
                        <div style={{ display: "flex", gap: 6, flexWrap: "wrap", alignItems: "center" }}>
                            {activeTags.map((tag) => (
                                <ActiveTagPill key={tag} tag={tag} onRemove={removeActiveTag} />
                            ))}
                            <button
                                onClick={() => setActiveTags([])}
                                style={{
                                    background: "transparent",
                                    border: "none",
                                    color: "var(--ink-3)",
                                    fontSize: 11,
                                    letterSpacing: 1.5,
                                    cursor: "pointer",
                                    fontFamily: "var(--f-mono)",
                                    textTransform: "uppercase",
                                    marginLeft: 16,
                                    padding: 0,
                                }}
                            >
                                Очистить
                            </button>
                        </div>
                    )}

                    <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                        <SkillPill
                            label="Все"
                            isSelected={selectedSkill === null}
                            onSelect={() => setSelectedSkill(null)}
                        />
                        {skills.map((skill) => (
                            <SkillPill
                                key={skill.iconicName}
                                label={skill.title}
                                count={skill.techniqueCount}
                                isSelected={selectedSkill === skill.iconicName}
                                onSelect={() => setSelectedSkill(skill.iconicName)}
                            />
                        ))}
                    </div>
                </div>
            </div>

            <div style={{ padding: "24px 60px 80px", maxWidth: 1200, margin: "0 auto" }}>
                {isLoading ? (
                    <div style={{ display: "flex", justifyContent: "center", padding: "48px 0" }}>
                        <div
                            style={{
                                width: 32,
                                height: 32,
                                borderRadius: "50%",
                                border: "4px solid var(--indigo)",
                                borderTopColor: "transparent",
                                animation: "spin 0.8s linear infinite",
                            }}
                        />
                    </div>
                ) : cards.length === 0 ? (
                    <div style={{ textAlign: "center", padding: "64px 0" }}>
                        <div
                            style={{
                                width: 64,
                                height: 64,
                                borderRadius: "50%",
                                background: "var(--surface)",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                margin: "0 auto 16px",
                            }}
                        >
                            <Icon name="search" size="lg" color="var(--ink-3)" />
                        </div>
                        <p style={{ fontWeight: 600, marginBottom: 4 }}>Ничего не найдено</p>
                        <p style={{ fontSize: 14, color: "var(--ink-3)" }}>Попробуй другой запрос или навык</p>
                    </div>
                ) : (
                    <div style={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 16 }}>
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

function SkillPill({
    label,
    count,
    isSelected,
    onSelect,
}: {
    label: string;
    count?: number;
    isSelected: boolean;
    onSelect: () => void;
}) {
    return (
        <button
            onClick={onSelect}
            style={{
                padding: "8px 14px",
                borderRadius: 999,
                cursor: "pointer",
                background: isSelected ? "var(--ink)" : "var(--surface)",
                color: isSelected ? "var(--bg)" : "var(--ink-2)",
                border: `1px solid ${isSelected ? "var(--ink)" : "var(--line)"}`,
                fontSize: 13,
                fontWeight: 500,
                fontFamily: "var(--f-sans)",
                display: "inline-flex",
                alignItems: "center",
                gap: 6,
            }}
        >
            {label}
            {typeof count === "number" && (
                <span
                    style={{
                        fontFamily: "var(--f-mono)",
                        fontSize: 11,
                        opacity: 0.6,
                    }}
                >
                    {count}
                </span>
            )}
        </button>
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
    card: TechniqueCardData;
    isExpanded: boolean;
    detail: TechniqueDetail | null;
    onToggle: () => void;
    onTagClick: (tag: string) => void;
    activeTags: string[];
}) {
    return (
        <div
            style={{
                background: "var(--surface)",
                border: "1px solid var(--line)",
                borderRadius: 20,
                overflow: "hidden",
                transition: "all 0.2s",
                gridColumn: isExpanded ? "span 2" : "span 1",
                boxShadow: isExpanded ? "var(--sh-3)" : "var(--sh-1)",
            }}
        >
            <div
                role="button"
                tabIndex={0}
                onClick={onToggle}
                onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        onToggle();
                    }
                }}
                style={{
                    width: "100%",
                    padding: 24,
                    textAlign: "left",
                    background: "transparent",
                    cursor: "pointer",
                    fontFamily: "var(--f-sans)",
                    display: "flex",
                    gap: 20,
                    alignItems: "flex-start",
                }}
            >
                <MasteryRing masteryLevel={card.masteryLevel} masteryPercent={card.masteryPercent} />

                <div style={{ flex: 1 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6, flexWrap: "wrap" }}>
                        {card.primarySkillTitle && (
                            <Chip tone="neutral" size="sm">
                                {card.primarySkillTitle}
                            </Chip>
                        )}
                        {card.tags.slice(0, 2).map((tag) => (
                            <TagChip key={tag} tag={tag} onClick={onTagClick} isActive={activeTags.includes(tag)} />
                        ))}
                        {card.isNew && (
                            <Chip tone="indigo" size="sm">
                                Новое
                            </Chip>
                        )}
                    </div>
                    <div style={{ fontSize: 22, fontWeight: 500, letterSpacing: -0.4, marginBottom: 4 }}>
                        {card.name}
                    </div>
                    <div style={{ fontSize: 13, color: "var(--ink-3)", lineHeight: 1.5 }}>{card.summary}</div>
                </div>

                <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4 }}>
                    <div
                        style={{
                            fontSize: 11,
                            fontFamily: "var(--f-mono)",
                            color: "var(--ink-3)",
                            letterSpacing: 0.5,
                        }}
                    >
                        УРОВЕНЬ
                    </div>
                    <div style={{ fontSize: 14, fontWeight: 500, color: "var(--indigo)" }}>{card.difficultyName}</div>
                    <Icon name={isExpanded ? "chevron-up" : "chevron-down"} size="sm" color="var(--ink-3)" />
                </div>
            </div>

            {isExpanded && (
                <div
                    style={{
                        borderTop: "1px solid var(--line)",
                        padding: 24,
                        display: "grid",
                        gridTemplateColumns: card.hasCoach ? "1fr 300px" : "1fr",
                        gap: 32,
                    }}
                >
                    <div>
                        {detail?.body && (
                            <div
                                style={{
                                    fontSize: 14,
                                    color: "var(--ink-2)",
                                    lineHeight: 1.6,
                                    marginBottom: 24,
                                }}
                            >
                                <ReactMarkdown>{detail.body}</ReactMarkdown>
                            </div>
                        )}

                        {detail && detail.dialogTurns.length > 0 && (
                            <div style={{ marginBottom: 24 }}>
                                <SectionLabel>Пример диалога</SectionLabel>
                                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                                    {detail.dialogTurns.map((turn) => (
                                        <Bubble
                                            key={turn.orderIndex}
                                            side={turn.side === "me" ? "me" : "them"}
                                            annotation={
                                                turn.annotations.map((annotation) => annotation.label).join(" · ") || undefined
                                            }
                                        >
                                            {turn.text}
                                        </Bubble>
                                    ))}
                                </div>
                            </div>
                        )}

                        {detail?.case && (
                            <div style={{ marginBottom: 24 }}>
                                <SectionLabel>Кейс</SectionLabel>
                                <div
                                    style={{
                                        padding: 18,
                                        background: "var(--bg-2)",
                                        borderRadius: 12,
                                        fontSize: 14,
                                        color: "var(--ink-2)",
                                        lineHeight: 1.55,
                                    }}
                                >
                                    <b>{detail.case.title}</b> · {detail.case.body}
                                    {detail.case.metrics && (
                                        <div
                                            style={{
                                                marginTop: 10,
                                                display: "flex",
                                                gap: 10,
                                                flexWrap: "wrap",
                                                fontFamily: "var(--f-mono)",
                                                fontSize: 12,
                                                color: "var(--ink-3)",
                                            }}
                                        >
                                            {Object.entries(detail.case.metrics).map(([key, value]) => (
                                                <span key={key}>
                                                    {key}: {String(value)}
                                                </span>
                                            ))}
                                        </div>
                                    )}
                                </div>
                            </div>
                        )}

                        {card.primarySkillIconicName && (
                            <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                                <Link href={`/skill/${card.primarySkillIconicName}`}>
                                    <Button variant="ghost" size="md">
                                        Связанный навык →
                                    </Button>
                                </Link>
                            </div>
                        )}
                    </div>

                    {detail?.coach && (
                        <div
                            style={{
                                background: "var(--ink)",
                                color: "var(--bg)",
                                borderRadius: 16,
                                padding: 20,
                                alignSelf: "flex-start",
                            }}
                        >
                            <div style={{ display: "flex", gap: 12, alignItems: "center", marginBottom: 14 }}>
                                <GeoAvatar seed={detail.coach.avatarSeed} size={44} />
                                <div>
                                    <div style={{ fontSize: 13, fontWeight: 500 }}>{detail.coach.name}</div>
                                    <div
                                        style={{
                                            fontSize: 10,
                                            color: "var(--ink-4)",
                                            fontFamily: "var(--f-mono)",
                                            textTransform: "uppercase",
                                            letterSpacing: 1,
                                        }}
                                    >
                                        {detail.coach.role}
                                    </div>
                                </div>
                            </div>
                            <div style={{ fontSize: 13, lineHeight: 1.5, color: "var(--ink-2)" }}>{detail.coach.quote}</div>
                            {detail.coach.challenges.length > 0 && (
                                <>
                                    <div
                                        style={{
                                            marginTop: 14,
                                            paddingTop: 14,
                                            borderTop: "1px solid var(--ink-2)",
                                            fontSize: 11,
                                            fontFamily: "var(--f-mono)",
                                            color: "var(--ink-4)",
                                        }}
                                    >
                                        Практика · {detail.coach.challenges.length} микро-
                                        {detail.coach.challenges.length === 1 ? "вызов" : "вызова"}:
                                    </div>
                                    <div style={{ display: "flex", flexDirection: "column", gap: 6, marginTop: 10 }}>
                                        {detail.coach.challenges.map((challenge, index) => (
                                            <div
                                                key={index}
                                                style={{
                                                    textAlign: "left",
                                                    padding: "8px 12px",
                                                    borderRadius: 8,
                                                    background: "var(--ink-2)",
                                                    color: "var(--bg)",
                                                    fontSize: 12,
                                                    fontFamily: "var(--f-mono)",
                                                }}
                                            >
                                                {challenge.label}
                                            </div>
                                        ))}
                                    </div>
                                </>
                            )}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}
