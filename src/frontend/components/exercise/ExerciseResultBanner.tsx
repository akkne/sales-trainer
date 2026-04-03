"use client";

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
                isCorrect ? "bg-[#D7FFB8]" : "bg-[#FFDFE0]"
            }`}
        >
            <div className="max-w-2xl mx-auto">
                <div className="flex items-center gap-4 mb-3">
                    <div
                        className={`w-12 h-12 rounded-full flex items-center justify-center text-2xl shrink-0 ${
                            isCorrect ? "bg-[#58CC02]" : "bg-[#FF4B4B]"
                        }`}
                    >
                        {isCorrect ? "✓" : "✕"}
                    </div>
                    <div>
                        <p
                            className={`font-extrabold text-lg leading-tight ${
                                isCorrect ? "text-[#3C8400]" : "text-[#CC3333]"
                            }`}
                        >
                            {isCorrect ? "Отлично!" : "Неверно"}
                        </p>
                        {isCorrect && xpEarned > 0 && (
                            <p className="text-sm font-semibold text-[#58A700]">+{xpEarned} XP</p>
                        )}
                    </div>
                </div>

                {(explanation || aiFeedback) && (
                    <p
                        className={`text-sm mb-4 leading-relaxed ${
                            isCorrect ? "text-[#3C8400]" : "text-[#CC3333]"
                        }`}
                    >
                        {explanation ?? aiFeedback}
                    </p>
                )}

                <button
                    onClick={onContinue}
                    className={`w-full py-4 rounded-2xl font-extrabold text-white ${
                        isCorrect ? "bg-[#58CC02] btn-3d" : "bg-[#FF4B4B] btn-3d-red"
                    }`}
                >
                    {isCorrect ? "ПРОДОЛЖИТЬ" : "ПОНЯТНО"}
                </button>
            </div>
        </div>
    );
}
