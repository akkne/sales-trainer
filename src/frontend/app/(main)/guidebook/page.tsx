"use client";

import { useState, useDeferredValue } from "react";
import Link from "next/link";
import ReactMarkdown from "react-markdown";
import { useHandbook, useHandbookCategories } from "@/lib/hooks/useReference";
import { Icon } from "@/components/ui/Icon";

const CATEGORY_LABELS: Record<string, string> = {
    "objections": "Возражения",
    "cold-calls": "Холодные звонки",
    "closing": "Закрытие",
    "discovery": "Квалификация",
    "rapport": "Rapport",
    "negotiation": "Переговоры",
};

const CATEGORY_ICONS: Record<string, string> = {
    "objections": "verified_user",
    "cold-calls": "call",
    "closing": "handshake",
    "discovery": "psychology",
    "rapport": "diversity_3",
    "negotiation": "balance",
};

const CATEGORY_COLORS: Record<string, { bg: string; text: string }> = {
    "objections": { bg: "bg-error-container", text: "text-error" },
    "cold-calls": { bg: "bg-tertiary-container", text: "text-tertiary" },
    "closing": { bg: "bg-primary-container", text: "text-primary" },
    "discovery": { bg: "bg-secondary-container", text: "text-secondary" },
    "rapport": { bg: "bg-[#E8DEF8]", text: "text-[#6750A4]" },
    "negotiation": { bg: "bg-[#FFE0B2]", text: "text-[#E65100]" },
};

function categoryLabel(cat: string): string {
    return CATEGORY_LABELS[cat] ?? cat;
}

function categoryIcon(cat: string | null): string {
    return cat ? (CATEGORY_ICONS[cat] ?? "article") : "article";
}

