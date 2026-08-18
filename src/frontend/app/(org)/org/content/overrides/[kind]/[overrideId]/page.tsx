"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { Button } from "@/shared/components/button";
import { Card, CardContent, CardSkeleton } from "@/shared/components/card";
import { ConfirmDialog } from "@/shared/components/confirm-dialog";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { DialogModePromptEditor } from "@/features/org-content-overrides/components/dialog-mode-prompt-editor";
import { OverrideStateBadge } from "@/features/org-content-overrides/components/override-state-badge";
import {
    ThreeWayCompare,
    type CompareColumn,
} from "@/features/org-content-overrides/components/three-way-compare";
import {
    NO_AUTO_MERGE_NOTICE,
    NO_BASE_AT_FORK_NOTICE,
    PLATFORM_PANEL_EDIT_HREFS,
    PLATFORM_PANEL_EDIT_NOTICE,
    describeOverrideKind,
    describeOverrideState,
    resolveOverrideState,
} from "@/features/org-content-overrides/constants/override-dictionary";
import {
    useAcceptBase,
    useAcceptDialogModeBase,
    useContentOverrideReview,
    useDialogModeOverrideReview,
    useKeepDialogModeOverride,
    useKeepOverride,
    useUpdateDialogModePrompts,
} from "@/features/org-content-overrides/hooks/use-content-overrides";
import { useLessonVersions } from "@/features/org-content-overrides/hooks/use-lesson-editor";
import {
    isOverrideKind,
    type LearningOverrideKind,
} from "@/features/org-content-overrides/types/content-override";

const BACK_HREF = "/org/content/overrides";
const BACK_LABEL = "Свои версии";

const ACCEPT_BASE_CONFIRM_BODY =
    "Ваша версия уйдёт в архив (не удалится — на неё ссылаются записи о прохождении). Команда снова будет видеть общий материал.";

/**
 * O15 «Разбор» (docs/TENANCY/ADMIN_UI_DESIGN.md O15).
 *
 * Three columns and three actions, and **no fourth action**: there is no «слить автоматически»
 * button because the model has no merge, on purpose (docs/TENANCY/CONTENT_MODEL.md §2.6). Nothing
 * on this screen computes a diff either — the server returns whole documents and the screen shows
 * them side by side.
 */
export default function OrganizationContentOverrideReviewPage() {
    const routeParameters = useParams<{ kind: string; overrideId: string }>();
    const kind = routeParameters?.kind ?? "";
    const overrideId = routeParameters?.overrideId ?? "";

    if (!isOverrideKind(kind)) {
        return (
            <>
                <PageHeader title="Разбор" backHref={BACK_HREF} backLabel={BACK_LABEL} />
                <EmptyState
                    icon="warning"
                    title="Неизвестный тип материала"
                    description="Такого раздела нет. Вернитесь к списку своих версий и откройте нужную копию оттуда."
                />
            </>
        );
    }

    return kind === "modes" ? (
        <DialogModeOverrideReviewScreen overrideId={overrideId} />
    ) : (
        <LearningOverrideReviewScreen kind={kind} overrideId={overrideId} />
    );
}

function ReviewSkeleton() {
    return (
        <>
            <PageHeader title="Разбор" backHref={BACK_HREF} backLabel={BACK_LABEL} />
            <CardSkeleton lines={6} showHeader={false} />
        </>
    );
}

function NoAutoMergeNotice() {
    return (
        <p className="mt-6 rounded-xl bg-bg-2 p-4 text-sm text-ink-2">{NO_AUTO_MERGE_NOTICE}</p>
    );
}

