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

interface ExercisePageProps {
    params: Promise<{ id: string }>;
}

export default function ExercisePage({ params }: ExercisePageProps) {
    const { id: lessonId } = use(params);
    const router = useRouter();

    const { data: exercises, isLoading } = useExercisesForLesson(lessonId);
    const submitExerciseMutation = useSubmitExercise();

    const [currentExerciseIndex, setCurrentExerciseIndex] = useState(0);
    const [lastSubmissionResult, setLastSubmissionResult] =
        useState<ExerciseSubmissionResult | null>(null);
    const [hearts, setHearts] = useState(MAX_HEARTS);

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
                    if (!result.isCorrect && hearts > 0) {
                        setHearts((h) => Math.max(0, h - 1));
                    }
                },
            }
        );
    }

    function handleContinueAfterResult() {
        setLastSubmissionResult(null);
        if (currentExerciseIndex + 1 < totalExerciseCount) {
            setCurrentExerciseIndex((prev) => prev + 1);
        } else {
            router.back();
        }
    }

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-screen">
                <div className="w-10 h-10 rounded-full border-4 border-[#58CC02] border-t-transparent animate-spin" />
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
        <div className="max-w-2xl mx-auto px-4 pb-40">
            {/* Header: X + progress bar + hearts */}
            <div className="flex items-center gap-3 py-5 sticky top-0 bg-white z-10">
                <button
                    onClick={() => router.back()}
                    className="text-[#AFAFAF] hover:text-gray-500 text-xl leading-none transition-colors"
                    aria-label="Выйти"
                >
                    ✕
                </button>

                <div className="flex-1 h-4 bg-[#E5E5E5] rounded-full overflow-hidden">
                    <div
                        className="h-full bg-[#58CC02] rounded-full transition-all duration-300"
                        style={{ width: `${progressPercent}%` }}
                    />
                </div>

                {/* Hearts counter */}
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

            {currentExercise.type === "multiple_choice" && (
                <MultipleChoiceExercise
                    content={currentExercise.content as Parameters<typeof MultipleChoiceExercise>[0]["content"]}
                    onSubmit={handleExerciseSubmit}
                    isSubmitting={submitExerciseMutation.isPending}
                />
            )}

            {currentExercise.type === "fill_blank" && (
                <FillBlankExercise
                    content={currentExercise.content as Parameters<typeof FillBlankExercise>[0]["content"]}
                    onSubmit={handleExerciseSubmit}
                    isSubmitting={submitExerciseMutation.isPending}
                />
            )}

            {currentExercise.type === "free_text" && (
                <FreeTextExercise
                    content={currentExercise.content as Parameters<typeof FreeTextExercise>[0]["content"]}
                    onSubmit={handleExerciseSubmit}
                    isSubmitting={submitExerciseMutation.isPending}
                />
            )}

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
