"use client";

import { Icon } from "@/components/ui/Icon";

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
    return (
        <div
            className={`fixed bottom-0 left-0 right-0 rounded-t-3xl px-6 pt-6 pb-8 shadow-xl slide-up ${
                isCorrect ? "bg-primary-container" : "bg-error-container"
            }`}
        >
            <div className="max-w-2xl mx-auto">
                <div className="flex items-center gap-4 mb-3">
                    <div
                        className={`w-12 h-12 rounded-full flex items-center justify-center shrink-0 ${
                            isCorrect ? "bg-primary" : "bg-error"
                        }`}
                    >
                        <Icon
                            name={isCorrect ? "check" : "close"}
                            size="md"
                            className={isCorrect ? "text-on-primary" : "text-on-error"}
                        />
                    </div>
                    <div>
                        <p
                            className={`font-headline font-bold text-lg leading-tight ${
                                isCorrect ? "text-on-primary-container" : "text-on-error-container"
                            }`}
                        >
                            {isCorrect ? "Отлично!" : "Неверно"}
                        </p>
                        {isCorrect && xpEarned > 0 && (
                            <p className="text-sm font-semibold text-primary">+{xpEarned} XP</p>
                        )}
                    </div>
                </div>

                {(explanation || aiFeedback) && (
                    <p
                        className={`text-sm mb-4 leading-relaxed ${
                            isCorrect ? "text-on-primary-container" : "text-on-error-container"
                        }`}
                    >
                        {explanation ?? aiFeedback}
                    </p>
                )}

                <button
                    onClick={onContinue}
                    className={`w-full py-4 rounded-full font-extrabold ${
                        isCorrect
                            ? "bg-primary text-on-primary btn-3d"
                            : "bg-error text-on-error btn-3d-red"
                    }`}
                >
                    {isCorrect ? "ПРОДОЛЖИТЬ" : "ПОНЯТНО"}
                </button>
            </div>
        </div>
    );
}
