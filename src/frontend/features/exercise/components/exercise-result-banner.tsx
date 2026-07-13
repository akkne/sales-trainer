"use client";

import { useRef, useState, useEffect } from "react";
import { Icon, IconName } from "@/shared/components/icon";

interface ExerciseResultBannerProps {
    isCorrect: boolean;
    score: number;
    explanation: string | null;
    aiFeedback: string | null;
    xpEarned: number;
    onContinue: () => void;
}

type Tone = "good" | "warn" | "bad";

function pickTone(isCorrect: boolean, score: number): Tone {
    if (isCorrect) return "good";
    if (score >= 40) return "warn";
    return "bad";
}

const TONE_STYLES: Record<
    Tone,
    { footClass: string; tileBg: string; tileColor: string; title: string; icon: IconName; btnClass: string }
> = {
    good: { footClass: " ok",   tileBg: "var(--success-soft)", tileColor: "var(--success)", title: "Верно!",   icon: "check",   btnClass: "btn-success" },
    warn: { footClass: " warn", tileBg: "var(--amber-soft)",   tileColor: "var(--amber)",   title: "Почти",     icon: "warning", btnClass: "btn-danger"  },
    bad:  { footClass: " bad",  tileBg: "var(--heart-soft)",   tileColor: "var(--heart)",   title: "Не совсем",  icon: "warning", btnClass: "btn-danger"  },
};

export function ExerciseResultBanner({
    isCorrect,
    score,
    explanation,
    aiFeedback,
    xpEarned,
    onContinue,
}: ExerciseResultBannerProps) {
    const tone = pickTone(isCorrect, score);
    const t = TONE_STYLES[tone];

    const feedback = (aiFeedback ?? "").trim();
    const fallback = isCorrect
        ? "Отличный ответ."
        : "Попробуй не подсказывать решение — дай клиенту раскрыться самому.";
    const detailed = feedback.length > 0;
    const ratingOutOfTen = detailed ? Math.max(0, Math.min(10, Math.round(score / 10))) : null;
    const compactSubtitle = explanation ?? fallback;
    const scrollerRef = useRef<HTMLDivElement | null>(null);
    const [isScrollable, setIsScrollable] = useState(false);
    useEffect(() => {
        if (!detailed) return;
        const el = scrollerRef.current;
        if (!el) return;
        setIsScrollable(el.scrollHeight > el.clientHeight + 1);
    }, [detailed, feedback]);

    return (
        <div
            className={"session-foot" + t.footClass}
            style={{
                position: "fixed",
                bottom: 0,
                left: 0,
                right: 0,
                paddingBottom: "max(18px, env(safe-area-inset-bottom))",
            }}
        >
            <div
                className="session-foot-inner"
                style={{ flexDirection: "column", gap: detailed ? 12 : 0 }}
            >
                {/* Main row: icon+title left, score+XP+continue right */}
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12, width: "100%" }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 12, minWidth: 0 }}>
                        <span
                            style={{
                                width: 42, height: 42, borderRadius: 12,
                                background: t.tileBg, color: t.tileColor,
                                display: "grid", placeItems: "center", flex: "none",
                            }}
                        >
                            <Icon name={t.icon} size={20} />
                        </span>
                        <div style={{ minWidth: 0 }}>
                            <div style={{ fontSize: 15, fontWeight: 700, color: "var(--ink-heading)" }}>{t.title}</div>
                            <div
                                style={{
                                    marginTop: 2, fontSize: 13, color: "var(--ink-3)",
                                    maxWidth: 480,
                                    overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
                                }}
                            >
                                {detailed ? "Разбор от AI ↓" : compactSubtitle}
                            </div>
                        </div>
                    </div>

                    <div style={{ display: "flex", alignItems: "center", gap: 10, flex: "none" }}>
                        {ratingOutOfTen !== null && (
                            <div
                                aria-label={`Оценка ${ratingOutOfTen} из 10`}
                                style={{
                                    display: "inline-flex", alignItems: "baseline", gap: 2,
                                    padding: "5px 10px",
                                    background: "var(--surface)",
                                    border: `1.5px solid ${t.tileColor}`,
                                    borderRadius: 9,
                                    color: t.tileColor,
                                }}
                            >
                                <span style={{ fontSize: 15, fontWeight: 700 }}>{ratingOutOfTen}</span>
                                <span style={{ fontSize: 11, opacity: 0.65 }}>/10</span>
                            </div>
                        )}
                        {isCorrect && xpEarned > 0 && (
                            <span
                                style={{
                                    fontSize: 13, fontWeight: 600,
                                    color: t.tileColor,
                                    padding: "4px 9px",
                                    background: t.tileBg,
                                    borderRadius: 8,
                                }}
                            >
                                +{xpEarned} XP
                            </span>
                        )}
                        <button className={"btn btn-lg " + t.btnClass} onClick={onContinue}>
                            Далее
                            <Icon name="arrow-right" size={17} />
                        </button>
                    </div>
                </div>

                {/* AI feedback card */}
                {detailed && (
                    <div
                        style={{
                            width: "100%",
                            background: "var(--surface)",
                            border: `1px solid ${t.tileColor}`,
                            borderRadius: 12,
                            padding: "12px 16px",
                        }}
                    >
                        <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 6 }}>
                            <Icon name="sparkle" size="xs" style={{ color: t.tileColor }} />
                            <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: "0.04em", textTransform: "uppercase", color: t.tileColor }}>
                                Разбор ответа
                            </span>
                        </div>
                        <div
                            ref={scrollerRef}
                            style={{
                                fontSize: 13, lineHeight: 1.55, color: "var(--ink-2)",
                                whiteSpace: "pre-wrap", maxHeight: 140, overflowY: "auto",
                                WebkitMaskImage: isScrollable
                                    ? "linear-gradient(to bottom, black calc(100% - 24px), transparent 100%)"
                                    : undefined,
                                maskImage: isScrollable
                                    ? "linear-gradient(to bottom, black calc(100% - 24px), transparent 100%)"
                                    : undefined,
                            }}
                        >
                            {feedback}
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
