"use client";

import { motion } from "framer-motion";

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
    score,
    explanation,
    aiFeedback,
    xpEarned,
    onContinue,
}: ExerciseResultBannerProps) {
    return (
        <motion.div
            initial={{ y: "100%" }}
            animate={{ y: 0 }}
            transition={{ type: "spring", stiffness: 300, damping: 30 }}
            className={`fixed bottom-0 left-0 right-0 rounded-t-3xl p-6 shadow-xl ${
                isCorrect ? "bg-[#D7FFB8]" : "bg-[#FFDFE0]"
            }`}
        >
            <div className="max-w-2xl mx-auto">
                <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-3">
                        <span className="text-3xl">{isCorrect ? "🎉" : "❌"}</span>
                        <div>
                            <p
                                className={`font-[var(--font-space-grotesk)] font-bold text-lg ${
                                    isCorrect ? "text-[#4CAD00]" : "text-[#FF4B4B]"
                                }`}
                            >
                                {isCorrect ? "Отлично!" : "Неверно"}
                            </p>
                            {isCorrect && xpEarned > 0 && (
                                <p className="text-sm text-[#4CAD00]">+{xpEarned} XP</p>
                            )}
                        </div>
                    </div>
                    {!isCorrect && (
                        <span className="font-bold text-[#FF4B4B]">{score}%</span>
                    )}
                </div>

                {(explanation || aiFeedback) && (
                    <p className="text-gray-700 text-sm mb-4">
                        {explanation ?? aiFeedback}
                    </p>
                )}

                <button
                    onClick={onContinue}
                    className={`w-full py-4 rounded-2xl font-bold text-white transition-transform active:translate-y-1 ${
                        isCorrect
                            ? "bg-[#58CC02] shadow-[0_4px_0_#4CAD00] active:shadow-none"
                            : "bg-[#FF4B4B] shadow-[0_4px_0_#CC3333] active:shadow-none"
                    }`}
                >
                    Продолжить
                </button>
            </div>
        </motion.div>
    );
}
