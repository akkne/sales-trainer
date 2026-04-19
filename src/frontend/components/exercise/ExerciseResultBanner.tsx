"use client";

import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";

interface ExerciseResultBannerProps {
    isCorrect: boolean;
    score: number;
    explanation: string | null;
    aiFeedback: string | null;
    xpEarned: number;
    onContinue: () => void;
}

export function ExerciseResultBanner({
    isCorrect,
    explanation,
    aiFeedback,
    xpEarned,
    onContinue,
}: ExerciseResultBannerProps) {
    const toneStyles = isCorrect
        ? { bg: "var(--good-soft)", color: "var(--good)", title: "Верно!", sub: explanation ?? aiFeedback ?? "Отличный ответ." }
        : { bg: "var(--bad-soft)", color: "var(--bad)", title: "Не совсем", sub: explanation ?? aiFeedback ?? "Попробуйте без намёка на решение — пусть клиент откроется." };

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
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: 20,
                    maxWidth: 820,
                    margin: "0 auto",
                    padding: 16,
                    background: toneStyles.bg,
                    borderRadius: 14,
                    animation: "slideUp 0.3s cubic-bezier(0.5, 1.6, 0.4, 1)",
                }}
                className="slide-up"
            >
                <div style={{ display: "flex", gap: 14, alignItems: "center" }}>
                    <div
                        style={{
                            width: 42,
                            height: 42,
                            borderRadius: 12,
                            background: toneStyles.color,
                            color: "white",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                        }}
                    >
                        <Icon name={isCorrect ? "check" : "close"} size={20} />
                    </div>
                    <div>
                        <div style={{ fontSize: 15, fontWeight: 600, color: toneStyles.color }}>
                            {toneStyles.title}
                        </div>
                        <div style={{ fontSize: 13, color: "var(--ink-2)", marginTop: 2, maxWidth: 400 }}>
                            {toneStyles.sub}
                        </div>
                    </div>
                </div>

                <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
                    {isCorrect && xpEarned > 0 && (
                        <span style={{ fontFamily: "var(--f-mono)", fontSize: 13, fontWeight: 500, color: toneStyles.color }}>
                            +{xpEarned} XP
                        </span>
                    )}
                    <Button variant="primary" onClick={onContinue} iconRightName="arrow-right">
                        ПРОДОЛЖИТЬ
                    </Button>
                </div>
            </div>
        </div>
    );
}
