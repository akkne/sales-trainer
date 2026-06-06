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
import { StatTile } from "@/shared/components/stat-tile";

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
                <div style={{ width: 40, height: 40, borderRadius: "50%", border: "4px solid var(--indigo)", borderTopColor: "transparent", animation: "spin 0.8s linear infinite" }} />
            </div>
        );
    }

    // Completion screen
    if (sessionState === "complete") {
        const sessionDurationSeconds = Math.round((sessionEndTimeRef.current - sessionStartTimeRef.current) / 1000);
        const accuracyPercent = originalExerciseCount > 0 ? Math.round((correctAnswerCount / originalExerciseCount) * 100) : 100;

        return (
            <div style={{ minHeight: "100vh", background: "var(--bg)", position: "relative", overflow: "hidden" }}>
                {/* Confetti */}
                <Confetti />

                {/* Header */}
                <div style={{ padding: "20px 32px", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                    <div style={{ width: 22 }} />
                    <button
                        onClick={() => router.back()}
                        style={{ background: "transparent", border: "none", cursor: "pointer", color: "var(--ink-3)", padding: 6 }}
                    >
                        <Icon name="close" size={22} />
                    </button>
                </div>

                {/* Content */}
                <div style={{ maxWidth: 640, margin: "0 auto", padding: "40px 32px", textAlign: "center" }}>
                    <div
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            justifyContent: "center",
                            width: 120,
                            height: 120,
                            borderRadius: "50%",
                            background: "var(--olive-soft)",
                            color: "var(--olive)",
                            marginBottom: 24,
                            position: "relative",
                        }}
                    >
                        <div
                            style={{
                                position: "absolute",
                                inset: -12,
                                borderRadius: "50%",
                                background: "var(--olive-soft)",
                                opacity: 0.5,
                                animation: "pulse 2s ease-in-out infinite",
                            }}
                        />
                        <Icon name="check" size={56} />
                    </div>

                    <div
                        style={{
                            fontSize: 12,
                            color: "var(--olive)",
                            letterSpacing: 2,
                            textTransform: "uppercase",
                            marginBottom: 10,
                            fontWeight: 500,
                            fontFamily: "var(--f-mono)",
                        }}
                    >
                        УРОК ЗАВЕРШЁН
                    </div>

                    <h1 style={{ fontSize: 48, margin: 0, letterSpacing: -2, fontWeight: 500, lineHeight: 1 }}>
                        Отличная работа!
                    </h1>

                    <p style={{ fontSize: 17, color: "var(--ink-3)", marginTop: 16, maxWidth: 480, margin: "16px auto 40px" }}>
                        Завтра раскройте ту же технику на холодном клиенте. Стрик продолжается.
                    </p>

                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 32 }}>
                        <StatTile big label="XP получено" value={`+${totalXpEarned}`} icon={<Icon name="bolt" size="xs" />} tone="indigo" />
                        <StatTile big label="Точность" value={accuracyPercent} unit="%" icon={<Icon name="target" size="xs" />} tone="olive" />
                        <StatTile big label="Время" value={formatSessionDuration(sessionDurationSeconds)} icon={<Icon name="clock" size="xs" />} />
                        <StatTile big label="Жизни" value={`${hearts}/4`} icon={<Icon name="heart" size="xs" />} tone="rust" />
                    </div>

                    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                        <Button variant="accent" size="xl" fullWidth iconRightName="arrow-right" onClick={() => router.back()}>
                            Вернуться к пути
                        </Button>
                    </div>
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
        <div style={{ minHeight: "100vh", background: "var(--bg)", display: "flex", flexDirection: "column" }}>
            <AchievementToastQueue queue={toastQueue} onDismiss={dismissToast} />

            {/* Header */}
            <div
                style={{
                    padding: "16px 32px",
                    display: "flex",
                    alignItems: "center",
                    gap: 20,
                    borderBottom: "1px solid var(--line)",
                    background: "var(--surface)",
                }}
            >
                <button
                    onClick={() => router.back()}
                    style={{ background: "transparent", border: "none", cursor: "pointer", color: "var(--ink-3)", padding: 6 }}
                    aria-label="Выйти"
                >
                    <Icon name="close" size={22} />
                </button>

                <div style={{ flex: 1 }}>
                    <Progress value={progressPercent} max={100} tone="indigo" height={8} />
                    <div style={{ marginTop: 6, display: "flex", justifyContent: "space-between", fontSize: 11, color: "var(--ink-3)", fontFamily: "var(--f-mono)" }}>
                        <span>УРОК</span>
                        <span>{currentQueueIndex + 1}/{exerciseQueue.length}</span>
                    </div>
                </div>

                {/* Hearts */}
                <div style={{ display: "flex", gap: 4 }}>
                    {Array.from({ length: 4 }).map((_, i) => (
                        <div key={i} style={{ width: 24, height: 24, display: "flex", alignItems: "center", justifyContent: "center" }}>
                            <Icon name="heart" size={20} color={i < hearts ? "var(--rust)" : "var(--line-2)"} />
                        </div>
                    ))}
                </div>
            </div>

            {/* Exercise content */}
            <div
                key={currentQueued.queueKey}
                style={{
                    flex: 1,
                    overflowY: "auto",
                    padding: lastSubmissionResult?.aiFeedback ? "48px 24px 320px" : "48px 24px 180px",
                    display: "flex",
                    alignItems: "flex-start",
                    justifyContent: "center",
                }}
            >
                <div style={{ maxWidth: 820, width: "100%" }}>
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

function Confetti() {
    const pieces = Array.from({ length: 36 }).map((_, i) => ({
        left: Math.random() * 100,
        delay: Math.random() * 0.4,
        dur: 1.5 + Math.random() * 1.5,
        rot: Math.random() * 360,
        color: ["var(--indigo)", "var(--olive)", "var(--rust)", "var(--clay)"][i % 4],
        w: 6 + Math.random() * 6,
    }));

    return (
        <div style={{ position: "absolute", inset: 0, pointerEvents: "none", overflow: "hidden" }}>
            {pieces.map((p, i) => (
                <div
                    key={i}
                    style={{
                        position: "absolute",
                        left: `${p.left}%`,
                        top: -20,
                        width: p.w,
                        height: p.w,
                        background: p.color,
                        transform: `rotate(${p.rot}deg)`,
                        animation: `confetti ${p.dur}s cubic-bezier(.3,0,.7,1) ${p.delay}s forwards`,
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
