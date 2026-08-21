"use client";

import { Suspense, use, useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
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
import { useEnterAction } from "@/features/exercise/hooks/use-enter-action";
import { Icon } from "@/shared/components/icon";
import { ErrorState } from "@/shared/components/error-state";

const PASSING_SCORE_THRESHOLD = 7;

interface SessionPageProps {
    params: Promise<{ lessonId: string }>;
}

// "playing" — going through the active queue; "mistakes-intro" — the gate screen shown
// after the first pass when mistakes were made; "complete" — final results screen.
type SessionState = "playing" | "mistakes-intro" | "complete";

// "first" — the initial run over every exercise; "review" — the single mistakes-practice
// round over the exercises the user got wrong in the first pass.
type SessionPhase = "first" | "review";

interface SessionFlowProps {
    lessonId: string;
    exitHref: string;
}

// The exit action must always land on the lesson's skill/lesson list, never on whatever
// screen happened to be in browser history before this one (A-6). Callers that link into
// a session pass `?exit=<path>` pointing at where they consider "back" to be; unrecognised
// or missing values fall back to the skill tree.
function resolveExitHref(exitParam: string | null): string {
    return exitParam && exitParam.startsWith("/") ? exitParam : "/tree";
}

interface QueuedExercise {
    exercise: ExerciseData;
    queueKey: string;
}

function formatSessionDuration(totalSeconds: number): string {
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    if (minutes === 0) return `${seconds} сек`;
    return `${minutes} мин ${seconds} сек`;
}

function SessionFlow({ lessonId, exitHref }: SessionFlowProps) {
    const router = useRouter();
    const { data: exercises, isLoading } = useExercisesForLesson(lessonId);
    const submitExerciseMutation = useSubmitExercise();
    const sessionStartTimeRef = useRef<number>(Date.now());
    const sessionEndTimeRef = useRef<number>(0);
    const [lastSubmissionResult, setLastSubmissionResult] = useState<ExerciseSubmissionResult | null>(null);
    const [sessionState, setSessionState] = useState<SessionState>("playing");
    const [phase, setPhase] = useState<SessionPhase>("first");
    const [correctAnswerCount, setCorrectAnswerCount] = useState(0);
    const [exerciseQueue, setExerciseQueue] = useState<QueuedExercise[]>([]);
    const [reviewQueue, setReviewQueue] = useState<QueuedExercise[]>([]);
    // Exercises answered incorrectly during the first pass — replayed once in the review phase.
    const [mistakeExercises, setMistakeExercises] = useState<ExerciseData[]>([]);
    const [currentQueueIndex, setCurrentQueueIndex] = useState(0);

    useEffect(() => {
        if (exercises && exercises.length > 0 && exerciseQueue.length === 0) {
            const initialQueue: QueuedExercise[] = exercises.map((ex) => ({
                exercise: ex,
                queueKey: `${ex.exerciseId}-first`,
            }));
            setExerciseQueue(initialQueue);
        }
    }, [exercises, exerciseQueue.length]);

    const activeQueue = phase === "review" ? reviewQueue : exerciseQueue;
    const currentQueued = activeQueue[currentQueueIndex];
    const currentExercise = currentQueued?.exercise;
    const originalExerciseCount = exercises?.length ?? 0;
    const progressPercent = activeQueue.length > 0 ? Math.round((currentQueueIndex / activeQueue.length) * 100) : 0;

    function handleExerciseSubmit(answer: unknown) {
        if (!currentExercise || !currentQueued) return;
        submitExerciseMutation.mutate(
            { exerciseId: currentExercise.exerciseId, answer },
            {
                onSuccess: (result) => {
                    setLastSubmissionResult(result);
                    const isPassing = result.isCorrect || (result.score !== undefined && result.score >= PASSING_SCORE_THRESHOLD * 10);

                    if (isPassing) {
                        // Accuracy reflects the first pass only.
                        if (phase === "first") {
                            setCorrectAnswerCount((prev) => prev + 1);
                        }
                    } else if (phase === "first") {
                        // Queue the exercise for the single end-of-lesson review round (dedupe).
                        setMistakeExercises((prev) =>
                            prev.some((ex) => ex.exerciseId === currentExercise.exerciseId)
                                ? prev
                                : [...prev, currentExercise]
                        );
                    }
                },
            }
        );
    }

    function recordSessionEnd() {
        sessionEndTimeRef.current = Date.now();
    }

    // Advance to the next exercise in the active queue, or finish the current phase:
    // after the first pass, go to the mistakes-intro gate if any mistakes were made;
    // after the review round (or a clean first pass), show the completion screen.
    function advanceToNext() {
        // R-4: `submitExerciseMutation.error` is sticky until the next `mutate()` or an explicit
        // `reset()` — without this, an error raised on one exercise rode along onto every
        // following exercise (skip, or the next unrelated submit) until a submit finally
        // succeeded again.
        submitExerciseMutation.reset();
        if (currentQueueIndex + 1 < activeQueue.length) {
            setCurrentQueueIndex((prev) => prev + 1);
            return;
        }
        if (phase === "first" && mistakeExercises.length > 0) {
            setSessionState("mistakes-intro");
            return;
        }
        recordSessionEnd();
        setSessionState("complete");
    }

    function handleSkip() {
        advanceToNext();
    }

    function handleContinueAfterResult() {
        setLastSubmissionResult(null);
        advanceToNext();
    }

    function handleStartMistakesReview() {
        setReviewQueue(
            mistakeExercises.map((ex) => ({ exercise: ex, queueKey: `${ex.exerciseId}-review` }))
        );
        setPhase("review");
        setCurrentQueueIndex(0);
        setLastSubmissionResult(null);
        setSessionState("playing");
    }

    if (isLoading) {
        return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", background: "var(--bg)" }}>
                <div style={{ width: 40, height: 40, borderRadius: "50%", border: "4px solid var(--primary)", borderTopColor: "transparent", animation: "spin 0.8s linear infinite" }} />
            </div>
        );
    }

    // E-11: an empty (but successfully loaded) exercise list must not spin forever — the queue
    // only ever fills from a non-empty `exercises`, so `exerciseQueue.length === 0` would
    // otherwise never resolve.
    if (!exercises || exercises.length === 0) {
        return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", color: "var(--ink-3)", background: "var(--bg)" }}>
                Упражнения не найдены
            </div>
        );
    }

    if (exerciseQueue.length === 0) {
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
                accuracyPercent={accuracyPercent}
                durationSeconds={sessionDurationSeconds}
                onBack={() => router.push(exitHref)}
            />
        );
    }

    // Gate shown after the first pass when the user made mistakes: explain the review
    // round, then reveal the exercises they got wrong.
    if (sessionState === "mistakes-intro") {
        return (
            <MistakesIntroScreen
                mistakeCount={mistakeExercises.length}
                onStart={handleStartMistakesReview}
            />
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
            {/* Header: ✕ close + violet gradient progress bar */}
            <div className="session-top">
                <button
                    className="icon-btn"
                    onClick={() => router.push(exitHref)}
                    aria-label="Выйти"
                    style={{ flex: "none" }}
                >
                    <Icon name="close" size={20} />
                </button>

                <div className="grow">
                    <div className="session-prog-track" role="progressbar" aria-valuenow={progressPercent} aria-valuemin={0} aria-valuemax={100}>
                        <div className="session-prog-fill" style={{ width: `${progressPercent}%` }} />
                    </div>
                </div>

                {phase === "review" && (
                    <span
                        className="eyebrow"
                        style={{ flex: "none", color: "var(--amber)", whiteSpace: "nowrap" }}
                    >
                        <Icon name="target" size={14} />
                        Работа над ошибками
                    </span>
                )}
            </div>

            {/* Exercise content */}
            <div
                key={currentQueued.queueKey}
                className="session-body"
                style={{
                    overflowY: "auto",
                    // The AI-feedback banner is 42dvh of review card plus ~120px of chrome,
                    // so a flat 320px of runway left the bottom of the exercise (often the
                    // user's own answer) stuck behind it on taller screens.
                    padding: lastSubmissionResult?.aiFeedback
                        ? "48px 24px calc(42dvh + 120px)"
                        : "48px 24px 180px",
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
                            submitError={submitExerciseMutation.error}
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
                            submitError={submitExerciseMutation.error}
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
                            submitError={submitExerciseMutation.error}
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
                            submitError={submitExerciseMutation.error}
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
                            submitError={submitExerciseMutation.error}
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
                            submitError={submitExerciseMutation.error}
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
                            submitError={submitExerciseMutation.error}
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
                            submitError={submitExerciseMutation.error}
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
                            submitError={submitExerciseMutation.error}
                        />
                    )}
                </div>
            </div>
        </div>
    );
}

