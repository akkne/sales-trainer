"use client";

import { useDialogBundles, useDialogSessions } from "@/features/dialog/hooks/use-dialog";
import type { DialogBundle, DialogSessionSummary } from "@/features/dialog/hooks/use-dialog";
import { Icon } from "@/shared/components/icon";
import { Skeleton, ErrorState } from "@/shared/components";
import Link from "next/link";
import { useRouter } from "next/navigation";

// ── Avatar seeding ────────────────────────────────────────────────────────────
// 7-pair gradient palette matching DESIGN_SPEC §1.1
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

// ── Difficulty inference (no backend field — derived from bundle sort order) ─
type Difficulty = "easy" | "medium" | "hard";
function inferDifficulty(bundle: DialogBundle, index: number): Difficulty {
    // Use sortOrder or position as a proxy; cycle through easy/medium/hard
    const n = (bundle.sortOrder > 0 ? bundle.sortOrder - 1 : index) % 3;
    return (["easy", "medium", "hard"] as Difficulty[])[n];
}

function DifficultyBadge({ level }: { level: Difficulty }) {
    return <span className={`badge-${level}`}>{
        level === "easy" ? "Легко" : level === "medium" ? "Средне" : "Сложно"
    }</span>;
}

// ── Relative timestamp ────────────────────────────────────────────────────────
function relativeTime(iso: string): string {
    const diff = Date.now() - new Date(iso).getTime();
    const mins = Math.floor(diff / 60_000);
    if (mins < 1) return "только что";
    if (mins < 60) return `${mins} мин назад`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs} ч назад`;
    const days = Math.floor(hrs / 24);
    return `${days} дн назад`;
}

function sessionKind(session: DialogSessionSummary): string {
    // voiceEnabled is on the mode, not summary — infer from modeTitle heuristic
    const t = session.modeId.toLowerCase();
    return t.includes("voice") || t.includes("голос") || t.includes("call")
        ? "Голосовой звонок"
        : "Текстовый чат";
}

// ── NPC mentor static data ────────────────────────────────────────────────────
const NPC_MENTOR = {
    initials: "SS",
    name: "Skeptical Sam",
    blurb: "«Хочешь, позвоню и попробую разнести твою лучшую презентацию? Пять минут на подготовку.»",
    // no dedicated route yet — challenge goes to the first bundle's voice mode
};

// ─────────────────────────────────────────────────────────────────────────────

export default function DialogPage() {
    const { data: bundles, isLoading: bundlesLoading, error: bundlesError, refetch } = useDialogBundles();
    const { data: sessions } = useDialogSessions();
    const router = useRouter();

    // ── Loading skeleton ──────────────────────────────────────────────────────
    if (bundlesLoading) {
        return (
            <div className="page">
                <div className="container">
                    <div className="practice-header">
                        <Skeleton width={120} height={20} />
                        <Skeleton width={260} height={14} style={{ marginTop: 6 }} />
                    </div>
                    {/* mentor banner skeleton */}
                    <Skeleton height={120} rounded={18} style={{ marginBottom: 26 }} />
                    <Skeleton width={140} height={14} style={{ marginBottom: 12 }} />
                    <div className="bundle-grid">
                        {[1, 2, 3, 4].map((i) => (
                            <Skeleton key={i} height={200} rounded={14} />
                        ))}
                    </div>
                </div>
            </div>
        );
    }

    // ── Error state ───────────────────────────────────────────────────────────
    if (bundlesError) {
        return (
            <div className="page" style={{ padding: "60px 24px" }}>
                <ErrorState
                    title="Не удалось загрузить"
                    message={bundlesError.message}
                    onRetry={() => refetch()}
                />
            </div>
        );
    }

    // ── Empty / unconfigured ──────────────────────────────────────────────────
    if (!bundles || bundles.length === 0) {
        return (
            <div className="page container">
                <div className="empty" style={{ paddingTop: 120 }}>
                    <div className="ic">
                        <Icon name="message" size="lg" />
                    </div>
                    <h1 className="h3" style={{ marginBottom: 8 }}>Практика диалогов пока недоступна</h1>
                    <p className="small">Эта функция ещё в разработке или не настроена</p>
                </div>
            </div>
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    // Find the first bundle that has a voice-enabled mode for the mentor CTA
    // (we don't know which modes are voice here, so just navigate to the first bundle)
    const firstBundleId = bundles[0]?.id;

    const recentSessions = sessions?.slice(0, 5) ?? [];

    // ── Render ────────────────────────────────────────────────────────────────
    return (
        <div className="page">
            <div className="container">
                {/* ── Page header ── */}
                <div className="practice-header">
                    <h1 className="practice-title">Практика</h1>
                    <p className="practice-subtitle">
                        Интерактивные сценарии отработки техник продаж с ИИ-клиентом
                    </p>
                </div>

                {/* ── Featured mentor banner ── */}
                <div className="mentor-banner">
                    <div className="mentor-banner-glow" aria-hidden="true" />
                    <div className="mentor-banner-avatar" aria-hidden="true">
                        {NPC_MENTOR.initials}
                    </div>
                    <div className="mentor-banner-body">
                        <div className="mentor-banner-eyebrow">Рекомендуемый наставник</div>
                        <p className="mentor-banner-name">{NPC_MENTOR.name}</p>
                        <p className="mentor-banner-blurb">{NPC_MENTOR.blurb}</p>
                    </div>
                    <div className="mentor-banner-cta">
                        <button
                            className="mentor-banner-btn"
                            onClick={() => firstBundleId && router.push(`/dialog/${firstBundleId}`)}
                            aria-label="Начать голосовой звонок со Skeptical Sam"
                        >
                            <Icon name="mic" size={16} />
                            Начать звонок
                        </button>
                    </div>
                </div>

                {/* ── Dialog bundles ── */}
                <p className="practice-section-label">Диалоговые модули</p>
                <div className="bundle-grid" role="list">
                    {bundles.map((bundle, idx) => {
                        const { from, to } = ava(bundle.id);
                        const abbr = initials(bundle.title);
                        const difficulty = inferDifficulty(bundle, idx);

                        return (
                            <article key={bundle.id} className="bundle-card" role="listitem">
                                {/* top: icon + difficulty badge */}
                                <div className="bundle-card-top">
                                    <div
                                        className="bundle-icon-sq"
                                        style={{ background: `linear-gradient(135deg, ${from}, ${to})` }}
                                        aria-hidden="true"
                                    >
                                        {abbr}
                                    </div>
                                    <DifficultyBadge level={difficulty} />
                                </div>

                                {/* title + description */}
                                <h3 className="bundle-title">{bundle.title}</h3>
                                <p className="bundle-desc">{bundle.description}</p>

                                {/* skill pill + mode count */}
                                <div className="bundle-meta">
                                    {bundle.skillTitle && (
                                        <span className="bundle-skill-pill">
                                            {bundle.skillTitle}
                                        </span>
                                    )}
                                    <span className="bundle-modes-count">режимы →</span>
                                </div>

                                {/* footer: Chat + Call buttons */}
                                <div className="bundle-footer">
                                    <Link
                                        href={`/dialog/${bundle.id}`}
                                        className="bundle-btn-chat"
                                        aria-label={`Открыть текстовый чат: ${bundle.title}`}
                                        onClick={(e) => e.stopPropagation()}
                                    >
                                        <Icon name="message" size={15} />
                                        Чат
                                    </Link>
                                    <Link
                                        href={`/dialog/${bundle.id}`}
                                        className="bundle-btn-call"
                                        aria-label={`Открыть голосовой звонок: ${bundle.title}`}
                                        onClick={(e) => e.stopPropagation()}
                                    >
                                        <Icon name="phone" size={15} />
                                        Звонок
                                    </Link>
                                </div>
                            </article>
                        );
                    })}
                </div>

                {/* ── Recent sessions (only if data exists) ── */}
                {recentSessions.length > 0 && (
                    <div className="practice-sessions">
                        <p className="practice-section-label">Недавние сессии</p>
                        <div className="sessions-card" role="list">
                            {recentSessions.map((session) => {
                                const { from, to } = ava(session.bundleId);
                                const abbr = initials(session.bundleTitle);
                                const kind = sessionKind(session);
                                const ts = relativeTime(session.createdAt);
                                const msgCount = session.messageCount;

                                return (
                                    <div key={session.id} className="session-row" role="listitem">
                                        <div
                                            className="session-icon-sq"
                                            style={{ background: `linear-gradient(135deg, ${from}, ${to})` }}
                                            aria-hidden="true"
                                        >
                                            {abbr}
                                        </div>
                                        <div className="session-row-body">
                                            <p className="session-mode-title">{session.modeTitle}</p>
                                            <p className="session-meta">
                                                {session.bundleTitle}
                                                {msgCount > 0 && ` · ${msgCount} ${msgCount === 1 ? "сообщение" : "сообщений"}`}
                                                {` · ${kind}`}
                                            </p>
                                        </div>
                                        <span className="session-ts">{ts}</span>
                                        <Link
                                            href={`/dialog/${session.bundleId}`}
                                            className="session-open-link"
                                            aria-label={`Открыть сессию: ${session.modeTitle}`}
                                        >
                                            Открыть →
                                        </Link>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                )}

                <div style={{ height: 48 }} />
            </div>
        </div>
    );
}
