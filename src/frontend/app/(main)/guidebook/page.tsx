"use client";

import { useState, useDeferredValue } from "react";
import Link from "next/link";
import ReactMarkdown from "react-markdown";
import { useHandbook, useHandbookCategories } from "@/lib/hooks/useReference";

const CATEGORY_LABELS: Record<string, string> = {
    "objections": "Возражения",
    "cold-calls": "Холодные звонки",
    "closing": "Закрытие",
    "discovery": "Квалификация",
    "rapport": "Rapport",
    "negotiation": "Переговоры",
};

function categoryLabel(cat: string): string {
    return CATEGORY_LABELS[cat] ?? cat;
}

const CATEGORY_COLORS: Record<string, string> = {
    "objections": "bg-red-100 text-red-700",
    "cold-calls": "bg-blue-100 text-blue-700",
    "closing": "bg-green-100 text-green-700",
    "discovery": "bg-yellow-100 text-yellow-700",
    "rapport": "bg-purple-100 text-purple-700",
    "negotiation": "bg-orange-100 text-orange-700",
};

function categoryColor(cat: string | null): string {
    if (!cat) return "bg-gray-100 text-gray-600";
    return CATEGORY_COLORS[cat] ?? "bg-gray-100 text-gray-600";
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
        <div className="max-w-2xl mx-auto px-4 py-8">
            {/* Header */}
            <div className="mb-6">
                <h1 className="font-extrabold text-2xl text-gray-900 mb-1">📖 Справочник</h1>
                <p className="text-sm text-[#AFAFAF]">Ключевые техники продаж</p>
            </div>

            {/* Search */}
            <div className="relative mb-4">
                <span className="absolute left-3 top-1/2 -translate-y-1/2 text-[#AFAFAF] text-sm">🔍</span>
                <input
                    type="text"
                    placeholder="Поиск техник..."
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    className="w-full pl-9 pr-4 py-3 rounded-xl border-2 border-[#E5E5E5] focus:border-[#58CC02] outline-none text-sm font-medium transition-colors"
                />
                {searchInput && (
                    <button
                        onClick={() => setSearchInput("")}
                        className="absolute right-3 top-1/2 -translate-y-1/2 text-[#AFAFAF] hover:text-gray-600 text-lg leading-none"
                    >
                        ×
                    </button>
                )}
            </div>

            {/* Category filter chips */}
            {categories.length > 0 && (
                <div className="flex gap-2 flex-wrap mb-6">
                    <button
                        onClick={() => setSelectedCategory(null)}
                        className={`px-3 py-1.5 rounded-full text-xs font-bold border-2 transition-colors ${
                            selectedCategory === null
                                ? "bg-[#58CC02] text-white border-[#58CC02]"
                                : "bg-white text-gray-600 border-[#E5E5E5] hover:border-[#58CC02]"
                        }`}
                    >
                        Все
                    </button>
                    {categories.map((cat) => (
                        <button
                            key={cat}
                            onClick={() => setSelectedCategory(cat === selectedCategory ? null : cat)}
                            className={`px-3 py-1.5 rounded-full text-xs font-bold border-2 transition-colors ${
                                selectedCategory === cat
                                    ? "bg-[#58CC02] text-white border-[#58CC02]"
                                    : "bg-white text-gray-600 border-[#E5E5E5] hover:border-[#58CC02]"
                            }`}
                        >
                            {categoryLabel(cat)}
                        </button>
                    ))}
                </div>
            )}

            {/* Results */}
            {isLoading ? (
                <div className="flex justify-center py-12">
                    <div className="w-8 h-8 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
                </div>
            ) : materials.length === 0 ? (
                <div className="text-center py-16">
                    <p className="text-4xl mb-3">🔎</p>
                    <p className="font-semibold text-gray-700 mb-1">Ничего не найдено</p>
                    <p className="text-sm text-[#AFAFAF]">Попробуй другой запрос или категорию</p>
                </div>
            ) : (
                <div className="flex flex-col gap-3">
                    {materials.map((material) => {
                        const isExpanded = expandedId === material.materialId;
                        // First ~120 chars of content as excerpt
                        const excerpt = material.markdownContent.replace(/[#*_`]/g, "").slice(0, 120);

                        return (
                            <div
                                key={material.materialId}
                                className={`rounded-2xl border-2 overflow-hidden transition-all ${
                                    isExpanded
                                        ? "border-[#58CC02]"
                                        : "border-[#E5E5E5] hover:border-[#C3E89A]"
                                }`}
                            >
                                {/* Card header — always visible */}
                                <button
                                    onClick={() => toggleExpand(material.materialId)}
                                    className="w-full text-left px-4 py-4"
                                >
                                    <div className="flex items-start justify-between gap-2">
                                        <div className="flex-1 min-w-0">
                                            <div className="flex items-center gap-2 flex-wrap mb-1">
                                                {material.category && (
                                                    <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${categoryColor(material.category)}`}>
                                                        {categoryLabel(material.category)}
                                                    </span>
                                                )}
                                                {material.tags.map((tag) => (
                                                    <span key={tag} className="text-[10px] text-[#AFAFAF] bg-[#F7F7F7] px-1.5 py-0.5 rounded-full">
                                                        {tag}
                                                    </span>
                                                ))}
                                            </div>
                                            <p className="font-bold text-gray-900 text-sm leading-snug">
                                                {material.title}
                                            </p>
                                            {!isExpanded && (
                                                <p className="text-xs text-[#7A7A7A] mt-1 line-clamp-2">
                                                    {excerpt}{excerpt.length >= 120 ? "…" : ""}
                                                </p>
                                            )}
                                        </div>
                                        <span className={`text-[#AFAFAF] flex-shrink-0 mt-1 transition-transform duration-200 ${isExpanded ? "rotate-180" : ""}`}>
                                            ▼
                                        </span>
                                    </div>
                                </button>

                                {/* Expanded content */}
                                {isExpanded && (
                                    <div className="px-4 pb-4">
                                        <div className="prose prose-sm max-w-none text-gray-700 mb-3">
                                            <ReactMarkdown>{material.markdownContent}</ReactMarkdown>
                                        </div>
                                        <Link
                                            href={`/skill/${material.skillSlug}`}
                                            className="inline-flex items-center gap-1 text-xs text-[#58CC02] font-semibold hover:underline"
                                        >
                                            📚 Связанный навык →
                                        </Link>
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