function LearningOverrideReviewScreen({
    kind,
    overrideId,
}: {
    kind: LearningOverrideKind;
    overrideId: string;
}) {
    const router = useRouter();
    const reviewQuery = useContentOverrideReview(kind, overrideId);
    const acceptBase = useAcceptBase();
    const keepOverride = useKeepOverride();
    const [isConfirmingAcceptBase, setIsConfirmingAcceptBase] = useState(false);

    const summary = reviewQuery.data?.summary ?? null;

    // Version numbers for the column headings live on the base lesson's own history, not in the
    // review payload — it carries version ids only. Lessons only: the other families have none.
    const baseVersionsQuery = useLessonVersions(
        summary?.baseId ?? "",
        kind === "lessons" && Boolean(summary?.baseId)
    );

    const versionNumberById = useMemo(() => {
        const lookup = new Map<string, number>();
        for (const version of baseVersionsQuery.data ?? []) lookup.set(version.id, version.versionNumber);
        return lookup;
    }, [baseVersionsQuery.data]);

    const describeVersion = (versionId: string | null, fallback: string) => {
        if (versionId === null) return fallback;
        const versionNumber = versionNumberById.get(versionId);
        return versionNumber === undefined ? fallback : `версия ${versionNumber}`;
    };

    if (reviewQuery.isLoading) return <ReviewSkeleton />;

    if (reviewQuery.isError || !reviewQuery.data || !summary) {
        return (
            <>
                <PageHeader title="Разбор" backHref={BACK_HREF} backLabel={BACK_LABEL} />
                <ErrorState
                    message="Не удалось прочитать эту копию. Возможно, её уже разобрали в другой вкладке."
                    onRetry={() => void reviewQuery.refetch()}
                />
            </>
        );
    }

    const review = reviewQuery.data;
    const state = resolveOverrideState(summary);
    const hasBaseAtFork = review.baseAtFork !== null && review.baseAtFork !== undefined;

    const columns: CompareColumn[] = [
        ...(hasBaseAtFork
            ? [
                  {
                      key: "base-at-fork",
                      title: "База на момент копирования",
                      subtitle: describeVersion(summary.forkedFrom, "точка форка"),
                      document: review.baseAtFork,
                  },
              ]
            : []),
        {
            key: "override",
            title: "Ваша версия",
            subtitle: "с вашими правками",
            document: review.override,
        },
        {
            key: "base-current",
            title: "База сейчас",
            subtitle: describeVersion(summary.baseCurrent, "текущий оригинал"),
            document: review.baseCurrent,
        },
    ];

    const editHref =
        kind === "lessons"
            ? `/org/content/lessons/${summary.overrideId}`
            : PLATFORM_PANEL_EDIT_HREFS[kind];

    const isActing = acceptBase.isPending || keepOverride.isPending;

    return (
        <>
            <PageHeader
                title={summary.title}
                subtitle={`${describeOverrideKind(kind)} · ${describeOverrideState(state).label}`}
                backHref={BACK_HREF}
                backLabel={BACK_LABEL}
                action={<OverrideStateBadge state={state} />}
            />

            <p className="mb-4 text-sm text-ink-3">{describeOverrideState(state).hint}</p>

            <Card>
                <CardContent>
                    <ThreeWayCompare
                        columns={columns}
                        missingBaseAtForkNotice={hasBaseAtFork ? undefined : NO_BASE_AT_FORK_NOTICE}
                    />
                </CardContent>
            </Card>

            <NoAutoMergeNotice />

            <div className="mt-6 flex flex-wrap gap-3">
                <div>
                    <Button
                        variant="secondary"
                        disabled={isActing}
                        onClick={() => setIsConfirmingAcceptBase(true)}
                    >
                        Взять базу
                    </Button>
                    <p className="mt-1 max-w-[200px] text-xs text-ink-3">
                        ваша копия уйдёт в архив, команда вернётся к общему материалу
                    </p>
                </div>
                <div>
                    <Button
                        variant="secondary"
                        disabled={isActing}
                        onClick={() =>
                            keepOverride.mutate(
                                { kind, overrideId },
                                { onSuccess: () => void reviewQuery.refetch() }
                            )
                        }
                    >
                        {keepOverride.isPending ? "Отмечаем…" : "Оставить своё"}
                    </Button>
                    <p className="mt-1 max-w-[200px] text-xs text-ink-3">
                        отметим, что вы посмотрели; текст не изменится
                    </p>
                </div>
                {editHref && (
                    <div>
                        <Link href={editHref}>
                            <Button variant="primary">Править</Button>
                        </Link>
                        <p className="mt-1 max-w-[220px] text-xs text-ink-3">
                            {kind === "lessons"
                                ? "откроется редактор; публикация сама снимет пометку"
                                : PLATFORM_PANEL_EDIT_NOTICE}
                        </p>
                    </div>
                )}
            </div>

            {(acceptBase.isError || keepOverride.isError) && (
                <p className="mt-4 text-sm text-bad" role="alert">
                    Действие не прошло. Обновите страницу и попробуйте ещё раз.
                </p>
            )}

            <ConfirmDialog
                open={isConfirmingAcceptBase}
                title="Вернуться к общему материалу?"
                body={ACCEPT_BASE_CONFIRM_BODY}
                confirmLabel="Взять базу"
                tone="danger"
                isPending={acceptBase.isPending}
                onCancel={() => setIsConfirmingAcceptBase(false)}
                onConfirm={() =>
                    acceptBase.mutate(
                        { kind, overrideId },
                        {
                            onSuccess: () => {
                                setIsConfirmingAcceptBase(false);
                                router.push(BACK_HREF);
                            },
                        }
                    )
                }
            />
        </>
    );
}

