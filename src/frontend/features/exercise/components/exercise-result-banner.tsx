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
    good: { footClass: " ok", tileBg: "var(--success-soft)", tileColor: "var(--success)", title: "Верно!", icon: "check", btnClass: "btn-success" },
    warn: { footClass: " bad", tileBg: "var(--amber-soft)", tileColor: "var(--amber)", title: "Почти", icon: "warning", btnClass: "btn-danger" },
    bad: { footClass: " bad", tileBg: "var(--heart-soft)", tileColor: "var(--heart)", title: "Не совсем", icon: "warning", btnClass: "btn-danger" },
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
        : "Попробуйте без намёка на решение — пусть клиент откроется.";
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
                className="container session-foot-inner"
                style={{ flexDirection: "column", gap: detailed ? 14 : 0, width: "100%" }}
            >
                <div className="row between wrap gap-4 grow">
                    <div className="row gap-3" style={{ minWidth: 0 }}>
                        <span
                            className="itile"
                            style={{ width: 44, height: 44, background: t.tileBg, color: t.tileColor, flex: "none" }}
                        >
                            <Icon name={t.icon} size={22} />
                        </span>
                        <div style={{ minWidth: 0 }}>
                            <div className="h4">{t.title}</div>
                            <div
                                className="small"
                                style={{
                                    marginTop: 2,
                                    maxWidth: 520,
                                    overflow: "hidden",
                                    textOverflow: "ellipsis",
                                    whiteSpace: "nowrap",
                                }}
                            >
                                {detailed ? "Оценка AI" : compactSubtitle}
                            </div>
                        </div>
                    </div>

                    <div className="row gap-3" style={{ flex: "none" }}>
                        {ratingOutOfTen !== null && (
                            <div
                                aria-label={`Оценка ${ratingOutOfTen} из 10`}
                                style={{
                                    display: "inline-flex",
                                    alignItems: "baseline",
                                    gap: 2,
                                    padding: "6px 10px",
                                    background: "var(--surface)",
                                    border: `1px solid ${t.tileColor}`,
                                    borderRadius: 10,
                                    fontFamily: "var(--font-mono)",
                                    color: t.tileColor,
                                }}
                            >
                                <span style={{ fontSize: 16, fontWeight: 600 }}>{ratingOutOfTen}</span>
                                <span style={{ fontSize: 11, opacity: 0.7 }}>/10</span>
                            </div>
                        )}
                        {isCorrect && xpEarned > 0 && (
                            <span
                                style={{
                                    fontFamily: "var(--font-mono)",
                                    fontSize: 13,
                                    fontWeight: 500,
                                    color: t.tileColor,
                                }}
                            >
                                +{xpEarned} XP
                            </span>
                        )}
                        <button className={"btn btn-lg " + t.btnClass} onClick={onContinue}>
                            Дальше
                            <Icon name="arrow-right" size={18} />
                        </button>
                    </div>
                </div>

                {detailed && (
                    <div
                        className="card card-pad"
                        style={{ width: "100%" }}
                    >
                        <div className="row gap-2" style={{ marginBottom: 6 }}>
                            <Icon name="sparkle" size="xs" style={{ color: t.tileColor }} />
                            <span className="eyebrow">Разбор ответа</span>
                        </div>
                        <div
                            ref={scrollerRef}
                            style={{
                                fontSize: 13,
                                lineHeight: 1.55,
                                color: "var(--ink-2)",
                                whiteSpace: "pre-wrap",
                                maxHeight: 160,
                                overflowY: "auto",
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
