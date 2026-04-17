"use client";

import { use, useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
    useExercisesForLesson,
    useSubmitExercise,
    type ExerciseSubmissionResult,
    type ExerciseData,
} from "@/lib/hooks/useLesson";
import { useAchievements } from "@/lib/hooks/useAchievements";
import { ExerciseTypes } from "@/lib/exerciseTypes";
import { ChooseOptionExercise } from "@/components/exercise/ChooseOptionExercise";
import { FillBlankExercise } from "@/components/exercise/FillBlankExercise";
import { ReorderExercise } from "@/components/exercise/ReorderExercise";
import { MatchPairsExercise } from "@/components/exercise/MatchPairsExercise";
import { CategorizeExercise } from "@/components/exercise/CategorizeExercise";
import { SpotMistakeExercise } from "@/components/exercise/SpotMistakeExercise";
import { RewriteExercise } from "@/components/exercise/RewriteExercise";
import { AiDialogueExercise } from "@/components/exercise/AiDialogueExercise";
import { EvaluateCallExercise } from "@/components/exercise/EvaluateCallExercise";
import { FreeTextExercise } from "@/components/exercise/FreeTextExercise";
import { AchievementToastQueue, type AchievementToastData } from "@/components/ui/AchievementToast";
import { Icon } from "@/components/ui/Icon";

// Score threshold for passing (7/10 or higher is passing)
const PASSING_SCORE_THRESHOLD = 7;
const MAX_RETRY_ATTEMPTS = 2;

interface SessionPageProps {
    params: Promise<{ lessonId: string }>;
}

type SessionState = "playing" | "complete";

interface SessionFlowProps {
    lessonId: string;
}

interface QueuedExercise {
    exercise: ExerciseData;
    attemptNumber: number; // 1 = first attempt, 2 = retry
    queueKey: string; // unique key for React
}

function formatSessionDuration(totalSeconds: number): string {
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    if (minutes === 0) return `${seconds} сек`;
    return `${minutes} мин ${seconds} сек`;
}

