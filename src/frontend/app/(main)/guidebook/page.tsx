"use client";

import { useState, useDeferredValue } from "react";
import Link from "next/link";
import ReactMarkdown from "react-markdown";
import { useHandbook, useHandbookCategories } from "@/lib/hooks/useReference";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";
import { StatTile } from "@/components/ui/StatTile";
import { GeoAvatar } from "@/components/ui/GeoAvatar";

const CATEGORY_CONFIG: Record<string, { label: string; color: string }> = {
    all: { label: "Все", color: "var(--ink)" },
    objections: { label: "Возражения", color: "var(--rust)" },
    "cold-calls": { label: "Холодные звонки", color: "var(--indigo)" },
    closing: { label: "Закрытие", color: "var(--olive)" },
    discovery: { label: "Квалификация", color: "var(--clay)" },
    rapport: { label: "Rapport", color: "var(--sage)" },
    negotiation: { label: "Переговоры", color: "var(--ink-2)" },
};

function categoryLabel(cat: string): string {
    return CATEGORY_CONFIG[cat]?.label ?? cat;
}

function categoryColor(cat: string | null): string {
    return cat ? (CATEGORY_CONFIG[cat]?.color ?? "var(--ink-3)") : "var(--ink-3)";
}

function MasteryRing({ level, mastered }: { level: number; mastered: number }) {
    const size = 56;
    const r = 24;
    const circ = 2 * Math.PI * r;
    const pct = mastered / 100;
    return (
        <div style={{ position: "relative", width: size, height: size, flexShrink: 0 }}>
            <svg width={size} height={size} style={{ transform: "rotate(-90deg)" }}>
                <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--bg-2)" strokeWidth={4} />
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={r}
                    fill="none"
                    stroke="var(--rust)"
                    strokeWidth={4}
                    strokeDasharray={circ}
                    strokeDashoffset={circ * (1 - pct)}
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
                    color: "var(--rust)",
                }}
            >
                L{level}
            </div>
        </div>
    );
}

function Chip({ children, tone = "neutral", size = "sm" }: { children: React.ReactNode; tone?: string; size?: string }) {
    const toneStyles: Record<string, { bg: string; color: string }> = {
        neutral: { bg: "var(--bg-2)", color: "var(--ink-2)" },
        ghost: { bg: "transparent", color: "var(--ink-3)" },
        rust: { bg: "var(--rust-soft)", color: "var(--rust)" },
        olive: { bg: "var(--olive-soft)", color: "var(--olive)" },
        indigo: { bg: "var(--indigo-soft)", color: "var(--indigo)" },
        good: { bg: "var(--good-soft)", color: "var(--good)" },
        warn: { bg: "var(--warn-soft)", color: "var(--warn)" },
    };
    const t = toneStyles[tone] ?? toneStyles.neutral;
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
                background: t.bg,
                color: t.color,
                border: tone === "ghost" ? "1px solid var(--line)" : "none",
            }}
        >
            {children}
        </span>
    );
}

function Bubble({ side, children }: { side: "me" | "them"; children: React.ReactNode }) {
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
        </div>
    );
}