const CONFETTI_COLORS = ["var(--primary)", "var(--violet)", "var(--flame)", "var(--success)", "var(--amber)"];

// Deterministic pseudo-random so SSR and client render identically
function seeded(i: number, salt: number): number {
    const x = Math.sin(i * 12.9898 + salt * 78.233) * 43758.5453;
    return x - Math.floor(x);
}

function Confetti() {
    const pieces = Array.from({ length: 70 }).map((_, i) => {
        const left = seeded(i, 1) * 100;
        const drift = (seeded(i, 2) - 0.5) * 160; // px horizontal sway
        const delay = seeded(i, 3) * 1.2;
        const duration = 2.4 + seeded(i, 4) * 2.2;
        const rotate = 360 + Math.round(seeded(i, 5) * 720);
        const size = 7 + Math.round(seeded(i, 6) * 7);
        const circle = seeded(i, 7) > 0.62;
        return (
            <span
                key={i}
                className={circle ? "confetti-circle" : undefined}
                style={{
                    left: `${left}%`,
                    width: size,
                    height: circle ? size : size * 1.5,
                    background: CONFETTI_COLORS[i % CONFETTI_COLORS.length],
                    // @ts-expect-error CSS custom properties
                    "--cf-x": `${drift}px`,
                    "--cf-rot": `${rotate}deg`,
                    animationDelay: `${delay}s`,
                    animationDuration: `${duration}s`,
                }}
            />
        );
    });
    return <div className="confetti" aria-hidden="true">{pieces}</div>;
}

