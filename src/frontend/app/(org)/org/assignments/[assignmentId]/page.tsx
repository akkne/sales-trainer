"use client";

import { Suspense, useState } from "react";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/shared/components/button";
import { Card, CardContent } from "@/shared/components/card";
import { Chip } from "@/shared/components/chip";
import { ConfirmDialog } from "@/shared/components/confirm-dialog";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { Skeleton } from "@/shared/components/skeleton";
import { Tabs } from "@/shared/components/tabs";
import { describeCompletionRule } from "@/features/assignments/utils/completion-rule";
import { AssignmentFunnel } from "@/features/org-assignments/components/assignment-funnel";
import { AssignmentSettingsPanel } from "@/features/org-assignments/components/assignment-settings-panel";
import {
    RemindDialog,
    describeRecipientName,
} from "@/features/org-assignments/components/remind-dialog";
import {
    assignmentStatusTone,
    describeAssignmentSourceType,
    describeAssignmentStatus,
    describeProgressStatus,
    progressStatusTone,
} from "@/features/org-assignments/constants/assignment-dictionary";
import {
    useActivateAssignment,
    useAssignmentDashboard,
    useAssignmentRawProgress,
    useCloseAssignment,
    useOrganizationAssignment,
    useRemindAssignment,
} from "@/features/org-assignments/hooks/use-org-assignments";
import type {
    AssignmentDashboardRow,
    AssignmentProgressRecord,
    AssignmentReminderScope,
} from "@/features/org-assignments/types/assignment";
import {
    describeAssignmentWriteFailure,
    isNotFoundFailure,
} from "@/features/org-assignments/utils/api-failure";
import { describeAudienceKind, pluralizePeople } from "@/features/org-assignments/utils/audience-rule";
import { describeBestScore } from "@/features/org-assignments/utils/completion-rule-draft";
import {
    countReminderRecipients,
    describeWaveComparison,
    formatLongDate,
    describeDeadline,
} from "@/features/org-assignments/utils/funnel";

const REMINDER_SCOPES: AssignmentReminderScope[] = ["not_started", "unfinished"];

export default function AssignmentDetailPage() {
    return (
        <Suspense fallback={null}>
            <AssignmentDetailContent />
        </Suspense>
    );
}

/**
 * O4 — the five-stage funnel, the named rows behind it, the waves of the series, and the one button
 * the deadline digest sends the РОП here to press.
 *
 * `?action=remind&scope=not_started` opens the reminder dialog preset to that scope and sends
 * nothing: the link arrives by email, and an URL that messages the team on load fires from a mail
 * scanner.
 */