function categoryColors(cat: string | null): { bg: string; text: string } {
    if (!cat) return { bg: "bg-surface-container", text: "text-on-surface-variant" };
    return CATEGORY_COLORS[cat] ?? { bg: "bg-surface-container", text: "text-on-surface-variant" };
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

    return (
        <div className="max-w-3xl mx-auto px-4 py-8">
            {/* Header */}
            <div className="mb-6">
                <div className="flex items-center gap-2 mb-2">
                    <Icon name="menu_book" size="lg" className="text-primary" />
                    <h1 className="font-headline font-bold text-2xl text-on-surface">Справочник</h1>
                </div>
                <p className="text-sm text-on-surface-variant">Ключевые техники продаж</p>
            </div>

            {/* Search */}
            <div className="relative mb-4">
                <Icon name="search" size="md" className="absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant" />
                <input
                    type="text"
                    placeholder="Поиск техник..."
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    className="w-full pl-10 pr-10 py-3 rounded-full bg-surface-container-low text-on-surface placeholder-on-surface-variant outline-none focus:ring-2 focus:ring-primary border-2 border-transparent focus:border-primary tonal-transition"
                />
                {searchInput && (
                    <button
                        onClick={() => setSearchInput("")}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-on-surface-variant hover:text-on-surface tonal-transition"
                    >
                        <Icon name="close" size="sm" />
                    </button>
                )}
            </div>

            {/* Category filter chips */}
            {categories.length > 0 && (
                <div className="flex gap-2 flex-wrap mb-6">
                    <button
                        onClick={() => setSelectedCategory(null)}
                        className={`px-4 py-2 rounded-full text-sm font-semibold tonal-transition ${
                            selectedCategory === null
                                ? "bg-primary text-on-primary"
                                : "bg-surface-container text-on-surface-variant hover:bg-surface-container-high"
                        }`}
                    >
                        Все
                    </button>
                    {categories.map((cat) => {
                        const colors = categoryColors(cat);
                        const isSelected = selectedCategory === cat;
                        return (
                            <button
                                key={cat}
                                onClick={() => setSelectedCategory(cat === selectedCategory ? null : cat)}
                                className={`flex items-center gap-1.5 px-4 py-2 rounded-full text-sm font-semibold tonal-transition ${
                                    isSelected
                                        ? "bg-primary text-on-primary"
                                        : `${colors.bg} ${colors.text} hover:opacity-80`
                                }`}
                            >
                                <Icon name={categoryIcon(cat)} size="sm" />
                                {categoryLabel(cat)}
                            </button>
                        );
                    })}
                </div>
            )}

            {/* Results */}
            {isLoading ? (
                <div className="flex justify-center py-12">
                    <div className="w-8 h-8 rounded-full border-4 border-primary border-t-transparent animate-spin" />
                </div>
            ) : materials.length === 0 ? (
                <div className="text-center py-16">
                    <div className="w-16 h-16 rounded-full bg-surface-container flex items-center justify-center mx-auto mb-4">
                        <Icon name="search_off" size="xl" className="text-on-surface-variant" />
                    </div>
                    <p className="font-semibold text-on-surface mb-1">Ничего не найдено</p>
                    <p className="text-sm text-on-surface-variant">Попробуй другой запрос или категорию</p>
                </div>
            ) : (
                <div className="flex flex-col gap-4">
                    {materials.map((material) => {
                        const isExpanded = expandedId === material.materialId;
                        const colors = categoryColors(material.category);
                        // First ~150 chars of content as excerpt
                        const excerpt = material.markdownContent.replace(/[#*_`]/g, "").slice(0, 150);

                        return (
                            <div
                                key={material.materialId}
                                className={`rounded-2xl overflow-hidden tonal-transition ${
                                    isExpanded
                                        ? "bg-surface-container-lowest ring-2 ring-primary"
                                        : "bg-surface-container-lowest hover:bg-surface-container"
                                }`}
                            >
                                {/* Card header — always visible */}
                                <button
                                    onClick={() => toggleExpand(material.materialId)}
                                    className="w-full text-left px-5 py-4"
                                >
                                    <div className="flex items-start justify-between gap-3">
                                        <div className="flex-1 min-w-0">
                                            {/* Category and tags */}
                                            <div className="flex items-center gap-2 flex-wrap mb-2">
                                                {material.category && (
                                                    <span className={`flex items-center gap-1 text-xs font-semibold px-2.5 py-0.5 rounded-full ${colors.bg} ${colors.text}`}>
                                                        <Icon name={categoryIcon(material.category)} size="sm" />
                                                        {categoryLabel(material.category)}
                                                    </span>
                                                )}
                                                {material.tags.slice(0, 2).map((tag) => (
                                                    <span key={tag} className="text-xs text-on-surface-variant bg-surface-container px-2 py-0.5 rounded-full">
                                                        #{tag}
                                                    </span>
                                                ))}
                                            </div>

                                            <p className="font-bold text-on-surface text-base leading-snug mb-1">
                                                {material.title}
                                            </p>

                                            {!isExpanded && (
                                                <p className="text-sm text-on-surface-variant line-clamp-2">
                                                    {excerpt}{excerpt.length >= 150 ? "…" : ""}
                                                </p>
                                            )}
                                        </div>

                                        <Icon
                                            name={isExpanded ? "expand_less" : "expand_more"}
                                            size="md"
                                            className="text-on-surface-variant shrink-0 mt-1"
                                        />
                                    </div>
                                </button>

                                {/* Expanded content */}
                                {isExpanded && (
                                    <div className="px-5 pb-5">
                                        <div className="prose prose-sm max-w-none text-on-surface-variant mb-4 [&_strong]:text-on-surface [&_h1]:font-headline [&_h2]:font-headline [&_h3]:font-headline">
                                            <ReactMarkdown>{material.markdownContent}</ReactMarkdown>
                                        </div>

                                        <div className="flex items-center justify-between flex-wrap gap-3 pt-3 border-t border-outline-variant">
                                            {/* Tags */}
                                            <div className="flex gap-2 flex-wrap">
                                                {material.tags.map((tag) => (
                                                    <span key={tag} className="text-xs text-on-surface-variant bg-surface-container px-2 py-0.5 rounded-full">
                                                        #{tag}
                                                    </span>
                                                ))}
                                            </div>

                                            {/* Related skill link */}
                                            {material.skillSlug && (
                                                <Link
                                                    href={`/skill/${material.skillSlug}`}
                                                    className="flex items-center gap-1 text-xs text-primary font-semibold hover:underline"
                                                >
                                                    <Icon name="school" size="sm" />
                                                    Связанный навык
                                                    <Icon name="arrow_forward" size="sm" />
                                                </Link>
                                            )}
                                        </div>
                                    </div>
                                )}
                            </div>
                        );
                    })}
                </div>
            )}
        </div>
    );
}
