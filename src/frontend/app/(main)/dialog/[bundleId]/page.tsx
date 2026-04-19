"use client";

import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useDialogBundles, useDialogModes } from "@/lib/hooks/useDialog";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";

export default function BundleModesPage() {
    const params = useParams();
    const router = useRouter();
    const bundleId = params.bundleId as string;

    const { data: bundles } = useDialogBundles();
    const { data: modes, isLoading, error } = useDialogModes(bundleId);

    const currentBundle = bundles?.find((bundle) => bundle.id === bundleId);

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

    return (
        <div style={{ minHeight: "100vh", background: "var(--bg)" }}>
            {/* Header */}
            <div style={{ padding: "40px 60px 32px", borderBottom: "1px solid var(--line)", background: "var(--surface-2)" }}>
                <div style={{ maxWidth: 1200, margin: "0 auto" }}>
                    <button
                        onClick={() => router.push("/dialog")}
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 8,
                            background: "transparent",
                            border: "none",
                            cursor: "pointer",
                            color: "var(--ink-3)",
                            fontSize: 13,
                            marginBottom: 20,
                            padding: 0,
                        }}
                    >
                        <Icon name="chevron-left" size="sm" />
                        Назад к диалогам
                    </button>

                    <div style={{ display: "flex", alignItems: "center", gap: 20 }}>
                        {currentBundle && (
                            <div
                                style={{
                                    width: 72,
                                    height: 72,
                                    borderRadius: 18,
                                    background: "var(--indigo-soft)",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    fontSize: 36,
                                }}
                            >
                                {currentBundle.iconEmoji || "💬"}
                            </div>
                        )}
                        <div>
                            <div style={{ fontSize: 12, color: "var(--indigo)", letterSpacing: 2, textTransform: "uppercase", fontWeight: 500, marginBottom: 6, fontFamily: "var(--f-mono)" }}>
                                ВЫБЕРИ РЕЖИМ
                            </div>
                            <h1 style={{ margin: 0, fontSize: 36, letterSpacing: -1, fontWeight: 500, lineHeight: 1 }}>
                                {currentBundle?.title || "Режимы практики"}
                            </h1>
                            {currentBundle?.description && (
                                <p style={{ fontSize: 15, color: "var(--ink-3)", marginTop: 8 }}>
                                    {currentBundle.description}
                                </p>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            {/* Modes grid */}
            <div style={{ padding: "32px 60px 80px", maxWidth: 1200, margin: "0 auto" }}>
                {(!modes || modes.length === 0) ? (
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
                            <Icon name="message" size="lg" color="var(--ink-3)" />
                        </div>
                        <p style={{ fontWeight: 600, marginBottom: 4 }}>Режимы пока не добавлены</p>
                        <p style={{ fontSize: 14, color: "var(--ink-3)" }}>Администратор ещё не настроил сценарии</p>
                    </div>
                ) : (
                    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(300px, 1fr))", gap: 16 }}>
                        {modes.map((mode) => (
                            <Link
                                key={mode.id}
                                href={`/dialog/${bundleId}/${mode.id}`}
                                style={{ textDecoration: "none" }}
                            >
                                <div
                                    style={{
                                        background: "var(--surface)",
                                        border: "1px solid var(--line)",
                                        borderRadius: 16,
                                        padding: 20,
                                        cursor: "pointer",
                                        transition: "all 0.2s",
                                        boxShadow: "var(--sh-1)",
                                        height: "100%",
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
                                    <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 12 }}>
                                        <div>
                                            <div style={{ fontSize: 17, fontWeight: 500, marginBottom: 6 }}>{mode.title}</div>
                                            <div style={{ fontSize: 13, color: "var(--ink-3)", lineHeight: 1.4 }}>
                                                {mode.description}
                                            </div>
                                        </div>
                                        <Icon name="arrow-right" size="sm" color="var(--indigo)" style={{ flexShrink: 0, marginTop: 4 }} />
                                    </div>

                                    {mode.voiceEnabled && (
                                        <div
                                            style={{
                                                marginTop: 12,
                                                display: "inline-flex",
                                                alignItems: "center",
                                                gap: 6,
                                                padding: "4px 10px",
                                                borderRadius: 999,
                                                background: "var(--rust-soft)",
                                                color: "var(--rust)",
                                                fontSize: 11,
                                                fontWeight: 500,
                                            }}
                                        >
                                            <Icon name="mic" size="xs" />
                                            Voice
                                        </div>
                                    )}
                                </div>
                            </Link>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}
