"use client";

import { useDialogBundles } from "@/lib/hooks/useDialog";
import { BundleCard } from "@/components/dialog/BundleCard";
import { Icon } from "@/components/ui/Icon";
import { StatTile } from "@/components/ui/StatTile";
import { GeoAvatar } from "@/components/ui/GeoAvatar";
import { Button } from "@/components/ui/Button";
import Link from "next/link";

export default function DialogPage() {
    const { data: bundles, isLoading, error } = useDialogBundles();

    if (isLoading) {
        return (
            <div style={{ minHeight: "100vh", background: "var(--bg)", display: "flex", alignItems: "center", justifyContent: "center" }}>
                <div style={{ width: 40, height: 40, borderRadius: "50%", border: "4px solid var(--indigo)", borderTopColor: "transparent", animation: "spin 0.8s linear infinite" }} />
            </div>
        );
    }

    if (error) {
        return (
            <div style={{ minHeight: "100vh", background: "var(--bg)", padding: "80px 60px" }}>
                <div style={{ maxWidth: 600, margin: "0 auto", textAlign: "center" }}>
                    <div style={{ width: 80, height: 80, borderRadius: "50%", background: "var(--bad-soft)", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 24px" }}>
                        <Icon name="close" size="lg" color="var(--bad)" />
                    </div>
                    <h1 style={{ fontSize: 28, fontWeight: 500, marginBottom: 8 }}>Ошибка загрузки</h1>
                    <p style={{ color: "var(--ink-3)" }}>{error.message}</p>
                </div>
            </div>
        );
    }

    if (!bundles || bundles.length === 0) {
        return (
            <div style={{ minHeight: "100vh", background: "var(--bg)", padding: "80px 60px" }}>
                <div style={{ maxWidth: 600, margin: "0 auto", textAlign: "center" }}>
                    <div style={{ width: 80, height: 80, borderRadius: "50%", background: "var(--surface)", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 24px" }}>
                        <Icon name="message" size="lg" color="var(--ink-3)" />
                    </div>
                    <h1 style={{ fontSize: 28, fontWeight: 500, marginBottom: 8 }}>Практика диалогов пока недоступна</h1>
                    <p style={{ color: "var(--ink-3)" }}>Функция находится в разработке или не настроена</p>
                </div>
            </div>
        );
    }

    return (
        <div style={{ minHeight: "100vh", background: "var(--bg)" }}>
            {/* Hero header */}
            <div style={{ padding: "40px 60px 32px", borderBottom: "1px solid var(--line)", background: "var(--surface-2)" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-end", gap: 32, maxWidth: 1200, margin: "0 auto" }}>
                    <div>
                        <div style={{ fontSize: 12, color: "var(--indigo)", letterSpacing: 2, textTransform: "uppercase", fontWeight: 500, marginBottom: 10, fontFamily: "var(--f-mono)" }}>
                            AI ДИАЛОГ · {bundles.length} МОДУЛЕЙ
                        </div>
                        <h1 style={{ margin: 0, fontSize: 48, letterSpacing: -1.5, fontWeight: 500, lineHeight: 1 }}>
                            Мастерство разговора.
                        </h1>
                        <p style={{ fontSize: 16, color: "var(--ink-3)", marginTop: 10, maxWidth: 520 }}>
                            Интерактивные сценарии для отработки техник продаж. AI-клиент реагирует как настоящий.
                        </p>
                    </div>
                    <div style={{ display: "flex", gap: 8 }}>
                        <StatTile big label="Диалогов" value="—" icon={<Icon name="message" size="xs" />} tone="indigo" />
                        <StatTile big label="Средний балл" value="—" unit="/10" icon={<Icon name="star" size="xs" />} tone="rust" />
                    </div>
                </div>
            </div>

            {/* Bundles grid */}
            <div style={{ padding: "32px 60px", maxWidth: 1200, margin: "0 auto" }}>
                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", gap: 16 }}>
                    {bundles.map((bundle) => (
                        <Link key={bundle.id} href={`/dialog/${bundle.id}`} style={{ textDecoration: "none" }}>
                            <div
                                style={{
                                    background: "var(--surface)",
                                    border: "1px solid var(--line)",
                                    borderRadius: 20,
                                    padding: 24,
                                    cursor: "pointer",
                                    transition: "all 0.2s",
                                    boxShadow: "var(--sh-1)",
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.boxShadow = "var(--sh-2)";
                                    e.currentTarget.style.borderColor = "var(--indigo)";
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.boxShadow = "var(--sh-1)";
                                    e.currentTarget.style.borderColor = "var(--line)";
                                }}
                            >
                                <div style={{ display: "flex", alignItems: "flex-start", gap: 16 }}>
                                    <div
                                        style={{
                                            width: 56,
                                            height: 56,
                                            borderRadius: 14,
                                            background: "var(--indigo-soft)",
                                            display: "flex",
                                            alignItems: "center",
                                            justifyContent: "center",
                                            fontSize: 28,
                                            flexShrink: 0,
                                        }}
                                    >
                                        {bundle.iconEmoji || "💬"}
                                    </div>
                                    <div style={{ flex: 1 }}>
                                        <div style={{ fontSize: 18, fontWeight: 500, marginBottom: 4 }}>{bundle.title}</div>
                                        <div style={{ fontSize: 13, color: "var(--ink-3)", lineHeight: 1.4 }}>
                                            {bundle.description}
                                        </div>
                                    </div>
                                </div>
                                <div style={{ marginTop: 16, display: "flex", alignItems: "center", justifyContent: "flex-end" }}>
                                    <Icon name="arrow-right" size="sm" color="var(--indigo)" />
                                </div>
                            </div>
                        </Link>
                    ))}
                </div>
            </div>

            {/* NPC Mentor card */}
            <div style={{ padding: "0 60px 80px", maxWidth: 1200, margin: "0 auto" }}>
                <div
                    style={{
                        background: "var(--ink)",
                        color: "var(--bg)",
                        borderRadius: 20,
                        padding: 32,
                        display: "flex",
                        gap: 32,
                        alignItems: "center",
                        position: "relative",
                        overflow: "hidden",
                    }}
                >
                    <div style={{ position: "absolute", top: -40, right: -40, width: 200, height: 200, borderRadius: "50%", background: "var(--rust)", opacity: 0.2 }} />

                    <div style={{ display: "flex", gap: 16, alignItems: "center", position: "relative" }}>
                        <GeoAvatar seed="sergey" size={80} />
                        <div>
                            <div style={{ fontSize: 24, fontWeight: 500 }}>Skeptic Sergey</div>
                            <div style={{ fontSize: 12, fontFamily: "var(--f-mono)", color: "var(--ink-4)", textTransform: "uppercase", letterSpacing: 1 }}>
                                VP · возражения
                            </div>
                        </div>
                    </div>

                    <div style={{ flex: 1, position: "relative" }}>
                        <p style={{ fontSize: 15, color: "var(--ink-2)", lineHeight: 1.5, marginBottom: 16 }}>
                            «Хочешь, позвоню и попробую развалить твою лучшую продажу? Пять минут на подготовку.»
                        </p>
                        <Button variant="accent" size="lg" iconRightName="phone">
                            CHALLENGE
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
}
