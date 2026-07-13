"use client";

import { use, useState } from "react";
import { useRouter } from "next/navigation";
import ReactMarkdown from "react-markdown";
import { Icon } from "@/shared/components/icon";
import { Skeleton } from "@/shared/components/skeleton";
import { useReferenceMaterials } from "@/features/skills/hooks/use-reference";

interface ReferencePageProps {
    params: Promise<{ id: string }>;
}

export default function ReferencePage({ params }: ReferencePageProps) {
    const { id: skillSlug } = use(params);
    const router = useRouter();
    const { data: referenceMaterials, isLoading } = useReferenceMaterials(skillSlug);
    const [expandedMaterialId, setExpandedMaterialId] = useState<string | null>(null);

    return (
        <div className="page">
            <div className="container" style={{ maxWidth: 760 }}>
                {/* Back navigation */}
                <button
                    onClick={() => router.back()}
                    style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 6,
                        background: "none",
                        border: "none",
                        cursor: "pointer",
                        color: "var(--ink-4)",
                        fontSize: 13,
                        fontWeight: 600,
                        fontFamily: "var(--font-ui)",
                        padding: "0 0 20px",
                        transition: "color var(--transition)",
                    }}
                    onMouseEnter={(e) => (e.currentTarget.style.color = "var(--ink)")}
                    onMouseLeave={(e) => (e.currentTarget.style.color = "var(--ink-4)")}
                >
                    <Icon name="chevron-left" size="sm" />
                    Назад
                </button>

                {/* Page title */}
                <h1 className="ref-title" style={{ marginBottom: 20 }}>
                    Техники
                </h1>

                {/* Content */}
                {isLoading ? (
                    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                        {[1, 2, 3].map((i) => (
                            <Skeleton key={i} height={64} rounded={15} />
                        ))}
                    </div>
                ) : !referenceMaterials?.length ? (
                    <div className="ref-empty">
                        <Icon name="search" size="lg" color="var(--ink-4)" />
                        <p>Материалы пока не добавлены</p>
                    </div>
                ) : (
                    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                        {referenceMaterials.map((material) => {
                            const isExpanded = expandedMaterialId === material.materialId;
                            return (
                                <div
                                    key={material.materialId}
                                    className="card"
                                    style={{
                                        borderRadius: 15,
                                        overflow: "hidden",
                                        border: isExpanded
                                            ? "1px solid var(--primary-tint-border)"
                                            : "1px solid var(--line)",
                                        boxShadow: isExpanded
                                            ? "0 6px 20px rgba(108,91,217,.10)"
                                            : "var(--sh-1)",
                                        transition: "border-color var(--transition), box-shadow var(--transition)",
                                    }}
                                >
                                    <button
                                        onClick={() =>
                                            setExpandedMaterialId(isExpanded ? null : material.materialId)
                                        }
                                        style={{
                                            width: "100%",
                                            display: "flex",
                                            alignItems: "center",
                                            justifyContent: "space-between",
                                            gap: 16,
                                            padding: "16px 20px",
                                            background: "none",
                                            border: "none",
                                            cursor: "pointer",
                                            textAlign: "left",
                                            fontFamily: "var(--font-ui)",
                                        }}
                                        aria-expanded={isExpanded}
                                    >
                                        <span
                                            style={{
                                                fontSize: 15,
                                                fontWeight: 700,
                                                color: "var(--ink-heading)",
                                                letterSpacing: "-0.01em",
                                            }}
                                        >
                                            {material.title}
                                        </span>
                                        <span
                                            style={{
                                                color: isExpanded ? "var(--primary)" : "var(--ink-4)",
                                                flexShrink: 0,
                                                display: "flex",
                                                alignItems: "center",
                                                transform: isExpanded ? "rotate(180deg)" : "rotate(0deg)",
                                                transition: "transform var(--transition), color var(--transition)",
                                            }}
                                        >
                                            <Icon name="chevron-down" size="sm" />
                                        </span>
                                    </button>

                                    {isExpanded && (
                                        <div
                                            style={{
                                                padding: "0 20px 20px",
                                                fontSize: 13.5,
                                                color: "var(--ink-2)",
                                                lineHeight: 1.6,
                                                borderTop: "1px solid var(--line-2)",
                                                paddingTop: 16,
                                            }}
                                        >
                                            <ReactMarkdown>{material.markdownContent}</ReactMarkdown>
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
