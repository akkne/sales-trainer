"use client";

import { use, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
    useExercisesForLesson,
    useSubmitExercise,
    type ExerciseSubmissionResult,
    type ExerciseData,
} from "@/features/exercise/hooks/use-lesson";
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
import { TheoryLessonPlayer } from "@/features/exercise/components/theory-lesson-player";
import type { TheoryCardContent } from "@/features/exercise/types/theory-card";
import { Icon } from "@/shared/components/icon";

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
    if (minutes === 0) return `${seconds} sec`;
    return `${minutes} min ${seconds} sec`;
}

function SessionFlow({ lessonId }: SessionFlowProps) {
    const router = useRouter();
    const { data: exercises, isLoading } = useExercisesForLesson(lessonId);
    const submitExerciseMutation = useSubmitExercise();
    const sessionStartTimeRef = useRef<number>(Date.now());
    const sessionEndTimeRef = useRef<number>(0);
    const [lastSubmissionResult, setLastSubmissionResult] = useState<ExerciseSubmissionResult | null>(null);
    const [sessionState, setSessionState] = useState<SessionState>("playing");
    const [totalXpEarned, setTotalXpEarned] = useState(0);
    const [correctAnswerCount, setCorrectAnswerCount] = useState(0);
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
                    } else {
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
            <CompletionScreen
                xp={totalXpEarned}
                accuracyPercent={accuracyPercent}
                durationSeconds={sessionDurationSeconds}
                onBack={() => router.back()}
            />
        );
    }

    if (!currentExercise) {
        return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", color: "var(--ink-3)", background: "var(--bg)" }}>
                No exercises found
            </div>
        );
    }

    return (
        <div className="session">
            {/* Header: ✕ close + violet gradient progress bar */}
            <div className="session-top">
                <button
                    className="icon-btn"
                    onClick={() => router.back()}
                    aria-label="Exit"
                    style={{ flex: "none" }}
                >
                    <Icon name="close" size={20} />
                </button>

                <div className="grow">
                    <div className="session-prog-track" role="progressbar" aria-valuenow={progressPercent} aria-valuemin={0} aria-valuemax={100}>
                        <div className="session-prog-fill" style={{ width: `${progressPercent}%` }} />
                    </div>
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
                <div className="exercise" style={{ maxWidth: 900 }}>
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

interface CompletionScreenProps {
    xp: number;
    accuracyPercent?: number;
    durationSeconds: number;
    onBack: () => void;
    eyebrow?: string;
    heading?: string;
}

function CompletionScreen({ xp, accuracyPercent, durationSeconds, onBack, eyebrow = "Lesson complete", heading = "Great work!" }: CompletionScreenProps) {
    return (
        <div className="complete">
            <Confetti />
            <div className="complete-inner">
                {/* Animated success ring */}
                <div className="check-circle">
                    <Icon name="check" size={44} color="#fff" />
                </div>

                {/* Eyebrow + heading */}
                <div
                    className="eyebrow"
                    style={{ justifyContent: "center", marginBottom: 8 }}
                >
                    {eyebrow}
                </div>
                <h1 className="h1" style={{ margin: "0 0 28px", fontSize: 26, letterSpacing: "-0.02em" }}>
                    {heading}
                </h1>

                {/* Stat grid — XP / accuracy / time (NO hearts) */}
                <div className="complete-stats">
                    <div className="cs">
                        <Icon name="bolt" size={22} style={{ color: "var(--primary)" }} />
                        <b>+{xp}</b>
                        <span>XP earned</span>
                    </div>
                    {accuracyPercent !== undefined && (
                        <div className="cs">
                            <Icon name="target" size={22} style={{ color: "var(--success)" }} />
                            <b>{accuracyPercent}%</b>
                            <span>accuracy</span>
                        </div>
                    )}
                    {durationSeconds > 0 && (
                        <div className="cs">
                            <Icon name="clock" size={22} style={{ color: "var(--violet)" }} />
                            <b>{formatSessionDuration(durationSeconds)}</b>
                            <span>time</span>
                        </div>
                    )}
                </div>

                {/* Primary CTA */}
                <button
                    className="btn btn-primary btn-lg btn-block"
                    style={{ marginTop: 28 }}
                    onClick={onBack}
                >
                    Back to path
                    <Icon name="arrow-right" size={18} />
                </button>
            </div>
        </div>
    );
}

function SessionLoader() {
    return (
        <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", background: "var(--bg)" }}>
            <div style={{ width: 40, height: 40, borderRadius: "50%", border: "4px solid var(--primary)", borderTopColor: "transparent", animation: "spin 0.8s linear infinite" }} />
        </div>
    );
}

/**
 * Theory lesson flow: swipe through story cards. Reaching the end submits the last
 * card once (the only graded-shaped call) so the lesson is marked complete on the
 * backend and the fixed theory XP is awarded — then shows the completion screen.
 */
function TheoryLessonFlow({ exercises }: { exercises: ExerciseData[] }) {
    const router = useRouter();
    const submitExerciseMutation = useSubmitExercise();
    const startTimeRef = useRef<number>(0);
    const [completed, setCompleted] = useState(false);
    const [xpEarned, setXpEarned] = useState(0);
    const [durationSeconds, setDurationSeconds] = useState(0);

    useEffect(() => {
        startTimeRef.current = Date.now();
    }, []);

    const cards = exercises.map((ex) => ex.content as TheoryCardContent);

    function handleComplete() {
        const lastCard = exercises[exercises.length - 1];
        submitExerciseMutation.mutate(
            { exerciseId: lastCard.exerciseId, answer: {} },
            {
                onSuccess: (result) => {
                    setXpEarned(result.xpEarned);
                    setDurationSeconds(Math.round((Date.now() - startTimeRef.current) / 1000));
                    setCompleted(true);
                },
            }
        );
    }

    if (completed) {
        return (
            <CompletionScreen
                xp={xpEarned}
                durationSeconds={durationSeconds}
                onBack={() => router.back()}
                eyebrow="Theory complete"
                heading="Now you know more!"
            />
        );
    }

    return (
        <TheoryLessonPlayer
            cards={cards}
            onComplete={handleComplete}
            isCompleting={submitExerciseMutation.isPending}
            onExit={() => router.back()}
        />
    );
}

function SessionRouter({ lessonId }: { lessonId: string }) {
    const { data: exercises, isLoading } = useExercisesForLesson(lessonId);

    if (isLoading || !exercises) return <SessionLoader />;

    const isTheoryLesson =
        exercises.length > 0 && exercises.every((ex) => ex.type === ExerciseTypes.TheoryCard);

    if (isTheoryLesson) return <TheoryLessonFlow exercises={exercises} />;

    return <SessionFlow lessonId={lessonId} />;
}

export default function SessionPage({ params }: SessionPageProps) {
    const { lessonId } = use(params);
    return <SessionRouter lessonId={lessonId} />;
}
