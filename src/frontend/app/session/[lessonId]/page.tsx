"use client";

import { use, useState } from "react";
import { useRouter } from "next/navigation";
import {
    useExercisesForLesson,
    useSubmitExercise,
    type ExerciseSubmissionResult,
} from "@/lib/hooks/useLesson";
import { MultipleChoiceExercise } from "@/components/exercise/MultipleChoiceExercise";
import { FillBlankExercise } from "@/components/exercise/FillBlankExercise";
import { FreeTextExercise } from "@/components/exercise/FreeTextExercise";
import { ExerciseResultBanner } from "@/components/exercise/ExerciseResultBanner";

const MAX_HEARTS = 4;

interface SessionPageProps {
    params: Promise<{ lessonId: string }>;
}

type SessionState = "playing" | "complete" | "failed";

interface SessionFlowProps {
    lessonId: string;
    onRestart: () => void;
}

function SessionFlow({ lessonId, onRestart }: SessionFlowProps) {
    const router = useRouter();
    const { data: exercises, isLoading } = useExercisesForLesson(lessonId);
    const submitExerciseMutation = useSubmitExercise();

    const [currentExerciseIndex, setCurrentExerciseIndex] = useState(0);
    const [lastSubmissionResult, setLastSubmissionResult] =
        useState<ExerciseSubmissionResult | null>(null);
    const [hearts, setHearts] = useState(MAX_HEARTS);
    const [sessionState, setSessionState] = useState<SessionState>("playing");
    const [totalXpEarned, setTotalXpEarned] = useState(0);

    const currentExercise = exercises?.[currentExerciseIndex];
    const totalExerciseCount = exercises?.length ?? 0;
    const progressPercent =
        totalExerciseCount > 0
            ? Math.round((currentExerciseIndex / totalExerciseCount) * 100)
            : 0;

    function handleExerciseSubmit(answer: unknown) {
        if (!currentExercise) return;
        submitExerciseMutation.mutate(
            { exerciseId: currentExercise.exerciseId, answer },
            {
                onSuccess: (result) => {
                    setLastSubmissionResult(result);
                    if (!result.isCorrect) {
                        setHearts((h) => Math.max(0, h - 1));
                    } else {
                        setTotalXpEarned((prev) => prev + result.xpEarned);
                    }
                },
            }
        );
    }

    function handleSkip() {
        if (currentExerciseIndex + 1 >= totalExerciseCount) {
            setSessionState("complete");
        } else {
            setCurrentExerciseIndex((prev) => prev + 1);
        }
    }

    function handleContinueAfterResult() {
        setLastSubmissionResult(null);
        const currentHearts = hearts;
        if (currentHearts === 0) {
            setSessionState("failed");
            return;
        }
        if (currentExerciseIndex + 1 >= totalExerciseCount) {
            setSessionState("complete");
        } else {
            setCurrentExerciseIndex((prev) => prev + 1);
        }
    }

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
            </div>
        );
    }

    // Completion screen
    if (sessionState === "complete") {
        return (
            <div className="min-h-screen flex flex-col items-center justify-center px-6 text-center bg-white">
                <div className="text-8xl mb-6 animate-bounce">🎉</div>
                <h1 className="text-3xl font-extrabold text-gray-900 mb-2">Урок пройден!</h1>
                <p className="text-[#AFAFAF] mb-10">
                    Отличная работа. Продолжай в том же духе!
                </p>

                <div className="flex gap-4 mb-10">
                    <div className="bg-[#F7F7F7] rounded-2xl px-6 py-4 text-center min-w-[110px]">
                        <div className="text-2xl font-extrabold text-[#FFC800]">
                            +{totalXpEarned}
                        </div>
                        <div className="text-xs text-[#AFAFAF] uppercase tracking-wider mt-1">
                            XP заработано
                        </div>
                    </div>
                    <div className="bg-[#F7F7F7] rounded-2xl px-6 py-4 text-center min-w-[110px]">
                        <div className="text-2xl font-extrabold text-[#58CC02]">
                            {totalExerciseCount}
                        </div>
                        <div className="text-xs text-[#AFAFAF] uppercase tracking-wider mt-1">
                            Упражнений
                        </div>
                    </div>
                    <div className="bg-[#F7F7F7] rounded-2xl px-6 py-4 text-center min-w-[110px]">
                        <div className="text-2xl font-extrabold text-[#FF4B4B]">
                            {Array.from({ length: hearts }, () => "❤️").join("")}
                        </div>
                        <div className="text-xs text-[#AFAFAF] uppercase tracking-wider mt-1">
                            Жизни
                        </div>
                    </div>
                </div>

                <div className="w-full max-w-xs flex flex-col gap-3">
                    <button
                        onClick={() => router.back()}
                        className="w-full py-4 rounded-2xl bg-[#58CC02] text-white font-extrabold btn-3d"
                    >
                        Вернуться к пути
                    </button>
                </div>
            </div>
        );
    }

    // Failure screen
    if (sessionState === "failed") {
        return (
            <div className="min-h-screen flex flex-col items-center justify-center px-6 text-center bg-white">
                <div className="text-8xl mb-6">💔</div>
                <h1 className="text-3xl font-extrabold text-gray-900 mb-2">
                    Жизни закончились
                </h1>
                <p className="text-[#AFAFAF] mb-10">
                    Не сдавайся — попробуй ещё раз!
                </p>

                <div className="w-full max-w-xs flex flex-col gap-3">
                    <button
                        onClick={onRestart}
                        className="w-full py-4 rounded-2xl bg-[#FF4B4B] text-white font-extrabold btn-3d-red"
                    >
                        Попробовать снова
                    </button>
                    <button
                        onClick={() => router.back()}
                        className="w-full py-4 rounded-2xl text-[#AFAFAF] font-semibold hover:text-gray-600 transition-colors"
                    >
                        Вернуться к пути
                    </button>
                </div>
            </div>
        );
    }

    if (!currentExercise) {
        return (
            <div className="flex items-center justify-center min-h-screen text-gray-500">
                Упражнения не найдены
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-white flex flex-col">
            {/* Header: ✕ + progress bar + hearts */}
            <div className="flex items-center gap-3 px-4 py-5 border-b border-[#E5E5E5] sticky top-0 bg-white z-10">
                <button
                    onClick={() => router.back()}
                    className="text-[#AFAFAF] hover:text-gray-500 text-xl leading-none transition-colors shrink-0"
                    aria-label="Выйти"
                >
                    ✕
                </button>

                <div className="flex-1 h-4 bg-[#E5E5E5] rounded-full overflow-hidden">
                    <div
                        className="h-full bg-[#58CC02] rounded-full transition-all duration-500"
                        style={{ width: `${progressPercent}%` }}
                    />
                </div>

                <div className="flex items-center gap-0.5 shrink-0">
                    {Array.from({ length: MAX_HEARTS }).map((_, i) => (
                        <span
                            key={i}
                            className={`text-xl transition-all duration-200 ${
                                i < hearts ? "opacity-100" : "opacity-20 grayscale"
                            }`}
                        >
                            ❤️
                        </span>
                    ))}
                </div>
            </div>

            {/* Exercise content */}
            <div className="flex-1 overflow-y-auto px-4 pb-10 pt-6 max-w-2xl w-full mx-auto">
                {currentExercise.type === "multiple_choice" && (
                    <MultipleChoiceExercise
                        content={
                            currentExercise.content as Parameters<
                                typeof MultipleChoiceExercise
                            >[0]["content"]
                        }
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        isSubmitting={submitExerciseMutation.isPending}
                    />
                )}
                {currentExercise.type === "fill_blank" && (
                    <FillBlankExercise
                        content={
                            currentExercise.content as Parameters<
                                typeof FillBlankExercise
                            >[0]["content"]
                        }
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        isSubmitting={submitExerciseMutation.isPending}
                    />
                )}
                {currentExercise.type === "free_text" && (
                    <FreeTextExercise
                        content={
                            currentExercise.content as Parameters<
                                typeof FreeTextExercise
                            >[0]["content"]
                        }
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        isSubmitting={submitExerciseMutation.isPending}
                    />
                )}
            </div>

            {lastSubmissionResult && (
                <ExerciseResultBanner
                    isCorrect={lastSubmissionResult.isCorrect}
                    score={lastSubmissionResult.score}
                    explanation={lastSubmissionResult.explanation}
                    aiFeedback={lastSubmissionResult.aiFeedback}
                    xpEarned={lastSubmissionResult.xpEarned}
                    onContinue={handleContinueAfterResult}
                />
            )}
        </div>
    );
}

export default function SessionPage({ params }: SessionPageProps) {
    const { lessonId } = use(params);
    const [restartKey, setRestartKey] = useState(0);

    return (
        <SessionFlow
            key={restartKey}
            lessonId={lessonId}
            onRestart={() => setRestartKey((k) => k + 1)}
        />
    );
}
