"use client";

import { Icon } from "@/shared/components/icon";

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

const TONE_STYLES: Record<Tone, { bg: string; color: string; title: string; icon: "check" | "close" }> = {
    good: { bg: "var(--good-soft)", color: "var(--good)", title: "Верно!", icon: "check" },
    warn: { bg: "var(--warn-soft)", color: "var(--warn)", title: "Почти", icon: "close" },
    bad:  { bg: "var(--bad-soft)",  color: "var(--bad)",  title: "Не совсем", icon: "close" },
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
            style={{
                position: "fixed",
                bottom: 0,
                left: 0,
                right: 0,
                background: "var(--surface)",
                borderTop: "1px solid var(--line)",
                padding: "20px 32px",
                paddingBottom: "max(20px, env(safe-area-inset-bottom))",
            }}
        >
            <div
                style={{
                    display: "flex",
                    flexDirection: "column",
                    gap: detailed ? 14 : 0,
                    maxWidth: 820,
                    margin: "0 auto",
                    padding: detailed ? 18 : 16,
                    background: t.bg,
                    borderRadius: 14,
                    animation: "slideUp 0.3s cubic-bezier(0.5, 1.6, 0.4, 1)",
                }}
                className="slide-up"
            >
                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: 20,
                    }}
                >
                    <div style={{ display: "flex", gap: 14, alignItems: "center", minWidth: 0 }}>
                        <div
                            style={{
                                width: 42,
                                height: 42,
                                borderRadius: 12,
                                background: t.color,
                                color: "white",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                flexShrink: 0,
                            }}
                        >
                            <Icon name={t.icon} size={20} />
                        </div>
                        <div style={{ minWidth: 0 }}>
                            <div style={{ fontSize: 15, fontWeight: 600, color: t.color }}>
                                {t.title}
                            </div>
                            <div
                                style={{
                                    fontSize: 13,
                                    color: "var(--ink-2)",
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

                    <div style={{ display: "flex", alignItems: "center", gap: 14, flexShrink: 0 }}>
                        {ratingOutOfTen !== null && (
                            <div
                                aria-label={`Оценка ${ratingOutOfTen} из 10`}
                                style={{
                                    display: "inline-flex",
                                    alignItems: "baseline",
                                    gap: 2,
                                    padding: "6px 10px",
                                    background: "var(--surface)",
                                    border: `1px solid ${t.color}`,
                                    borderRadius: 10,
                                    fontFamily: "var(--f-mono)",
                                    color: t.color,
                                }}
                            >
                                <span style={{ fontSize: 16, fontWeight: 600 }}>{ratingOutOfTen}</span>
                                <span style={{ fontSize: 11, opacity: 0.7 }}>/10</span>
                            </div>
                        )}
                        {isCorrect && xpEarned > 0 && (
                            <span
                                style={{
                                    fontFamily: "var(--f-mono)",
                                    fontSize: 13,
                                    fontWeight: 500,
                                    color: t.color,
                                }}
                            >
                                +{xpEarned} XP
                            </span>
                        )}
                        <Button variant="primary" onClick={onContinue} iconRightName="arrow-right">
                            ПРОДОЛЖИТЬ
                        </Button>
                    </div>
                </div>

                {detailed && (
                    <div
                        style={{
                            background: "var(--surface)",
                            border: "1px solid var(--line)",
                            borderRadius: 12,
                            padding: "12px 14px",
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 8,
                                marginBottom: 6,
                            }}
                        >
                            <Icon name="sparkle" size="xs" style={{ color: t.color }} />
                            <span
                                style={{
                                    fontSize: 11,
                                    letterSpacing: 1,
                                    textTransform: "uppercase",
                                    color: "var(--ink-3)",
                                    fontWeight: 500,
                                }}
                            >
                                Разбор ответа
                            </span>
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