interface CompletionScreenProps {
    accuracyPercent?: number;
    durationSeconds: number;
    onBack: () => void;
    eyebrow?: string;
    heading?: string;
}

function CompletionScreen({ accuracyPercent, durationSeconds, onBack, eyebrow = "Урок завершён", heading = "Отличная работа!" }: CompletionScreenProps) {
    // Enter presses the primary CTA ("Вернуться к пути").
    useEnterAction(onBack);

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

                {/* Stat grid — accuracy / time */}
                <div className="complete-stats">
                    {accuracyPercent !== undefined && (
                        <div className="cs">
                            <Icon name="target" size={22} style={{ color: "var(--success)" }} />
                            <b>{accuracyPercent}%</b>
                            <span>точность</span>
                        </div>
                    )}
                    {durationSeconds > 0 && (
                        <div className="cs">
                            <Icon name="clock" size={22} style={{ color: "var(--violet)" }} />
                            <b>{formatSessionDuration(durationSeconds)}</b>
                            <span>время</span>
                        </div>
                    )}
                </div>

                {/* Primary CTA */}
                <button
                    className="btn btn-primary btn-lg btn-block"
                    style={{ marginTop: 28 }}
                    onClick={onBack}
                >
                    Вернуться к пути
                    <Icon name="arrow-right" size={18} />
                </button>
            </div>
        </div>
    );
}

interface MistakesIntroScreenProps {
    mistakeCount: number;
    onStart: () => void;
}