function DialogModeOverrideReviewScreen({ overrideId }: { overrideId: string }) {
    const router = useRouter();
    const reviewQuery = useDialogModeOverrideReview(overrideId);
    const acceptBase = useAcceptDialogModeBase();
    const keepOverride = useKeepDialogModeOverride();
    const updatePrompts = useUpdateDialogModePrompts();
    const [isConfirmingAcceptBase, setIsConfirmingAcceptBase] = useState(false);
    const [isEditing, setIsEditing] = useState(false);

    if (reviewQuery.isLoading) return <ReviewSkeleton />;

    if (reviewQuery.isError || !reviewQuery.data) {
        return (
            <>
                <PageHeader title="Разбор" backHref={BACK_HREF} backLabel={BACK_LABEL} />
                <ErrorState
                    message="Не удалось прочитать этот режим диалога. Сервис ИИ мог быть недоступен."
                    onRetry={() => void reviewQuery.refetch()}
                />
            </>
        );
    }

    const review = reviewQuery.data;
    const summary = review.summary;
    const state = resolveOverrideState({
        isStale: summary.isStale,
        forkedFrom: summary.forkedFromHash,
        baseCurrent: summary.baseCurrentHash,
    });

    // A mode has no version table, so there is no «база на момент копирования» column to draw: the
    // fork point is a fingerprint and the text it fingerprinted was overwritten in place upstream.
    const columns: CompareColumn[] = [
        {
            key: "override",
            title: "Ваша версия",
            subtitle: "с вашими правками",
            document: {
                chatSystemPrompt: review.overrideChatSystemPrompt,
                feedbackSystemPrompt: review.overrideFeedbackSystemPrompt,
            },
        },
        {
            key: "base-current",
            title: "База сейчас",
            subtitle: "текущий оригинал",
            document: {
                chatSystemPrompt: review.baseChatSystemPrompt ?? "",
                feedbackSystemPrompt: review.baseFeedbackSystemPrompt ?? "",
            },
        },
    ];

    const isActing = acceptBase.isPending || keepOverride.isPending;

    return (
        <>
            <PageHeader
                title={summary.title}
                subtitle={`режим диалога · ${describeOverrideState(state).label}`}
                backHref={BACK_HREF}
                backLabel={BACK_LABEL}
                action={<OverrideStateBadge state={state} />}
            />

            <p className="mb-4 text-sm text-ink-3">{describeOverrideState(state).hint}</p>

            {isEditing ? (
                <Card>
                    <CardContent>
                        <DialogModePromptEditor
                            chatSystemPrompt={review.overrideChatSystemPrompt}
                            feedbackSystemPrompt={review.overrideFeedbackSystemPrompt}
                            isPending={updatePrompts.isPending}
                            failureMessage={
                                updatePrompts.isError
                                    ? "Промпты не сохранились. Попробуйте ещё раз."
                                    : null
                            }
                            onCancel={() => setIsEditing(false)}
                            onSave={(prompts) =>
                                updatePrompts.mutate(
                                    { overrideId, ...prompts },
                                    {
                                        onSuccess: () => {
                                            setIsEditing(false);
                                            void reviewQuery.refetch();
                                        },
                                    }
                                )
                            }
                        />
                    </CardContent>
                </Card>
            ) : (
                <>
                    <Card>
                        <CardContent>
                            <ThreeWayCompare
                                columns={columns}
                                missingBaseAtForkNotice={NO_BASE_AT_FORK_NOTICE}
                            />
                        </CardContent>
                    </Card>

                    <NoAutoMergeNotice />

                    <div className="mt-6 flex flex-wrap gap-3">
                        <div>
                            <Button
                                variant="secondary"
                                disabled={isActing}
                                onClick={() => setIsConfirmingAcceptBase(true)}
                            >
                                Взять базу
                            </Button>
                            <p className="mt-1 max-w-[200px] text-xs text-ink-3">
                                ваша копия уйдёт в архив, команда вернётся к общему режиму
                            </p>
                        </div>
                        <div>
                            <Button
                                variant="secondary"
                                disabled={isActing}
                                onClick={() =>
                                    keepOverride.mutate(overrideId, {
                                        onSuccess: () => void reviewQuery.refetch(),
                                    })
                                }
                            >
                                {keepOverride.isPending ? "Отмечаем…" : "Оставить своё"}
                            </Button>
                            <p className="mt-1 max-w-[200px] text-xs text-ink-3">
                                отметим, что вы посмотрели; текст не изменится
                            </p>
                        </div>
                        <div>
                            <Button variant="primary" onClick={() => setIsEditing(true)}>
                                Править
                            </Button>
                            <p className="mt-1 max-w-[220px] text-xs text-ink-3">
                                версий у режима нет — сохранение сразу перенаводит отметку форка
                            </p>
                        </div>
                    </div>
                </>
            )}

            <ConfirmDialog
                open={isConfirmingAcceptBase}
                title="Вернуться к общему режиму?"
                body={ACCEPT_BASE_CONFIRM_BODY}
                confirmLabel="Взять базу"
                tone="danger"
                isPending={acceptBase.isPending}
                onCancel={() => setIsConfirmingAcceptBase(false)}
                onConfirm={() =>
                    acceptBase.mutate(overrideId, {
                        onSuccess: () => {
                            setIsConfirmingAcceptBase(false);
                            router.push(BACK_HREF);
                        },
                    })
                }
            />
        </>
    );
}
