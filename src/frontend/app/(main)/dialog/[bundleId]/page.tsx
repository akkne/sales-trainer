"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useDialogBundles, useDialogModes } from "@/features/dialog/hooks/use-dialog";
import { Icon } from "@/shared/components/icon";
import { Skeleton } from "@/shared/components";
import { trackEvent } from "@/shared/analytics/track";

// ── Avatar seeding (mirrors dialog/page.tsx) ───────────────────────────────
const AVATAR_PALETTE: [string, string][] = [
    ["#6C5BD9", "#9B8CF0"],
    ["#4C8DF6", "#7FB0FA"],
    ["#E16BA0", "#F09BC2"],
    ["#2FB36F", "#73D6A0"],
    ["#F0863C", "#F7B07A"],
    ["#1E9FB0", "#6FCBD6"],
    ["#8A5BD9", "#B79BFF"],
];

function hashSeed(s: string): number {
    let h = 0;
    for (let i = 0; i < s.length; i++) h = (Math.imul(31, h) + s.charCodeAt(i)) | 0;
    return Math.abs(h);
}

function ava(seed: string): { from: string; to: string } {
    const [from, to] = AVATAR_PALETTE[hashSeed(seed) % AVATAR_PALETTE.length];
    return { from, to };
}

function initials(title: string): string {
    return title
        .split(/\s+/)
        .slice(0, 2)
        .map((w) => w[0]?.toUpperCase() ?? "")
        .join("");
}

// ─────────────────────────────────────────────────────────────────────────────

export default function BundleModesPage() {
    const params = useParams();
    const router = useRouter();
    const bundleId = params.bundleId as string;

    const { data: bundles } = useDialogBundles();
    const { data: modes, isLoading, error } = useDialogModes(bundleId);

    const currentBundle = bundles?.find((b) => b.id === bundleId);
    const bundleColors = ava(bundleId);
    const bundleAbbr = currentBundle ? initials(currentBundle.title) : "ДМ";

    // ── Loading ───────────────────────────────────────────────────────────────
    if (isLoading) {
        return (
            <div className="page">
                <div className="container">
                    <Skeleton width={120} height={14} style={{ margin: "22px 0 18px" }} />
                    <div className="mode-select-header">
                        <Skeleton width={56} height={56} rounded={12} />
                        <div style={{ flex: 1 }}>
                            <Skeleton width={80} height={11} style={{ marginBottom: 6 }} />
                            <Skeleton width={200} height={20} style={{ marginBottom: 8 }} />
                            <Skeleton width={320} height={14} />
                        </div>
                    </div>
                    <div className="mode-grid">
                        {[1, 2, 3].map((i) => (
                            <Skeleton key={i} height={160} rounded={14} />
                        ))}
                    </div>
                </div>
            </div>
        );
    }

    // ── Error ─────────────────────────────────────────────────────────────────
    if (error) {
        return (
            <div className="page container">
                <div className="empty" style={{ paddingTop: 100 }}>
                    <div className="ic" style={{ background: "var(--heart-soft)", color: "var(--heart)" }}>
                        <Icon name="close" size="lg" />
                    </div>
                    <h1 className="h3" style={{ marginBottom: 8 }}>Ошибка загрузки</h1>
                    <p className="small">{error.message}</p>
                </div>
            </div>
        );
    }

    // ── Render ────────────────────────────────────────────────────────────────
    return (
        <div className="page">
            <div className="container">
                {/* ── Back link ── */}
                <button
                    className="back-link"
                    onClick={() => router.push("/dialog")}
                    aria-label="Назад к практике"
                >
                    <Icon name="chevron-left" size={18} />
                    Назад
                </button>

                {/* ── Bundle header ── */}
                <div className="mode-select-header">
                    <div
                        className="mode-select-icon"
                        style={{
                            background: `linear-gradient(135deg, ${bundleColors.from}, ${bundleColors.to})`,
                        }}
                        aria-hidden="true"
                    >
                        {bundleAbbr}
                    </div>
                    <div>
                        <p className="mode-select-eyebrow">Выбери режим</p>
                        <h1 className="mode-select-title">
                            {currentBundle?.title ?? "Режимы практики"}
                        </h1>
                        {currentBundle?.description && (
                            <p className="mode-select-desc">{currentBundle.description}</p>
                        )}
                    </div>
                </div>

                {/* ── Modes grid ── */}
                {(!modes || modes.length === 0) ? (
                    <div className="empty">
                        <div className="ic">
                            <Icon name="message" size="lg" />
                        </div>
                        <p className="h4" style={{ marginBottom: 4 }}>Режимы пока не добавлены</p>
                        <p className="small">Администратор ещё не настроил сценарии</p>
                    </div>
                ) : (
                    <div className="mode-grid" role="list">
                        {modes.map((mode) => (
                            <div key={mode.id} className="mode-card" role="listitem">
                                <h3 className="mode-card-title">{mode.title}</h3>
                                <p className="mode-card-desc">{mode.description}</p>

                                <div className="mode-card-footer">
                                    <Link
                                        href={`/dialog/${bundleId}/${mode.id}`}
                                        className="bundle-btn-chat"
                                        style={{ flex: 1 }}
                                        onClick={() => trackEvent("start_dialog", "dialog")}
                                        aria-label={`Начать текстовый чат: ${mode.title}`}
                                    >
                                        <Icon name="message" size={15} />
                                        Чат
                                    </Link>
                                    {mode.voiceEnabled && (
                                        <Link
                                            href={`/dialog/${bundleId}/${mode.id}/voice`}
                                            className="bundle-btn-call"
                                            style={{ flex: 1 }}
                                            onClick={() => trackEvent("start_dialog", "dialog")}
                                            aria-label={`Начать голосовой звонок: ${mode.title}`}
                                        >
                                            <Icon name="phone" size={15} />
                                            Звонок
                                        </Link>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                )}

                <div style={{ height: 48 }} />
            </div>
        </div>
    );
}