function SessionFlow({ lessonId }: SessionFlowProps) {
    const router = useRouter();
    const { data: exercises, isLoading } = useExercisesForLesson(lessonId);
    const submitExerciseMutation = useSubmitExercise();
    const { data: allAchievements } = useAchievements();

    const sessionStartTimeRef = useRef<number>(Date.now());
    const sessionEndTimeRef = useRef<number>(0);
    const [lastSubmissionResult, setLastSubmissionResult] =
        useState<ExerciseSubmissionResult | null>(null);
    const [sessionState, setSessionState] = useState<SessionState>("playing");
    const [totalXpEarned, setTotalXpEarned] = useState(0);
    const [correctAnswerCount, setCorrectAnswerCount] = useState(0);
    const [toastQueue, setToastQueue] = useState<AchievementToastData[]>([]);

    // Exercise queue: starts with all exercises, failed ones get added to end
    const [exerciseQueue, setExerciseQueue] = useState<QueuedExercise[]>([]);
    const [currentQueueIndex, setCurrentQueueIndex] = useState(0);

    // Initialize queue when exercises load
    useEffect(() => {
        if (exercises && exercises.length > 0 && exerciseQueue.length === 0) {
            const initialQueue: QueuedExercise[] = exercises.map((ex) => ({
                exercise: ex,
                attemptNumber: 1,
                queueKey: `${ex.exerciseId}-1`,
            }));
            setExerciseQueue(initialQueue);
        }
    }, [exercises, exerciseQueue.length]);

    const currentQueued = exerciseQueue[currentQueueIndex];
    const currentExercise = currentQueued?.exercise;

    // Track original exercise count for accuracy calculation
    const originalExerciseCount = exercises?.length ?? 0;

    // Progress is based on current index vs current queue length
    const progressPercent =
        exerciseQueue.length > 0
            ? Math.round((currentQueueIndex / exerciseQueue.length) * 100)
            : 0;

    function handleExerciseSubmit(answer: unknown) {
        if (!currentExercise || !currentQueued) return;
        submitExerciseMutation.mutate(
            { exerciseId: currentExercise.exerciseId, answer },
            {
                onSuccess: (result) => {
                    setLastSubmissionResult(result);

                    // For AI-evaluated exercises (score 0-10), check against threshold
                    // For standard exercises, isCorrect is already set by backend
                    // Note: result.score is 0-100 scale, threshold is 0-10 scale
                    const isPassing = result.isCorrect || (result.score !== undefined && result.score >= PASSING_SCORE_THRESHOLD * 10);

                    if (isPassing) {
                        setTotalXpEarned((prev) => prev + result.xpEarned);
                        setCorrectAnswerCount((prev) => prev + 1);
                        // Queue achievement toasts for newly unlocked achievements
                        if (result.newlyUnlockedAchievementKeys?.length && allAchievements) {
                            const newToasts = result.newlyUnlockedAchievementKeys
                                .map((key) => allAchievements.find((a) => a.key === key))
                                .filter(Boolean)
                                .map((a) => ({
                                    key: a!.key,
                                    iconEmoji: a!.iconEmoji,
                                    title: a!.title,
                                    description: a!.description,
                                }));
                            if (newToasts.length > 0) {
                                setToastQueue((prev) => [...prev, ...newToasts]);
                            }
                        }
                    } else {
                        // Failed: if under max retries, queue for retry at end
                        if (currentQueued.attemptNumber < MAX_RETRY_ATTEMPTS) {
                            const retryEntry: QueuedExercise = {
                                exercise: currentExercise,
                                attemptNumber: currentQueued.attemptNumber + 1,
                                queueKey: `${currentExercise.exerciseId}-${currentQueued.attemptNumber + 1}`,
                            };
                            setExerciseQueue((prev) => [...prev, retryEntry]);
                        }
                    }
                },
            }
        );
    }

    const dismissToast = useCallback((key: string) => {
        setToastQueue((prev) => prev.filter((t) => t.key !== key));
    }, []);

    function recordSessionEnd() {
        sessionEndTimeRef.current = Date.now();
    }

    function handleSkip() {
        if (currentQueueIndex + 1 >= exerciseQueue.length) {
            recordSessionEnd();
            setSessionState("complete");
        } else {
            setCurrentQueueIndex((prev) => prev + 1);
        }
    }

    function handleContinueAfterResult() {
        setLastSubmissionResult(null);
        if (currentQueueIndex + 1 >= exerciseQueue.length) {
            recordSessionEnd();
            setSessionState("complete");
        } else {
            setCurrentQueueIndex((prev) => prev + 1);
        }
    }

    if (isLoading || exerciseQueue.length === 0) {
        return (
            <div className="flex items-center justify-center min-h-screen bg-surface">
                <div className="w-10 h-10 rounded-full border-4 border-primary border-t-transparent animate-spin" />
            </div>
        );
    }

    // Completion screen
    if (sessionState === "complete") {
        const sessionDurationSeconds = Math.round(
            (sessionEndTimeRef.current - sessionStartTimeRef.current) / 1000
        );
        const accuracyPercent =
            originalExerciseCount > 0
                ? Math.round((correctAnswerCount / originalExerciseCount) * 100)
                : 100;

        return (
            <div className="min-h-screen flex flex-col items-center justify-center px-6 text-center bg-surface">
                <div className="w-24 h-24 rounded-full bg-primary-container flex items-center justify-center mb-6">
                    <Icon name="celebration" size="2xl" className="text-primary" />
                </div>
                <h1 className="font-headline text-3xl font-bold text-on-surface mb-2">Урок пройден!</h1>
                <p className="text-on-surface-variant mb-10">
                    Отличная работа. Продолжай в том же духе!
                </p>

                <div className="grid grid-cols-3 gap-3 mb-4 w-full max-w-sm">
                    <div className="bg-surface-container rounded-2xl px-4 py-4 text-center">
                        <div className="font-headline text-2xl font-bold text-secondary">
                            +{totalXpEarned}
                        </div>
                        <div className="text-xs text-on-surface-variant uppercase tracking-wider mt-1">
                            XP заработано
                        </div>
                    </div>
                    <div className="bg-surface-container rounded-2xl px-4 py-4 text-center">
                        <div className="font-headline text-2xl font-bold text-primary">
                            {accuracyPercent}%
                        </div>
                        <div className="text-xs text-on-surface-variant uppercase tracking-wider mt-1">
                            Точность
                        </div>
                    </div>
                    <div className="bg-surface-container rounded-2xl px-4 py-4 text-center">
                        <div className="font-headline text-2xl font-bold text-tertiary">
                            {formatSessionDuration(sessionDurationSeconds)}
                        </div>
                        <div className="text-xs text-on-surface-variant uppercase tracking-wider mt-1">
                            Время
                        </div>
                    </div>
                </div>

                <div className="w-full max-w-sm flex flex-col gap-3 mt-6">
                    <button
                        onClick={() => router.back()}
                        className="w-full py-4 rounded-full bg-primary text-on-primary font-bold shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 tonal-transition"
                    >
                        Вернуться к пути
                    </button>
                </div>
            </div>
        );
    }

    if (!currentExercise) {
        return (
            <div className="flex items-center justify-center min-h-screen text-on-surface-variant bg-surface">
                Упражнения не найдены
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-surface flex flex-col">
            {/* Achievement toast queue */}
            <AchievementToastQueue queue={toastQueue} onDismiss={dismissToast} />

            {/* Header: ✕ + progress bar */}
            <div className="flex items-center gap-3 px-4 py-5 border-b border-outline-variant sticky top-0 bg-surface z-10">
                <button
                    onClick={() => router.back()}
                    className="text-on-surface-variant hover:text-on-surface tonal-transition shrink-0"
                    aria-label="Выйти"
                >
                    <Icon name="close" size="md" />
                </button>

                <div className="flex-1 h-3 bg-surface-container-highest rounded-full overflow-hidden">
                    <div
                        className="h-full bg-primary rounded-full transition-all duration-500"
                        style={{ width: `${progressPercent}%` }}
                    />
                </div>

                {currentQueued.attemptNumber > 1 && (
                    <span className="text-xs text-on-surface-variant bg-surface-container px-2 py-1 rounded-full shrink-0">
                        Повтор
                    </span>
                )}
            </div>

            {/* Exercise content — keyed by queueKey to fully reset state on each new question */}
            <div key={currentQueued.queueKey} className="flex-1 overflow-y-auto px-4 pb-10 pt-6 max-w-2xl w-full mx-auto">
                {currentExercise.type === ExerciseTypes.ChooseOption && (
                    <ChooseOptionExercise
                        key={currentQueued.queueKey}
                        content={
                            currentExercise.content as Parameters<
                                typeof ChooseOptionExercise
                            >[0]["content"]
                        }
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === ExerciseTypes.FillBlank && (
                    <FillBlankExercise
                        key={currentQueued.queueKey}
                        content={
                            currentExercise.content as Parameters<
                                typeof FillBlankExercise
                            >[0]["content"]
                        }
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === ExerciseTypes.Reorder && (
                    <ReorderExercise
                        key={currentQueued.queueKey}
                        content={currentExercise.content as Parameters<typeof ReorderExercise>[0]["content"]}
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === ExerciseTypes.MatchPairs && (
                    <MatchPairsExercise
                        key={currentQueued.queueKey}
                        content={currentExercise.content as Parameters<typeof MatchPairsExercise>[0]["content"]}
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === ExerciseTypes.Categorize && (
                    <CategorizeExercise
                        key={currentQueued.queueKey}
                        content={currentExercise.content as Parameters<typeof CategorizeExercise>[0]["content"]}
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === ExerciseTypes.SpotMistake && (
                    <SpotMistakeExercise
                        key={currentQueued.queueKey}
                        content={currentExercise.content as Parameters<typeof SpotMistakeExercise>[0]["content"]}
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === ExerciseTypes.Rewrite && (
                    <RewriteExercise
                        key={currentQueued.queueKey}
                        content={currentExercise.content as Parameters<typeof RewriteExercise>[0]["content"]}
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                        submitError={submitExerciseMutation.error}
                    />
                )}
                {currentExercise.type === ExerciseTypes.AiDialogue && (
                    <AiDialogueExercise
                        key={currentQueued.queueKey}
                        exerciseId={currentExercise.exerciseId}
                        content={currentExercise.content as Parameters<typeof AiDialogueExercise>[0]["content"]}
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === ExerciseTypes.EvaluateCall && (
                    <EvaluateCallExercise
                        key={currentQueued.queueKey}
                        content={currentExercise.content as Parameters<typeof EvaluateCallExercise>[0]["content"]}
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === ExerciseTypes.FreeText && (
                    <FreeTextExercise
                        key={currentQueued.queueKey}
                        content={currentExercise.content as Parameters<typeof FreeTextExercise>[0]["content"]}
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
            </div>

        </div>
    );
}

export default function SessionPage({ params }: SessionPageProps) {
    const { lessonId } = use(params);

    return <SessionFlow lessonId={lessonId} />;
}