export default function GuidebookPage() {
    const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
    const [searchInput, setSearchInput] = useState("");
    const [expandedId, setExpandedId] = useState<string | null>(null);

    const deferredSearch = useDeferredValue(searchInput);

    const { data: categories = [] } = useHandbookCategories();
    const { data: materials = [], isLoading } = useHandbook(
        selectedCategory ?? undefined,
        deferredSearch || undefined
    );

    function toggleExpand(id: string) {
        setExpandedId((prev) => (prev === id ? null : id));
    }

    const allCategories = ["all", ...categories];

    return (
        <div style={{ minHeight: "100vh", background: "var(--bg)" }}>
            {/* Hero header */}
            <div
                style={{
                    padding: "40px 60px 32px",
                    borderBottom: "1px solid var(--line)",
                    background: "var(--surface-2)",
                }}
            >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-end", gap: 32, maxWidth: 1200, margin: "0 auto" }}>
                    <div>
                        <div
                            style={{
                                fontSize: 12,
                                color: "var(--rust)",
                                letterSpacing: 2,
                                textTransform: "uppercase",
                                fontWeight: 500,
                                marginBottom: 10,
                                fontFamily: "var(--f-mono)",
                            }}
                        >
                            СПРАВОЧНИК · {materials.length} ТЕХНИК
                        </div>
                        <h1 style={{ margin: 0, fontSize: 48, letterSpacing: -1.5, fontWeight: 500, lineHeight: 1 }}>
                            Коллекция.
                        </h1>
                        <p style={{ fontSize: 16, color: "var(--ink-3)", marginTop: 10, maxWidth: 520 }}>
                            Не каталог — коллекция. Каждая техника — живой организм с уровнем мастерства, примером диалога и кейсом.
                        </p>
                    </div>
                    <div style={{ display: "flex", gap: 8 }}>
                        <StatTile big label="Освоено" value="—" icon={<Icon name="check" size="xs" />} tone="olive" />
                        <StatTile big label="Мастер" value="—" icon={<Icon name="trophy" size="xs" />} tone="rust" />
                        <StatTile big label="Новых" value={String(materials.length)} icon={<Icon name="sparkle" size="xs" />} tone="indigo" />
                    </div>
                </div>
            </div>

            {/* Search + category filter */}
            <div style={{ padding: "24px 60px 0", maxWidth: 1200, margin: "0 auto" }}>
                <div style={{ display: "flex", gap: 16, alignItems: "center", flexWrap: "wrap" }}>
                    {/* Search */}
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
                            onChange={(e) => setSearchInput(e.target.value)}
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
                                style={{ background: "transparent", border: "none", cursor: "pointer", color: "var(--ink-3)" }}
                            >
                                <Icon name="close" size="sm" />
                            </button>
                        )}
                    </div>

                    {/* Category chips */}
                    <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                        {allCategories.map((cat) => {
                            const isSelected = cat === "all" ? selectedCategory === null : selectedCategory === cat;
                            const color = categoryColor(cat);
                            return (
                                <button
                                    key={cat}
                                    onClick={() => setSelectedCategory(cat === "all" ? null : cat)}
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
                                    <span
                                        style={{
                                            width: 6,
                                            height: 6,
                                            borderRadius: 2,
                                            background: color,
                                        }}
                                    />
                                    {categoryLabel(cat)}
                                </button>
                            );
                        })}
                    </div>
                </div>
            </div>

            {/* Content */}
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
                ) : materials.length === 0 ? (
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
                        <p style={{ fontSize: 14, color: "var(--ink-3)" }}>Попробуй другой запрос или категорию</p>
                    </div>
                ) : (
                    <div style={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 16 }}>
                        {materials.map((material) => {
                            const isExpanded = expandedId === material.materialId;
                            const excerpt = material.markdownContent.replace(/[#*_`]/g, "").slice(0, 150);
                            const color = categoryColor(material.category);

                            return (
                                <div
                                    key={material.materialId}
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
                                    {/* Card header */}
                                    <button
                                        onClick={() => toggleExpand(material.materialId)}
                                        style={{
                                            width: "100%",
                                            padding: 24,
                                            textAlign: "left",
                                            background: "transparent",
                                            border: "none",
                                            cursor: "pointer",
                                            fontFamily: "var(--f-sans)",
                                            display: "flex",
                                            gap: 20,
                                            alignItems: "flex-start",
                                        }}
                                    >
                                        {/* Mastery ring placeholder */}
                                        <MasteryRing level={1} mastered={Math.random() * 100} />

                                        <div style={{ flex: 1 }}>
                                            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6, flexWrap: "wrap" }}>
                                                {material.category && (
                                                    <Chip tone="neutral" size="sm">
                                                        {categoryLabel(material.category)}
                                                    </Chip>
                                                )}
                                                {material.tags.slice(0, 2).map((tag) => (
                                                    <Chip key={tag} tone="ghost" size="sm">
                                                        #{tag}
                                                    </Chip>
                                                ))}
                                            </div>
                                            <div style={{ fontSize: 22, fontWeight: 500, letterSpacing: -0.4, marginBottom: 4 }}>
                                                {material.title}
                                            </div>
                                            {!isExpanded && (
                                                <div style={{ fontSize: 13, color: "var(--ink-3)", lineHeight: 1.5 }}>
                                                    {excerpt}
                                                    {excerpt.length >= 150 ? "…" : ""}
                                                </div>
                                            )}
                                        </div>

                                        <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4 }}>
                                            <div style={{ fontSize: 11, fontFamily: "var(--f-mono)", color: "var(--ink-3)", letterSpacing: 0.5 }}>
                                                УРОВЕНЬ
                                            </div>
                                            <div style={{ fontSize: 14, fontWeight: 500, color: "var(--rust)" }}>Novice</div>
                                            <Icon name={isExpanded ? "chevron-up" : "chevron-down"} size="sm" color="var(--ink-3)" />
                                        </div>
                                    </button>

                                    {/* Expanded content */}
                                    {isExpanded && (
                                        <div
                                            style={{
                                                borderTop: "1px solid var(--line)",
                                                padding: 24,
                                                display: "grid",
                                                gridTemplateColumns: "1fr 300px",
                                                gap: 32,
                                            }}
                                        >
                                            <div>
                                                {/* Content */}
                                                <div
                                                    style={{
                                                        fontSize: 14,
                                                        color: "var(--ink-2)",
                                                        lineHeight: 1.6,
                                                        marginBottom: 24,
                                                    }}
                                                >
                                                    <ReactMarkdown>{material.markdownContent}</ReactMarkdown>
                                                </div>

                                                {/* Sample dialog placeholder */}
                                                <div style={{ marginBottom: 24 }}>
                                                    <div
                                                        style={{
                                                            fontSize: 11,
                                                            color: "var(--ink-3)",
                                                            letterSpacing: 1.5,
                                                            textTransform: "uppercase",
                                                            fontWeight: 500,
                                                            marginBottom: 10,
                                                        }}
                                                    >
                                                        Пример диалога
                                                    </div>
                                                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                                                        <Bubble side="them">У нас всё хорошо с текущим решением. Спасибо.</Bubble>
                                                        <Bubble side="me">
                                                            Понимаю. Что для вас сейчас сработало лучше всего?
                                                        </Bubble>
                                                    </div>
                                                </div>

                                                <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                                                    <Button variant="accent" size="md" iconRightName="arrow-right">
                                                        Практиковать сейчас
                                                    </Button>
                                                    {material.skillSlug && (
                                                        <Link href={`/skill/${material.skillSlug}`}>
                                                            <Button variant="ghost" size="md">
                                                                Связанный навык →
                                                            </Button>
                                                        </Link>
                                                    )}
                                                </div>
                                            </div>

                                            {/* Coach sidecar */}
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
                                                    <GeoAvatar seed="sergey" size={44} />
                                                    <div>
                                                        <div style={{ fontSize: 13, fontWeight: 500 }}>Skeptic Sergey</div>
                                                        <div
                                                            style={{
                                                                fontSize: 10,
                                                                color: "var(--ink-4)",
                                                                fontFamily: "var(--f-mono)",
                                                                textTransform: "uppercase",
                                                                letterSpacing: 1,
                                                            }}
                                                        >
                                                            Коуч · возражения
                                                        </div>
                                                    </div>
                                                </div>
                                                <div style={{ fontSize: 13, lineHeight: 1.5, color: "var(--ink-2)" }}>
                                                    «Эта техника работает, пока ты не начинаешь задавать вопросы ради вопросов.
                                                    Каждый вопрос должен двигать разговор вперёд.»
                                                </div>
                                            </div>
                                        </div>
                                    )}
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>
        </div>
    );
}
