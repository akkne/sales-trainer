"use client";

import { use, useState } from "react";
import { useRouter } from "next/navigation";
import ReactMarkdown from "react-markdown";
import { useReferenceMaterials } from "@/features/skills/hooks/use-reference";

interface ReferencePageProps {
    params: Promise<{ id: string }>;
}

export default function ReferencePage({ params }: ReferencePageProps) {
    const { id: skillSlug } = use(params);
    const router = useRouter();
    const { data: referenceMaterials, isLoading } = useReferenceMaterials(skillSlug);
    const [expandedMaterialId, setExpandedMaterialId] = useState<string | null>(
        null
    );

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-ink border-t-transparent animate-spin" />
            </div>
        );
    }

    return (
        <div className="max-w-4xl mx-auto px-4 py-8">
            <button
                onClick={() => router.back()}
                className="text-ink-4 hover:text-ink text-sm mb-6 inline-block"
            >
                ← Назад
            </button>

            <h1 className="text-2xl font-bold text-ink mb-6">
                Техники
            </h1>

            {!referenceMaterials?.length && (
                <p className="text-ink-4">Материалы пока не добавлены</p>
            )}

            <div className="flex flex-col gap-3">
                {referenceMaterials?.map((referenceMaterial) => {
                    const isExpanded = expandedMaterialId === referenceMaterial.materialId;

                    return (
                        <div
                            key={referenceMaterial.materialId}
                            className="rounded-2xl bg-surface border border-line overflow-hidden"
                        >
                            <button
                                onClick={() =>
                                    setExpandedMaterialId(
                                        isExpanded ? null : referenceMaterial.materialId
                                    )
                                }
                                className="w-full flex items-center justify-between px-5 py-4 text-left"
                            >
                                <span className="font-semibold text-ink">
                                    {referenceMaterial.title}
                                </span>
                                <span
                                    className={`text-ink-4 transition-transform duration-200 ${
                                        isExpanded ? "rotate-180" : ""
                                    }`}
                                >
                                    ▼
                                </span>
                            </button>

                            {isExpanded && (
                                <div className="px-5 pb-5 prose prose-sm max-w-none text-ink-2">
                                    <ReactMarkdown>{referenceMaterial.markdownContent}</ReactMarkdown>
                                </div>
                            )}
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
