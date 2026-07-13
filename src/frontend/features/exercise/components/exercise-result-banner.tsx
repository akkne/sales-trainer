"use client";

import { Icon, IconName } from "@/shared/components/icon";

interface ExerciseResultBannerProps {
    isCorrect: boolean;
    score: number;
    explanation: string | null;
    aiFeedback: string | null;
    xpEarned: number;
    onContinue: () => void;
    /** The answer the user actually gave (shown in red when incorrect). */
    userAnswer?: string | null;
    /** The real correct answer (shown in green when the user was incorrect). */
    correctAnswer?: string | null;
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
    userAnswer,
    correctAnswer,
}: ExerciseResultBannerProps) {
    const tone = pickTone(isCorrect, score);
    const t = TONE_STYLES[tone];

    const feedback = (aiFeedback ?? "").trim();
    const explanationText = (explanation ?? "").trim();
    const detailed = feedback.length > 0;
    // The main review text: AI feedback if present, otherwise the static explanation.
    const reviewText = detailed ? feedback : explanationText;
    const hasReview = reviewText.length > 0;

    const fallback = isCorrect
        ? "Отличный ответ."
        : "Попробуй не подсказывать решение — дай клиенту раскрыться самому.";

    const ratingOutOfTen = detailed ? Math.max(0, Math.min(10, Math.round(score / 10))) : null;

    // Show the answer comparison only when the user got it wrong and we know the answers.
    const trimmedUser = (userAnswer ?? "").trim();
    const trimmedCorrect = (correctAnswer ?? "").trim();
    const showAnswers = !isCorrect && trimmedCorrect.length > 0;

    const hasCard = hasReview || showAnswers;

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
                style={{ flexDirection: "column", gap: hasCard ? 12 : 0 }}
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
                            <div style={{ marginTop: 2, fontSize: 13, color: "var(--ink-3)" }}>
                                {hasCard ? "Разбор ниже ↓" : fallback}
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

                {/* Review card: answer comparison + full explanation */}
                {hasCard && (
                    <div
                        style={{
                            width: "100%",
                            background: "var(--surface)",
                            border: `1px solid ${t.tileColor}`,
                            borderRadius: 12,
                            padding: "12px 16px",
                            maxHeight: "42vh",
                            overflowY: "auto",
                        }}
                    >
                        {/* Wrong answer (red) / correct answer (green) */}
                        {showAnswers && (
                            <div style={{ display: "flex", flexDirection: "column", gap: 8, marginBottom: hasReview ? 12 : 0 }}>
                                {trimmedUser.length > 0 && (
                                    <div style={{ display: "flex", alignItems: "flex-start", gap: 8 }}>
                                        <Icon name="close" size={16} style={{ color: "var(--heart)", flex: "none", marginTop: 2 }} />
                                        <div style={{ minWidth: 0 }}>
                                            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: "0.04em", textTransform: "uppercase", color: "var(--heart)" }}>
                                                Твой ответ
                                            </div>
                                            <div style={{ fontSize: 13, lineHeight: 1.5, color: "var(--heart)", whiteSpace: "pre-wrap", wordBreak: "break-word" }}>
                                                {trimmedUser}
                                            </div>
                                        </div>
                                    </div>
                                )}
                                <div style={{ display: "flex", alignItems: "flex-start", gap: 8 }}>
                                    <Icon name="check" size={16} style={{ color: "var(--success)", flex: "none", marginTop: 2 }} />
                                    <div style={{ minWidth: 0 }}>
                                        <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: "0.04em", textTransform: "uppercase", color: "var(--success)" }}>
                                            Правильный ответ
                                        </div>
                                        <div style={{ fontSize: 13, lineHeight: 1.5, color: "var(--success)", whiteSpace: "pre-wrap", wordBreak: "break-word" }}>
                                            {trimmedCorrect}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* Full explanation / AI review text (never truncated) */}
                        {hasReview && (
                            <>
                                <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 6 }}>
                                    <Icon name="sparkle" size="xs" style={{ color: t.tileColor }} />
                                    <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: "0.04em", textTransform: "uppercase", color: t.tileColor }}>
                                        Разбор ответа
                                    </span>
                                </div>
                                <div
                                    style={{
                                        fontSize: 13, lineHeight: 1.55, color: "var(--ink-2)",
                                        whiteSpace: "pre-wrap", wordBreak: "break-word",
                                    }}
                                >
                                    {reviewText}
                                </div>
                            </>
                        )}
                    </div>
                )}
            </div>
        </div>
    );
}
