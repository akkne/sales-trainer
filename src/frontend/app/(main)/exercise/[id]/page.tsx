"use client";

import { use, useState } from "react";
import { useRouter } from "next/navigation";
import { AnimatePresence } from "framer-motion";
import {
    useExercisesForLesson,
    useSubmitExercise,
    type ExerciseSubmissionResult,
} from "@/lib/hooks/useLesson";
import { MultipleChoiceExercise } from "@/components/exercise/MultipleChoiceExercise";
import { FillBlankExercise } from "@/components/exercise/FillBlankExercise";
import { FreeTextExercise } from "@/components/exercise/FreeTextExercise";
import { ExerciseResultBanner } from "@/components/exercise/ExerciseResultBanner";

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
            { onSuccess: (submissionResult) => setLastSubmissionResult(submissionResult) }
        );
    }

    function handleContinueAfterResult() {
        setLastSubmissionResult(null);
        if (currentExerciseIndex + 1 < totalExerciseCount) {
            setCurrentExerciseIndex((previousIndex) => previousIndex + 1);
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
        <div className="max-w-2xl mx-auto px-4 py-6 pb-40">
            <div className="flex items-center gap-3 mb-8">
                <button
                    onClick={() => router.back()}
                    className="text-gray-400 hover:text-gray-600 text-xl leading-none"
                >
                    ✕
                </button>
                <div className="flex-1 h-3 bg-gray-200 rounded-full overflow-hidden">
                    <div
                        className="h-full bg-[#58CC02] rounded-full transition-all duration-300"
                        style={{ width: `${progressPercent}%` }}
                    />
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

            <AnimatePresence>
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
            </AnimatePresence>
        </div>
    );
}