function AssignmentDetailContent() {
    const router = useRouter();
    const routeParameters = useParams();
    const searchParameters = useSearchParams();

    const assignmentId = String(routeParameters.assignmentId ?? "");
    const requestedScope = searchParameters.get("scope");

    const dashboardQuery = useAssignmentDashboard(assignmentId);
    const assignmentQuery = useOrganizationAssignment(assignmentId);

    const [isRemindDialogOpen, setIsRemindDialogOpen] = useState(
        searchParameters.get("action") === "remind"
    );
    const [reminderScope, setReminderScope] = useState<AssignmentReminderScope>(
        REMINDER_SCOPES.includes(requestedScope as AssignmentReminderScope)
            ? (requestedScope as AssignmentReminderScope)
            : "not_started"
    );
    const [isCloseDialogOpen, setIsCloseDialogOpen] = useState(false);
    const [showRawProgress, setShowRawProgress] = useState(false);
    const [reminderFailure, setReminderFailure] = useState<string | null>(null);
    const [reminderReceipt, setReminderReceipt] = useState<string | null>(null);
    const [writeFailure, setWriteFailure] = useState<string | null>(null);

    const remindAssignmentMutation = useRemindAssignment(assignmentId);
    const closeAssignmentMutation = useCloseAssignment(assignmentId);
    const activateAssignmentMutation = useActivateAssignment();
    const rawProgressQuery = useAssignmentRawProgress(assignmentId, showRawProgress);

    if (dashboardQuery.isLoading) {
        return (
            <>
                <PageHeader title="Задание" backHref="/org/assignments" backLabel="Задания" />
                <Skeleton height={120} rounded={16} />
                <div className="mt-4 flex flex-col gap-2">
                    {[0, 1, 2, 3, 4].map((rowIndex) => (
                        <Skeleton key={rowIndex} height={44} rounded={12} />
                    ))}
                </div>
            </>
        );
    }

    if (dashboardQuery.isError && isNotFoundFailure(dashboardQuery.error)) {
        return (
            <>
                <PageHeader title="Задание" backHref="/org/assignments" backLabel="Задания" />
                <EmptyState
                    icon="grid"
                    title="Задание не найдено"
                    description="Возможно, черновик удалили."
                    action={
                        <Button variant="secondary" onClick={() => router.push("/org/assignments")}>
                            К списку заданий
                        </Button>
                    }
                />
            </>
        );
    }

    if (dashboardQuery.isError || !dashboardQuery.data) {
        return (
            <>
                <PageHeader title="Задание" backHref="/org/assignments" backLabel="Задания" />
                <ErrorState
                    title="Не удалось загрузить карточку задания"
                    message="Воронка и имена читаются вместе с сервисом пользователей — он мог не ответить."
                    onRetry={() => void dashboardQuery.refetch()}
                />
                <div className="mt-4 text-center">
                    <Button variant="ghost" onClick={() => setShowRawProgress(true)}>
                        Показать сырые строки
                    </Button>
                </div>
                {showRawProgress && <RawProgressTable rows={rawProgressQuery.data ?? []} />}
            </>
        );
    }

    const dashboard = dashboardQuery.data;
    const summary = dashboard.assignment;
    const deadline = describeDeadline(summary.deadline);
    const completionRuleSentence = describeCompletionRule(assignmentQuery.data?.completionRule);
    const completionRuleKind = assignmentQuery.data?.completionRule?.kind ?? null;
    const isDraft = summary.status === "draft";
    const isActive = summary.status === "active";
    const waveComparison = describeWaveComparison(dashboard.series);

    const sendReminder = async () => {
        setReminderFailure(null);
        setReminderReceipt(null);
        try {
            const result = await remindAssignmentMutation.mutateAsync(reminderScope);
            setReminderReceipt(
                `Напоминание отправлено: ${result.notifiedCount} ${pluralizePeople(result.notifiedCount)}.`
            );
            setIsRemindDialogOpen(false);
        } catch (failure) {
            setReminderFailure(describeAssignmentWriteFailure(failure, "remind"));
        }
    };

    const rowColumns: Column<AssignmentDashboardRow>[] = [
        {
            key: "displayName",
            header: "Кто",
            render: (row) => (
                <span className="text-ink">
                    {describeRecipientName(row)}
                    {row.isActiveMember === false && dashboard.rosterKnown && (
                        <span className="text-ink-4"> †</span>
                    )}
                </span>
            ),
        },
        {
            key: "status",
            header: "Состояние",
            render: (row) => (
                <Chip tone={progressStatusTone(row.status)} size="sm">
                    {describeProgressStatus(row.status)}
                </Chip>
            ),
        },
        {
            key: "bestScore",
            header: "Результат",
            render: (row) => (
                <span className="tnum text-ink-2" style={{ fontFamily: "var(--font-mono)" }}>
                    {describeBestScore(row.bestScore, completionRuleKind)}
                </span>
            ),
        },
        {
            key: "attemptCount",
            header: "Попыток",
            align: "right",
            render: (row) => (
                <span className="tnum text-ink-2" style={{ fontFamily: "var(--font-mono)" }}>
                    {row.attemptCount === 0 ? "—" : row.attemptCount}
                </span>
            ),
        },
        {
            key: "when",
            header: "Когда",
            render: (row) => (
                <span className="text-ink-3">
                    {row.completedAt
                        ? `готово ${formatLongDate(row.completedAt)}`
                        : row.firstOpenedAt
                          ? `начал ${formatLongDate(row.firstOpenedAt)}`
                          : "—"}
                </span>
            ),
        },
    ];

    return (
        <>
            <PageHeader
                title={summary.title}
                backHref="/org/assignments"
                backLabel="Задания"
                subtitle={[
                    describeAssignmentSourceType(summary.sourceType),
                    describeAudienceKind(summary.audienceKind, summary.assignedCount || null),
                    summary.deadline ? `срок ${formatLongDate(summary.deadline)} (${deadline.text})` : "без срока",
                ].join(" · ")}
                action={
                    <div className="flex items-center gap-2">
                        <Chip tone={assignmentStatusTone(summary.status)}>
                            {describeAssignmentStatus(summary.status)}
                        </Chip>
                        {isActive && (
                            <Button variant="secondary" onClick={() => setIsCloseDialogOpen(true)}>
                                Закрыть
                            </Button>
                        )}
                    </div>
                }
            />

            {completionRuleSentence && (
                <p className="mb-4 text-sm text-ink-2">Порог: {completionRuleSentence}</p>
            )}

            {writeFailure && (
                <p className="mb-4 text-sm" style={{ color: "var(--heart)" }} role="alert">
                    {writeFailure}
                </p>
            )}

            {isDraft ? (
                <Card>
                    <CardContent>
                        <EmptyState
                            icon="grid"
                            compact
                            title="Задание ещё не выдано"
                            description="Пока это черновик: строк прогресса не существует, воронку показывать не из чего."
                            action={
                                <Button
                                    variant="primary"
                                    loading={activateAssignmentMutation.isPending}
                                    onClick={() => {
                                        setWriteFailure(null);
                                        activateAssignmentMutation.mutate(assignmentId, {
                                            onError: (failure) =>
                                                setWriteFailure(
                                                    describeAssignmentWriteFailure(failure, "issue")
                                                ),
                                        });
                                    }}
                                >
                                    Выдать команде
                                </Button>
                            }
                        />
                    </CardContent>
                </Card>
            ) : (
                <Card>
                    <CardContent>
                        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-ink-3">
                            Воронка
                        </h2>
                        <AssignmentFunnel
                            funnel={dashboard.funnel}
                            isRosterKnown={dashboard.rosterKnown}
                            action={
                                isActive ? (
                                    <Button
                                        variant="primary"
                                        onClick={() => {
                                            setReminderReceipt(null);
                                            setIsRemindDialogOpen(true);
                                        }}
                                    >
                                        {reminderScope === "not_started"
                                            ? `Напомнить тем, кто не начал (${countReminderRecipients(dashboard.funnel, "not_started")})`
                                            : `Напомнить всем, кто не закончил (${countReminderRecipients(dashboard.funnel, "unfinished")})`}
                                    </Button>
                                ) : null
                            }
                        />
                        {reminderReceipt && (
                            <p className="mt-3 text-sm" style={{ color: "var(--success)" }}>
                                {reminderReceipt}
                            </p>
                        )}
                    </CardContent>
                </Card>
            )}

            {dashboard.series.length > 1 && (
                <Card className="mt-4">
                    <CardContent>
                        <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-3">
                            Серия
                        </h2>
                        {waveComparison && (
                            <p className="mb-2 text-sm text-ink-3">{waveComparison}</p>
                        )}
                        <Tabs
                            items={dashboard.series.map((wave) => ({
                                key: wave.assignmentId,
                                label: `Волна ${wave.waveIndex + 1}${
                                    wave.assignmentId === assignmentId ? " · сейчас" : ""
                                }`,
                            }))}
                            activeKey={assignmentId}
                            onChange={(waveAssignmentId) => {
                                if (waveAssignmentId !== assignmentId) {
                                    router.push(`/org/assignments/${waveAssignmentId}`);
                                }
                            }}
                        />
                    </CardContent>
                </Card>
            )}

            {!isDraft && (
                <Card className="mt-4">
                    <CardContent>
                        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-ink-3">
                            Кто где
                        </h2>
                        <DataTable
                            columns={rowColumns}
                            rows={dashboard.rows}
                            rowKey={(row) => row.userId}
                            empty={
                                <EmptyState
                                    icon="users"
                                    compact
                                    title="Строк прогресса ещё нет"
                                    description="Задание выдано, но ни одна строка не создана — обновите страницу через минуту."
                                />
                            }
                        />
                        {dashboard.rosterKnown ? (
                            dashboard.rows.some((row) => row.isActiveMember === false) && (
                                <p className="mt-2 text-xs text-ink-4">
                                    † уже не работает в компании
                                </p>
                            )
                        ) : (
                            <p className="mt-2 text-xs text-ink-4">
                                Не удалось проверить, кто ещё работает в компании — пометок нет.
                            </p>
                        )}
                    </CardContent>
                </Card>
            )}

            <div className="mt-4">
                {assignmentQuery.isLoading && <Skeleton height={56} rounded={16} />}
                {assignmentQuery.data && (
                    <AssignmentSettingsPanel assignment={assignmentQuery.data} />
                )}
                {assignmentQuery.isError && (
                    <p className="text-sm text-ink-3">
                        Содержание и настройки сейчас недоступны — не удалось прочитать задание
                        целиком.
                    </p>
                )}
            </div>

            <RemindDialog
                open={isRemindDialogOpen}
                scope={reminderScope}
                onScopeChange={setReminderScope}
                funnel={dashboard.funnel}
                rows={dashboard.rows}
                onClose={() => setIsRemindDialogOpen(false)}
                onConfirm={() => void sendReminder()}
                isPending={remindAssignmentMutation.isPending}
                error={reminderFailure}
            />

            <ConfirmDialog
                open={isCloseDialogOpen}
                title="Закрыть задание?"
                body="После закрытия задание становится историей: напоминания больше не отправляются, оставшиеся волны не выйдут, содержание и сроки заморожены."
                confirmLabel="Закрыть задание"
                tone="danger"
                isPending={closeAssignmentMutation.isPending}
                onCancel={() => setIsCloseDialogOpen(false)}
                onConfirm={() => {
                    setWriteFailure(null);
                    closeAssignmentMutation.mutate(undefined, {
                        onSuccess: () => setIsCloseDialogOpen(false),
                        onError: (failure) => {
                            setWriteFailure(describeAssignmentWriteFailure(failure, "close"));
                            setIsCloseDialogOpen(false);
                        },
                    });
                }}
            />
        </>
    );
}

