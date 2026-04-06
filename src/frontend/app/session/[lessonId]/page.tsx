"use client";

import { use, useCallback, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
    useExercisesForLesson,
    useSubmitExercise,
    useNextLesson,
    type ExerciseSubmissionResult,
} from "@/lib/hooks/useLesson";
import { useAchievements } from "@/lib/hooks/useAchievements";
import { MultipleChoiceExercise } from "@/components/exercise/MultipleChoiceExercise";
import { FillBlankExercise } from "@/components/exercise/FillBlankExercise";
import { FreeTextExercise } from "@/components/exercise/FreeTextExercise";
import { OpenQuestionExercise } from "@/components/exercise/OpenQuestionExercise";
import { AchievementToastQueue, type AchievementToastData } from "@/components/ui/AchievementToast";

const MAX_HEARTS = 4;

interface SessionPageProps {
    params: Promise<{ lessonId: string }>;
}

type SessionState = "playing" | "complete" | "failed";

interface SessionFlowProps {
    lessonId: string;
    onRestart: () => void;
}

function formatSessionDuration(totalSeconds: number): string {
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    if (minutes === 0) return `${seconds} сек`;
    return `${minutes} мин ${seconds} сек`;
}

function SessionFlow({ lessonId, onRestart }: SessionFlowProps) {
    const router = useRouter();
    const { data: exercises, isLoading } = useExercisesForLesson(lessonId);
    const submitExerciseMutation = useSubmitExercise();
    const { data: allAchievements } = useAchievements();

    const sessionStartTimeRef = useRef<number>(Date.now());
    const sessionEndTimeRef = useRef<number>(0);
    const [currentExerciseIndex, setCurrentExerciseIndex] = useState(0);
    const [lastSubmissionResult, setLastSubmissionResult] =
        useState<ExerciseSubmissionResult | null>(null);
    const [hearts, setHearts] = useState(MAX_HEARTS);
    const [sessionState, setSessionState] = useState<SessionState>("playing");
    const [totalXpEarned, setTotalXpEarned] = useState(0);
    const [correctAnswerCount, setCorrectAnswerCount] = useState(0);
    const [toastQueue, setToastQueue] = useState<AchievementToastData[]>([]);

    const isSessionComplete = sessionState === "complete";
    const { data: nextLesson, isSuccess: isNextLessonLoaded } = useNextLesson(lessonId, isSessionComplete);

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
        if (currentExerciseIndex + 1 >= totalExerciseCount) {
            recordSessionEnd();
            setSessionState("complete");
        } else {
            setCurrentExerciseIndex((prev) => prev + 1);
        }
    }

    function handleContinueAfterResult() {
        setLastSubmissionResult(null);
        const currentHearts = hearts;
        if (currentHearts === 0) {
            recordSessionEnd();
            setSessionState("failed");
            return;
        }
        if (currentExerciseIndex + 1 >= totalExerciseCount) {
            recordSessionEnd();
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
        const sessionDurationSeconds = Math.round(
            (sessionEndTimeRef.current - sessionStartTimeRef.current) / 1000
        );
        const accuracyPercent =
            totalExerciseCount > 0
                ? Math.round((correctAnswerCount / totalExerciseCount) * 100)
                : 100;

        return (
            <div className="min-h-screen flex flex-col items-center justify-center px-6 text-center bg-white">
                <div className="text-8xl mb-6 animate-bounce">🎉</div>
                <h1 className="text-3xl font-extrabold text-gray-900 mb-2">Урок пройден!</h1>
                <p className="text-[#AFAFAF] mb-10">
                    Отличная работа. Продолжай в том же духе!
                </p>

                <div className="grid grid-cols-2 gap-3 mb-4 w-full max-w-sm">
                    <div className="bg-[#F7F7F7] rounded-2xl px-4 py-4 text-center">
                        <div className="text-2xl font-extrabold text-[#FFC800]">
                            +{totalXpEarned}
                        </div>
                        <div className="text-xs text-[#AFAFAF] uppercase tracking-wider mt-1">
                            XP заработано
                        </div>
                    </div>
                    <div className="bg-[#F7F7F7] rounded-2xl px-4 py-4 text-center">
                        <div className="text-2xl font-extrabold text-[#58CC02]">
                            {accuracyPercent}%
                        </div>
                        <div className="text-xs text-[#AFAFAF] uppercase tracking-wider mt-1">
                            Точность
                        </div>
                    </div>
                    <div className="bg-[#F7F7F7] rounded-2xl px-4 py-4 text-center">
                        <div className="text-2xl font-extrabold text-[#1CB0F6]">
                            {formatSessionDuration(sessionDurationSeconds)}
                        </div>
                        <div className="text-xs text-[#AFAFAF] uppercase tracking-wider mt-1">
                            Время
                        </div>
                    </div>
                    <div className="bg-[#F7F7F7] rounded-2xl px-4 py-4 text-center">
                        <div className="text-2xl font-extrabold text-[#FF4B4B]">
                            {Array.from({ length: hearts }, () => "❤️").join("")}
                        </div>
                        <div className="text-xs text-[#AFAFAF] uppercase tracking-wider mt-1">
                            Жизни
                        </div>
                    </div>
                </div>

                <div className="w-full max-w-sm flex flex-col gap-3 mt-6">
                    {isNextLessonLoaded && nextLesson ? (
                        <>
                            <button
                                onClick={() => router.replace(`/session/${nextLesson.lessonId}`)}
                                className="w-full py-4 rounded-2xl bg-[#58CC02] text-white font-extrabold btn-3d"
                            >
                                Следующий урок →
                            </button>
                            <p className="text-xs text-[#AFAFAF] text-center">{nextLesson.title}</p>
                        </>
                    ) : isNextLessonLoaded ? (
                        <p className="text-sm text-[#AFAFAF] text-center mb-1">Все уроки пройдены! 🎉</p>
                    ) : null}
                    <button
                        onClick={() => router.back()}
                        className={`w-full py-4 rounded-2xl font-extrabold ${
                            isNextLessonLoaded && nextLesson
                                ? "text-[#AFAFAF] hover:text-gray-600 transition-colors"
                                : "bg-[#58CC02] text-white btn-3d"
                        }`}
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
            {/* Achievement toast queue */}
            <AchievementToastQueue queue={toastQueue} onDismiss={dismissToast} />

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

            {/* Exercise content — keyed by exerciseId to fully reset state on each new question */}
            <div key={currentExercise.exerciseId} className="flex-1 overflow-y-auto px-4 pb-10 pt-6 max-w-2xl w-full mx-auto">
                {currentExercise.type === "multiple_choice" && (
                    <MultipleChoiceExercise
                        key={currentExercise.exerciseId}
                        content={
                            currentExercise.content as Parameters<
                                typeof MultipleChoiceExercise
                            >[0]["content"]
                        }
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === "fill_blank" && (
                    <FillBlankExercise
                        key={currentExercise.exerciseId}
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
                {currentExercise.type === "free_text" && (
                    <FreeTextExercise
                        key={currentExercise.exerciseId}
                        content={
                            currentExercise.content as Parameters<
                                typeof FreeTextExercise
                            >[0]["content"]
                        }
                        onSubmit={handleExerciseSubmit}
                        onSkip={handleSkip}
                        onContinue={handleContinueAfterResult}
                        isSubmitting={submitExerciseMutation.isPending}
                        submittedResult={lastSubmissionResult}
                    />
                )}
                {currentExercise.type === "open_question" && (
                    <OpenQuestionExercise
                        key={currentExercise.exerciseId}
                        content={
                            currentExercise.content as Parameters<
                                typeof OpenQuestionExercise
                            >[0]["content"]
                        }
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
    const [restartKey, setRestartKey] = useState(0);

    return (
        <SessionFlow
            key={restartKey}
            lessonId={lessonId}
            onRestart={() => setRestartKey((k) => k + 1)}
        />
    );
}
