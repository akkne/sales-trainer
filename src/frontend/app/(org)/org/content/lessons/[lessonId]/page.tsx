"use client";

import { useParams, useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { ApiError } from "@/shared/api/api-client";
import { Button } from "@/shared/components/button";
import { Card, CardContent, CardHeader, CardSkeleton } from "@/shared/components/card";
import { ConfirmDialog } from "@/shared/components/confirm-dialog";
import { ErrorState } from "@/shared/components/error-state";
import { Icon } from "@/shared/components/icon";
import { TextInput } from "@/shared/components/input";
import { Modal } from "@/shared/components/modal";
import { PageHeader } from "@/shared/components/page-header";
import { Skeleton } from "@/shared/components/skeleton";
import { AccuracySeriesChart } from "@/features/org-content-overrides/components/accuracy-series-chart";
import { ExerciseEditorModal } from "@/features/org-content-overrides/components/exercise-editor-modal";
import { ExerciseList } from "@/features/org-content-overrides/components/exercise-list";
import { PublishDialog } from "@/features/org-content-overrides/components/publish-dialog";
import { UnpublishedDraftBanner } from "@/features/org-content-overrides/components/unpublished-draft-banner";
import {
    LEAVE_WITH_DRAFT_BODY,
    LEAVE_WITH_DRAFT_TITLE,
    NOTHING_TO_PUBLISH_MESSAGE,
} from "@/features/org-content-overrides/constants/override-dictionary";
import {
    useContentOverrides,
    useCreateContentOverride,
} from "@/features/org-content-overrides/hooks/use-content-overrides";
import {
    useAdminLessons,
    useCreateExercise,
    useDeleteExercise,
    useEnsureLessonDraft,
    useLessonAccuracy,
    useLessonExercises,
    useLessonVersions,
    usePublishLessonVersion,
    useReorderExercises,
    useUpdateExercise,
    useUpdateLessonTitle,
} from "@/features/org-content-overrides/hooks/use-lesson-editor";
import type { AdminExercise, WriteExerciseRequest } from "@/features/org-content-overrides/types/lesson-editor";
import { moveExerciseInList } from "@/features/org-content-overrides/utils/exercise-summary";
import {
    describeLessonVersionState,
    resolveLessonVersionState,
} from "@/features/org-content-overrides/utils/lesson-draft";

const BACK_HREF = "/org/content";
const BACK_LABEL = "Контент";

/**
 * O19 «Редактор урока организации» (docs/TENANCY/ADMIN_UI_DESIGN.md O19).
 *
 * Two things this screen exists for. The first is that a lesson has no body of its own — everything
 * a customer wants to change lives in its exercises (docs/TENANCY/CONTENT_MODEL.md §0). The second
 * is decision 8 of the design: **publication is the end of editing, not a separate button**, because
 * editing without publishing binds the team's answers to the previous snapshot and says nothing.
 *
 * There is no `GET /admin/lessons/{id}` and no endpoint returns a lesson's `organizationId`, so
 * ownership is established by looking the lesson up in the organization's own override list. A
 * lesson that is the organization's but is not an override — one produced by the generation
 * pipeline — cannot be told from a global one that way; pressing «Сделать свою версию» on it
 * answers 409 «already an organization's own copy», and that answer is what unlocks editing. Both
 * halves are written down in docs/TESTING/ORG_PANEL.md.
 */
export default function OrganizationLessonEditorPage() {
    const routeParameters = useParams<{ lessonId: string }>();
    const lessonId = routeParameters?.lessonId ?? "";
    const router = useRouter();

    const lessonsQuery = useAdminLessons();
    const overridesQuery = useContentOverrides();
    const versionsQuery = useLessonVersions(lessonId);
    const exercisesQuery = useLessonExercises(lessonId);
    const accuracyQuery = useLessonAccuracy(lessonId);

    const ensureDraft = useEnsureLessonDraft(lessonId);
    const publishVersion = usePublishLessonVersion(lessonId);
    const createExercise = useCreateExercise(lessonId);
    const updateExercise = useUpdateExercise(lessonId);
    const deleteExercise = useDeleteExercise(lessonId);
    const reorderExercises = useReorderExercises(lessonId);
    const updateLessonTitle = useUpdateLessonTitle(lessonId);
    const createOverride = useCreateContentOverride();

    const [editedExercise, setEditedExercise] = useState<AdminExercise | null>(null);
    const [isExerciseModalOpen, setIsExerciseModalOpen] = useState(false);
    const [exerciseToDelete, setExerciseToDelete] = useState<AdminExercise | null>(null);
    const [isPublishOpen, setIsPublishOpen] = useState(false);
    const [publishNotice, setPublishNotice] = useState<string | null>(null);
    const [isLeaveConfirmOpen, setIsLeaveConfirmOpen] = useState(false);
    const [isEditableDespiteNotBeingAnOverride, setIsEditableDespiteNotBeingAnOverride] =
        useState(false);
    // Null means «не трогали»: the field then shows whatever the server last said, without an
    // effect racing a refetch to overwrite what is being typed.
    const [editedTitle, setEditedTitle] = useState<string | null>(null);
    const [writeFailure, setWriteFailure] = useState<string | null>(null);

    const lesson = useMemo(
        () => (lessonsQuery.data ?? []).find((candidate) => candidate.id === lessonId) ?? null,
        [lessonsQuery.data, lessonId]
    );

    const isOwnOverride = useMemo(
        () =>
            (overridesQuery.data ?? []).some(
                (override) => override.kind === "lessons" && override.overrideId === lessonId
            ),
        [overridesQuery.data, lessonId]
    );

    const isEditable = isOwnOverride || isEditableDespiteNotBeingAnOverride;

    const versionState = useMemo(
        () => resolveLessonVersionState(versionsQuery.data ?? []),
        [versionsQuery.data]
    );

    const exercises = useMemo(
        () => [...(exercisesQuery.data ?? [])].sort((left, right) => left.orderInLesson - right.orderInLesson),
        [exercisesQuery.data]
    );

    // The browser half of decision 8. The in-app half is the confirmation on the back control below;
    // this one covers the tab close, which no router can intercept.
    useEffect(() => {
        if (!versionState.hasUnpublishedDraft) return;

        const warnBeforeUnload = (unloadEvent: BeforeUnloadEvent) => {
            unloadEvent.preventDefault();
        };

        window.addEventListener("beforeunload", warnBeforeUnload);
        return () => window.removeEventListener("beforeunload", warnBeforeUnload);
    }, [versionState.hasUnpublishedDraft]);

    const leave = () => router.push(BACK_HREF);

    const goBack = () => {
        if (versionState.hasUnpublishedDraft) {
            setIsLeaveConfirmOpen(true);
            return;
        }
        leave();
    };

    /**
     * Every content write opens the draft first. Exercise rows are live rows; without a draft the
     * edit is real but invisible — the team keeps answering the last published snapshot and nothing
     * on screen says so. Opening the draft is idempotent, so doing it before each write costs one
     * request and makes the banner truthful.
     */
    const withDraft = async (write: () => Promise<unknown>) => {
        setWriteFailure(null);
        try {
            await ensureDraft.mutateAsync();
            await write();
            return true;
        } catch (error) {
            setWriteFailure(
                error instanceof ApiError && error.status === 403
                    ? "Этот урок принадлежит общей библиотеке — его правит Sellevate."
                    : "Не удалось сохранить. Попробуйте ещё раз."
            );
            return false;
        }
    };

    const saveExercise = async (body: WriteExerciseRequest) => {
        const saved = await withDraft(() =>
            editedExercise
                ? updateExercise.mutateAsync({ exerciseId: editedExercise.id, body })
                : createExercise.mutateAsync(body)
        );

        if (saved) {
            setIsExerciseModalOpen(false);
            setEditedExercise(null);
        }
    };

    /**
     * Q-8 (`docs/NIGHT_AUDIT_QUESTIONS.md`). One request carrying the whole new order, applied in a
     * single write transaction, replacing the loop of one `PUT /admin/exercises/{id}` per moved row.
     * The loop persisted correctly when every call succeeded; when one failed partway through it
     * left the lesson with duplicated positions and this screen with no way to say what the
     * administrator had actually asked for. The route also wants every exercise of the lesson, not
     * only the moved ones, which is why nothing is filtered down to "changed" rows here any more.
     */
    const moveExercise = async (fromIndex: number, toIndex: number) => {
        const reordered = moveExerciseInList(exercises, fromIndex, toIndex);

        await withDraft(() =>
            reorderExercises.mutateAsync(
                reordered.map((exercise) => ({
                    exerciseId: exercise.id,
                    orderInLesson: exercise.orderInLesson,
                }))
            )
        );
    };

    const publish = (isBreaking: boolean) => {
        setPublishNotice(null);
        publishVersion.mutate(isBreaking, {
            onSuccess: (result) => {
                if (result.createdNewVersion) {
                    setIsPublishOpen(false);
                    return;
                }
                setPublishNotice(NOTHING_TO_PUBLISH_MESSAGE);
            },
            onError: () => setPublishNotice("Опубликовать не удалось. Попробуйте ещё раз."),
        });
    };

    const makeOwnCopy = () => {
        createOverride.mutate(
            { kind: "lessons", baseId: lessonId },
            {
                onSuccess: (created) => router.replace(`/org/content/lessons/${created.overrideId}`),
                onError: (error) => {
                    // 409 here means the lesson already belongs to this organization — a generated
                    // one, most likely. That is not a failure, it is the answer that unlocks editing.
                    if (error instanceof ApiError && error.status === 409) {
                        setIsEditableDespiteNotBeingAnOverride(true);
                        return;
                    }
                    setWriteFailure("Не удалось создать свою версию. Попробуйте ещё раз.");
                },
            }
        );
    };

    if (lessonsQuery.isLoading || versionsQuery.isLoading || exercisesQuery.isLoading) {
        return (
            <>
                <PageHeader title="Урок" backHref={BACK_HREF} backLabel={BACK_LABEL} />
                <Skeleton height={28} width={280} />
                <div className="mt-6">
                    <CardSkeleton lines={5} showHeader={false} />
                </div>
            </>
        );
    }

    if (lessonsQuery.isError || versionsQuery.isError || exercisesQuery.isError) {
        return (
            <>
                <PageHeader title="Урок" backHref={BACK_HREF} backLabel={BACK_LABEL} />
                <ErrorState
                    message="Не удалось открыть урок."
                    onRetry={() => {
                        void lessonsQuery.refetch();
                        void versionsQuery.refetch();
                        void exercisesQuery.refetch();
                    }}
                />
            </>
        );
    }

    if (!lesson) {
        return (
            <>
                <PageHeader title="Урок" backHref={BACK_HREF} backLabel={BACK_LABEL} />
                <ErrorState
                    title="Урок не найден"
                    message="Такого урока нет в вашей библиотеке. Возможно, ссылка устарела."
                />
            </>
        );
    }

    const titleDraft = editedTitle ?? lesson.title;

    return (
        <>
            <button
                type="button"
                onClick={goBack}
                className="mb-3 inline-flex items-center gap-1.5 text-xs text-ink-3 transition-colors hover:text-ink"
            >
                <Icon name="arrow-left" size="sm" />
                {BACK_LABEL}
            </button>

            <PageHeader
                title={lesson.title}
                subtitle={`${lesson.topicTitle} · ${describeLessonVersionState(versionState, isEditable)}`}
            />

            {versionState.hasUnpublishedDraft && (
                <UnpublishedDraftBanner
                    publishedVersionNumber={versionState.latestPublished?.versionNumber ?? null}
                    isPublishing={publishVersion.isPending}
                    onPublish={() => {
                        setPublishNotice(null);
                        setIsPublishOpen(true);
                    }}
                />
            )}

            {!isEditable && (
                <div
                    role="status"
                    className="mb-6 flex flex-wrap items-start gap-3 rounded-2xl p-4"
                    style={{ background: "var(--bg-2)" }}
                >
                    <Icon name="lock" size="md" style={{ flexShrink: 0, marginTop: 2 }} />
                    <div className="min-w-0 flex-1">
                        <h2 className="text-sm font-medium text-ink">Урок из общей библиотеки</h2>
                        <p className="mt-1 text-sm text-ink-2">
                            Он открыт только на чтение. Своя копия появится ровно в тот момент, когда
                            вы нажмёте «Сделать свою версию» — и с этого момента обновления оригинала
                            перестанут приходить в неё автоматически.
                        </p>
                    </div>
                    <Button variant="primary" onClick={makeOwnCopy} disabled={createOverride.isPending}>
                        {createOverride.isPending ? "Создаём…" : "Сделать свою версию"}
                    </Button>
                </div>
            )}

            {writeFailure && (
                <p className="mb-4 text-sm text-bad" role="alert">
                    {writeFailure}
                </p>
            )}

            {isEditable && (
                <Card className="mb-6">
                    <CardContent>
                        <div className="flex flex-wrap items-end gap-3">
                            <div className="min-w-[240px] flex-1">
                                <TextInput
                                    label="Название урока"
                                    value={titleDraft}
                                    onChange={(changeEvent) => setEditedTitle(changeEvent.target.value)}
                                />
                            </div>
                            <Button
                                variant="secondary"
                                disabled={
                                    updateLessonTitle.isPending ||
                                    titleDraft.trim().length === 0 ||
                                    titleDraft === lesson.title
                                }
                                onClick={() =>
                                    void withDraft(() =>
                                        updateLessonTitle.mutateAsync({
                                            title: titleDraft.trim(),
                                            orderInTopic: lesson.orderInTopic,
                                        })
                                    ).then((saved) => {
                                        if (saved) setEditedTitle(null);
                                    })
                                }
                            >
                                Сохранить название
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            )}

            <Card className="mb-6">
                <CardHeader
                    title={`Упражнения (${exercises.length})`}
                    action={
                        isEditable && exercises.length > 0 ? (
                            <Button
                                variant="secondary"
                                onClick={() => {
                                    setEditedExercise(null);
                                    setIsExerciseModalOpen(true);
                                }}
                            >
                                + упражнение
                            </Button>
                        ) : undefined
                    }
                />
                <CardContent>
                    <ExerciseList
                        exercises={exercises}
                        readOnly={!isEditable}
                        isReordering={updateExercise.isPending}
                        onAdd={() => {
                            setEditedExercise(null);
                            setIsExerciseModalOpen(true);
                        }}
                        onEdit={(exercise) => {
                            setEditedExercise(exercise);
                            setIsExerciseModalOpen(true);
                        }}
                        onDelete={setExerciseToDelete}
                        onMove={(fromIndex, toIndex) => void moveExercise(fromIndex, toIndex)}
                    />
                </CardContent>
            </Card>

            <Card>
                <CardHeader title="Точность по версиям" />
                <CardContent>
                    {accuracyQuery.isLoading && <Skeleton height={160} />}
                    {accuracyQuery.isError && (
                        <ErrorState
                            compact
                            message="Не удалось посчитать точность по версиям. Сам урок это не затрагивает."
                            onRetry={() => void accuracyQuery.refetch()}
                        />
                    )}
                    {accuracyQuery.data && <AccuracySeriesChart series={accuracyQuery.data} />}
                </CardContent>
            </Card>

            <ExerciseEditorModal
                open={isExerciseModalOpen}
                exercise={editedExercise}
                nextOrderInLesson={exercises.length + 1}
                isPending={createExercise.isPending || updateExercise.isPending || ensureDraft.isPending}
                failureMessage={writeFailure}
                onCancel={() => {
                    setIsExerciseModalOpen(false);
                    setEditedExercise(null);
                }}
                onSave={(body) => void saveExercise(body)}
            />

            <ConfirmDialog
                open={exerciseToDelete !== null}
                title="Удалить упражнение?"
                body="Оно исчезнет из урока. Уже записанные ответы команды останутся привязаны к опубликованным версиям."
                confirmLabel="Удалить"
                tone="danger"
                isPending={deleteExercise.isPending}
                onCancel={() => setExerciseToDelete(null)}
                onConfirm={() => {
                    const exercise = exerciseToDelete;
                    if (!exercise) return;
                    void withDraft(() => deleteExercise.mutateAsync(exercise.id)).then((deleted) => {
                        if (deleted) setExerciseToDelete(null);
                    });
                }}
            />

            <PublishDialog
                open={isPublishOpen}
                isPending={publishVersion.isPending}
                notice={publishNotice}
                onCancel={() => setIsPublishOpen(false)}
                onConfirm={publish}
            />

            <Modal
                open={isLeaveConfirmOpen}
                onClose={() => setIsLeaveConfirmOpen(false)}
                title={LEAVE_WITH_DRAFT_TITLE}
                size="sm"
                footer={
                    <>
                        <Button
                            variant="ghost"
                            onClick={() => {
                                setIsLeaveConfirmOpen(false);
                                leave();
                            }}
                        >
                            Уйти без публикации
                        </Button>
                        <Button
                            variant="primary"
                            onClick={() => {
                                setIsLeaveConfirmOpen(false);
                                setPublishNotice(null);
                                setIsPublishOpen(true);
                            }}
                        >
                            Опубликовать сейчас
                        </Button>
                    </>
                }
            >
                <p className="text-sm text-ink-2">{LEAVE_WITH_DRAFT_BODY}</p>
            </Modal>
        </>
    );
}