/**
 * The fallback the design allows exactly here: `GET …/progress` has no identity-service dependency,
 * so it answers when the dashboard cannot. No names, and that is the honest trade.
 */
function RawProgressTable({ rows }: { rows: AssignmentProgressRecord[] }) {
    if (rows.length === 0) {
        return <p className="mt-4 text-sm text-ink-3">Сырых строк тоже нет.</p>;
    }

    return (
        <div className="mt-4">
            <p className="mb-2 text-xs text-ink-4">
                Сырые строки без имён — их отдаёт ручка, не зависящая от сервиса пользователей.
            </p>
            <DataTable
                columns={[
                    {
                        key: "userId",
                        header: "Идентификатор",
                        render: (row: AssignmentProgressRecord) => (
                            <span className="tnum text-ink-2" style={{ fontFamily: "var(--font-mono)" }}>
                                {row.userId}
                            </span>
                        ),
                    },
                    {
                        key: "status",
                        header: "Состояние",
                        render: (row: AssignmentProgressRecord) => describeProgressStatus(row.status),
                    },
                    {
                        key: "bestScore",
                        header: "Лучший результат",
                        align: "right",
                        render: (row: AssignmentProgressRecord) => row.bestScore ?? "—",
                    },
                    {
                        key: "attemptCount",
                        header: "Попыток",
                        align: "right",
                        render: (row: AssignmentProgressRecord) => row.attemptCount,
                    },
                ]}
                rows={rows}
                rowKey={(row) => row.userId}
                empty={null}
            />
        </div>
    );
}