function MistakesIntroScreen({ mistakeCount, onStart }: MistakesIntroScreenProps) {
    // Enter presses the primary CTA ("Начать работу над ошибками").
    useEnterAction(onStart);

    const exercisesWord =
        mistakeCount % 10 === 1 && mistakeCount % 100 !== 11
            ? "упражнении"
            : "упражнениях";

    return (
        <div className="complete">
            <div className="complete-inner">
                <div className="check-circle" style={{ background: "var(--amber)" }}>
                    <Icon name="target" size={44} color="#fff" />
                </div>

                <div className="eyebrow" style={{ justifyContent: "center", marginBottom: 8 }}>
                    Работа над ошибками
                </div>
                <h1 className="h1" style={{ margin: "0 0 16px", fontSize: 26, letterSpacing: "-0.02em" }}>
                    Теперь обработаем ошибки
                </h1>
                <p style={{ margin: "0 0 28px", color: "var(--ink-3)", lineHeight: 1.5 }}>
                    Вы прошли все упражнения! В {mistakeCount} {exercisesWord} были ошибки — давайте
                    разберём их ещё раз, чтобы закрепить материал.
                </p>

                <button className="btn btn-primary btn-lg btn-block" onClick={onStart}>
                    Начать работу над ошибками
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
 * Theory lesson flow: swipe through story cards. Reaching the end submits every
 * card (each records a correct attempt) so the backend's all-exercises-passed gate
 * marks the lesson complete — then shows the completion screen.
 */
function TheoryLessonFlow({ exercises, exitHref }: { exercises: ExerciseData[]; exitHref: string }) {
    const router = useRouter();
    const submitExerciseMutation = useSubmitExercise();
    const startTimeRef = useRef<number>(0);
    const [completed, setCompleted] = useState(false);
    const [durationSeconds, setDurationSeconds] = useState(0);
    const [completeError, setCompleteError] = useState<string | null>(null);

    useEffect(() => {
        startTimeRef.current = Date.now();
    }, []);

    const cards = exercises.map((ex) => ex.content as TheoryCardContent);

    async function handleComplete() {
        setCompleteError(null);
        try {
            // The backend marks a lesson complete only when every exercise in it has a
            // correct attempt. Theory cards always evaluate as correct, so we must submit
            // ALL cards (not just the last one) or a multi-card lesson never completes.
            for (const exercise of exercises) {
                await submitExerciseMutation.mutateAsync({
                    exerciseId: exercise.exerciseId,
                    answer: {},
                });
            }
            setDurationSeconds(Math.round((Date.now() - startTimeRef.current) / 1000));
            setCompleted(true);
        } catch (submitError) {
            // A mid-loop failure must not be silent: without this the screen just goes
            // back to "Завершить" with no explanation, and the lesson never closes
            // because the backend never saw every card submitted (docs/AUDIT_SILENT_WRITES.md W-3).
            setCompleteError(
                submitError instanceof Error
                    ? submitError.message
                    : "Не удалось сохранить прогресс. Попробуй ещё раз."
            );
        }
    }

    if (completed) {
        return (
            <CompletionScreen
                durationSeconds={durationSeconds}
                onBack={() => router.push(exitHref)}
                eyebrow="Теория пройдена"
                heading="Теперь ты знаешь больше!"
            />
        );
    }

    return (
        <TheoryLessonPlayer
            cards={cards}
            onComplete={handleComplete}
            isCompleting={submitExerciseMutation.isPending}
            completeError={completeError}
            onExit={() => router.push(exitHref)}
        />
    );
}

function SessionRouter({ lessonId }: { lessonId: string }) {
    const router = useRouter();
    const { data: exercises, isLoading, isLoadingError, refetch } = useExercisesForLesson(lessonId);
    const searchParams = useSearchParams();
    const exitHref = resolveExitHref(searchParams.get("exit"));

    if (isLoading) return <SessionLoader />;

    // E-11: a failed exercises fetch used to leave isLoading===false and exercises===undefined,
    // which both loading gates in this file treat as "still loading" — the spinner then never
    // stops, and there is no way out of the screen. Show the error and a way back instead.
    // R-5: gate on `isLoadingError` (first load failed, no data) rather than bare `isError` —
    // a background refetch failing while the lesson is already loaded (isRefetchError) must not
    // unmount the whole in-progress SessionFlow/TheoryLessonFlow and discard queue/timer state.
    if (isLoadingError || !exercises) {
        return (
            <div style={{ display: "flex", flexDirection: "column", minHeight: "100vh", background: "var(--bg)" }}>
                <div className="session-top">
                    <button
                        className="icon-btn"
                        onClick={() => router.push(exitHref)}
                        aria-label="Выйти"
                        style={{ flex: "none" }}
                    >
                        <Icon name="close" size={20} />
                    </button>
                </div>
                <div style={{ flex: 1, display: "flex", alignItems: "center", justifyContent: "center" }}>
                    <ErrorState
                        title="Не удалось загрузить урок"
                        message="Проверь подключение и попробуй снова."
                        onRetry={() => refetch()}
                    />
                </div>
            </div>
        );
    }

    const isTheoryLesson =
        exercises.length > 0 && exercises.every((ex) => ex.type === ExerciseTypes.TheoryCard);

    if (isTheoryLesson) return <TheoryLessonFlow exercises={exercises} exitHref={exitHref} />;

    return <SessionFlow lessonId={lessonId} exitHref={exitHref} />;
}

export default function SessionPage({ params }: SessionPageProps) {
    const { lessonId } = use(params);
    return (
        <Suspense fallback={<SessionLoader />}>
            <SessionRouter lessonId={lessonId} />
        </Suspense>
    );
}
