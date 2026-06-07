"use client";

import { use, useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
    useExercisesForLesson,
    useSubmitExercise,
    type ExerciseSubmissionResult,
    type ExerciseData,
} from "@/features/exercise/hooks/use-lesson";
import { useAchievements } from "@/features/achievements/hooks/use-achievements";
import { ExerciseTypes } from "@/features/exercise/types/exercise-types";
import { ChooseOptionExercise } from "@/features/exercise/components/choose-option-exercise";
import { FillBlankExercise } from "@/features/exercise/components/fill-blank-exercise";
import { ReorderExercise } from "@/features/exercise/components/reorder-exercise";
import { MatchPairsExercise } from "@/features/exercise/components/match-pairs-exercise";
import { CategorizeExercise } from "@/features/exercise/components/categorize-exercise";
import { SpotMistakeExercise } from "@/features/exercise/components/spot-mistake-exercise";
import { RewriteExercise } from "@/features/exercise/components/rewrite-exercise";
import { AiDialogueExercise } from "@/features/exercise/components/ai-dialogue-exercise";
import { EvaluateCallExercise } from "@/features/exercise/components/evaluate-call-exercise";
import { FreeTextExercise } from "@/features/exercise/components/free-text-exercise";
import { AchievementToastQueue, type AchievementToastData } from "@/shared/components/achievement-toast";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { Progress } from "@/shared/components/progress";

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
    attemptNumber: number;
    queueKey: string;
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
    const [lastSubmissionResult, setLastSubmissionResult] = useState<ExerciseSubmissionResult | null>(null);
    const [sessionState, setSessionState] = useState<SessionState>("playing");
    const [totalXpEarned, setTotalXpEarned] = useState(0);
    const [correctAnswerCount, setCorrectAnswerCount] = useState(0);
    const [hearts, setHearts] = useState(4);
    const [toastQueue, setToastQueue] = useState<AchievementToastData[]>([]);
    const [exerciseQueue, setExerciseQueue] = useState<QueuedExercise[]>([]);
    const [currentQueueIndex, setCurrentQueueIndex] = useState(0);

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
    const originalExerciseCount = exercises?.length ?? 0;
    const progressPercent = exerciseQueue.length > 0 ? Math.round((currentQueueIndex / exerciseQueue.length) * 100) : 0;

    function handleExerciseSubmit(answer: unknown) {
        if (!currentExercise || !currentQueued) return;
        submitExerciseMutation.mutate(
            { exerciseId: currentExercise.exerciseId, answer },
            {
                onSuccess: (result) => {
                    setLastSubmissionResult(result);
                    const isPassing = result.isCorrect || (result.score !== undefined && result.score >= PASSING_SCORE_THRESHOLD * 10);

                    if (isPassing) {
                        setTotalXpEarned((prev) => prev + result.xpEarned);
                        setCorrectAnswerCount((prev) => prev + 1);
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
                        setHearts((prev) => Math.max(0, prev - 1));
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
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", background: "var(--bg)" }}>
                <div style={{ width: 40, height: 40, borderRadius: "50%", border: "4px solid var(--primary)", borderTopColor: "transparent", animation: "spin 0.8s linear infinite" }} />
            </div>
        );
    }

    // Completion screen
    if (sessionState === "complete") {
        const sessionDurationSeconds = Math.round((sessionEndTimeRef.current - sessionStartTimeRef.current) / 1000);
        const accuracyPercent = originalExerciseCount > 0 ? Math.round((correctAnswerCount / originalExerciseCount) * 100) : 100;

        return (
            <div className="complete">
                <Confetti />
                <div className="complete-inner fade-up">
                    <div className="check-circle">
                        <Icon name="check" size={56} color="#fff" />
                    </div>
                    <span className="eyebrow" style={{ justifyContent: "center" }}>
                        Урок завершён
                    </span>
                    <h1 className="h1" style={{ margin: "12px 0 30px" }}>
                        Отличная работа!
                    </h1>
                    <div className="complete-stats">
                        <div className="cs">
                            <Icon name="bolt" size={24} style={{ color: "var(--primary)" }} />
                            <b className="num">+{totalXpEarned}</b>
                            <span>XP</span>
                        </div>
                        <div className="cs">
                            <Icon name="target" size={24} style={{ color: "var(--success)" }} />
                            <b className="num">{accuracyPercent}%</b>
                            <span>точность</span>
                        </div>
                        {sessionDurationSeconds > 0 && (
                            <div className="cs">
                                <Icon name="clock" size={24} style={{ color: "var(--violet)" }} />
                                <b className="num">{formatSessionDuration(sessionDurationSeconds)}</b>
                                <span>время</span>
                            </div>
                        )}
                        <div className="cs">
                            <Icon name="heart" size={24} style={{ color: "var(--heart)" }} />
                            <b className="num">{hearts}/4</b>
                            <span>жизни</span>
                        </div>
                    </div>
                    <button
                        className="btn btn-primary btn-lg btn-block"
                        style={{ marginTop: 30 }}
                        onClick={() => router.back()}
                    >
                        Вернуться к пути
                        <Icon name="arrow-right" size={18} />
                    </button>
                </div>
            </div>
        );
    }

    if (!currentExercise) {
        return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", color: "var(--ink-3)", background: "var(--bg)" }}>
                Упражнения не найдены
            </div>
        );
    }

    return (
        <div className="session">
            <AchievementToastQueue queue={toastQueue} onDismiss={dismissToast} />

            {/* Header */}
            <div className="session-top">
                <button
                    className="icon-btn"
                    onClick={() => router.back()}
                    aria-label="Выйти"
                >
                    <Icon name="close" size={22} />
                </button>

                <div className="grow">
                    <Progress value={progressPercent} max={100} tone="indigo" height={8} />
                </div>

                {/* Hearts */}
                <div className="row gap-1">
                    {Array.from({ length: 4 }).map((_, i) => (
                        <Icon
                            key={i}
                            name="heart"
                            size={22}
                            color={i < hearts ? "var(--heart)" : "var(--line-2)"}
                        />
                    ))}
                </div>
            </div>

            {/* Exercise content */}
            <div
                key={currentQueued.queueKey}
                className="session-body"
                style={{
                    overflowY: "auto",
                    padding: lastSubmissionResult?.aiFeedback ? "48px 24px 320px" : "48px 24px 180px",
                    alignItems: "flex-start",
                }}
            >
                <div className="exercise fade-up" style={{ maxWidth: 900 }}>
                    {currentExercise.type === ExerciseTypes.ChooseOption && (
                        <ChooseOptionExercise
                            key={currentQueued.queueKey}
                            content={currentExercise.content as Parameters<typeof ChooseOptionExercise>[0]["content"]}
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
                            content={currentExercise.content as Parameters<typeof FillBlankExercise>[0]["content"]}
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
        </div>
    );
}

const CONFETTI_COLORS = ["var(--primary)", "var(--violet)", "var(--flame)", "var(--success)", "var(--amber)"];

function Confetti() {
    return (
        <div className="confetti">
            {Array.from({ length: 40 }).map((_, i) => (
                <span
                    key={i}
                    style={{
                        left: `${i * 2.5}%`,
                        animationDelay: `${(i % 10) * 0.12}s`,
                        background: CONFETTI_COLORS[i % CONFETTI_COLORS.length],
                    }}
                />
            ))}
        </div>
    );
}

export default function SessionPage({ params }: SessionPageProps) {
    const { lessonId } = use(params);
    return <SessionFlow lessonId={lessonId} />;
}
