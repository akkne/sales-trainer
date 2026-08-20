"use client";

import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { Button } from "@/shared/components/button";
import { Card, CardContent } from "@/shared/components/card";
import { PageHeader } from "@/shared/components/page-header";
import { TextInput, Textarea } from "@/shared/components/input";
import { AssignmentContentPicker } from "@/features/org-assignments/components/assignment-content-picker";
import { AudiencePicker } from "@/features/org-assignments/components/audience-picker";
import { CompletionRuleEditor } from "@/features/org-assignments/components/completion-rule-editor";
import {
    useActivateAssignment,
    useCreateAssignment,
} from "@/features/org-assignments/hooks/use-org-assignments";
import type {
    AssignmentAudienceKind,
    CreateAssignmentRequest,
} from "@/features/org-assignments/types/assignment";
import {
    describeAssignmentWriteFailure,
    isConflictFailure,
} from "@/features/org-assignments/utils/api-failure";
import { buildAudienceRule, validateAudienceRule } from "@/features/org-assignments/utils/audience-rule";
import {
    buildCompletionRuleDocument,
    validateCompletionRuleDraft,
    EMPTY_COMPLETION_RULE_DRAFT,
    type CompletionRuleDraft,
} from "@/features/org-assignments/utils/completion-rule-draft";
import {
    collectContentKinds,
    toContentItems,
    type AssignmentContentDraftItem,
} from "@/features/org-assignments/utils/content-draft";
import {
    buildRepeatScheduleDocument,
    validateRepeatOffsetDays,
    DEFAULT_REPEAT_OFFSET_DAYS,
    REPEAT_SCHEDULE_LIMITS,
} from "@/features/org-assignments/utils/repeat-schedule";
import {
    isDeadlineInPast,
    isOpensAtAfterDeadline,
    readDeadlineInput,
    readOpensAtInput,
} from "@/features/org-assignments/utils/schedule-input";

type SubmitPhase = "idle" | "creating" | "activating";

const SOURCE_TYPE_OPTIONS: { value: string; label: string }[] = [
    { value: "manual", label: "Вручную" },
    { value: "training", label: "По внутреннему тренингу" },
];

/**
 * O3 — turning «у нас был тренинг про возражение по цене» into issued practice with a bar.
 *
 * One scrolling column rather than a wizard: the threshold in step 3 only makes sense next to the
 * content in step 2, and a wizard hides one behind the other.
 */
export default function CreateAssignmentPage() {
    const router = useRouter();
    const createAssignmentMutation = useCreateAssignment();
    const activateAssignmentMutation = useActivateAssignment();

    const [title, setTitle] = useState("");
    const [goal, setGoal] = useState("");
    const [sourceType, setSourceType] = useState("training");
    const [contentItems, setContentItems] = useState<AssignmentContentDraftItem[]>([]);
    const [completionRuleDraft, setCompletionRuleDraft] = useState<CompletionRuleDraft>(
        EMPTY_COMPLETION_RULE_DRAFT
    );
    const [audienceKind, setAudienceKind] = useState<AssignmentAudienceKind>("whole_team");
    const [selectedUserIds, setSelectedUserIds] = useState<string[]>([]);
    const [opensAtInput, setOpensAtInput] = useState("");
    const [deadlineInput, setDeadlineInput] = useState("");
    const [isRepeatEnabled, setIsRepeatEnabled] = useState(false);
    const [repeatOffsetDays, setRepeatOffsetDays] = useState<number[]>(DEFAULT_REPEAT_OFFSET_DAYS);

    const [submitPhase, setSubmitPhase] = useState<SubmitPhase>("idle");
    const [createdAssignmentId, setCreatedAssignmentId] = useState<string | null>(null);
    const [submitFailure, setSubmitFailure] = useState<string | null>(null);
    const [completionRuleFailure, setCompletionRuleFailure] = useState<string | null>(null);

    const contentKinds = useMemo(() => collectContentKinds(contentItems), [contentItems]);
    const audience = buildAudienceRule(audienceKind, selectedUserIds);

    const titleFailure = title.trim().length === 0 ? "Название обязательно." : null;
    const contentFailure =
        contentItems.length === 0
            ? "Добавьте хотя бы один материал — иначе задание просит людей ничего не делать."
            : null;
    const completionRuleValidation = validateCompletionRuleDraft(completionRuleDraft, contentKinds);
    const audienceFailure = validateAudienceRule(audience);
    const repeatFailure = isRepeatEnabled ? validateRepeatOffsetDays(repeatOffsetDays) : null;
    const deadlineFailure =
        deadlineInput.length > 0 && isDeadlineInPast(deadlineInput)
            ? "Срок не может быть в прошлом."
            : null;
    const opensAtFailure =
        deadlineFailure === null && opensAtInput.length > 0 && deadlineInput.length > 0
            && isOpensAtAfterDeadline(opensAtInput, deadlineInput)
            ? "«Открыть» не может быть позже срока."
            : null;
    const scheduleFailure = deadlineFailure ?? opensAtFailure;

    const canIssue =
        titleFailure === null &&
        contentFailure === null &&
        completionRuleValidation === null &&
        audienceFailure === null &&
        repeatFailure === null &&
        scheduleFailure === null;

    const buildRequest = (): CreateAssignmentRequest | null => {
        const completionRule = buildCompletionRuleDocument(completionRuleDraft);
        if (completionRule === null) return null;

        return {
            title: title.trim(),
            goal: goal.trim().length > 0 ? goal.trim() : null,
            sourceType,
            sourceRef: null,
            content: toContentItems(contentItems),
            audience,
            opensAt: readOpensAtInput(opensAtInput),
            deadline: readDeadlineInput(deadlineInput),
            completionRule,
            repeatSchedule: buildRepeatScheduleDocument(isRepeatEnabled, repeatOffsetDays),
        };
    };

    const ensureDraftExists = async (): Promise<string | null> => {
        if (createdAssignmentId !== null) return createdAssignmentId;

        const request = buildRequest();
        if (request === null) return null;

        setSubmitPhase("creating");
        const created = await createAssignmentMutation.mutateAsync(request);
        setCreatedAssignmentId(created.id);

        return created.id;
    };

    const saveDraft = async () => {
        setSubmitFailure(null);
        setCompletionRuleFailure(null);
        try {
            const assignmentId = await ensureDraftExists();
            if (assignmentId !== null) router.replace(`/org/assignments/${assignmentId}`);
        } catch (failure) {
            setSubmitFailure(describeAssignmentWriteFailure(failure, "save"));
        } finally {
            setSubmitPhase("idle");
        }
    };

    const issueToTeam = async () => {
        setSubmitFailure(null);
        setCompletionRuleFailure(null);

        let hasDraftBeenCreated = false;
        try {
            const assignmentId = await ensureDraftExists();
            if (assignmentId === null) return;
            hasDraftBeenCreated = true;

            setSubmitPhase("activating");
            await activateAssignmentMutation.mutateAsync(assignmentId);
            router.replace(`/org/assignments/${assignmentId}`);
        } catch (failure) {
            const message = describeAssignmentWriteFailure(failure, "issue");
            if (hasDraftBeenCreated && isConflictFailure(failure)) {
                setCompletionRuleFailure(message);
            } else {
                setSubmitFailure(message);
            }
        } finally {
            setSubmitPhase("idle");
        }
    };

    const isBusy = submitPhase !== "idle";

    return (
        <>
            <PageHeader
                title="Новое задание"
                backHref="/org/assignments"
                backLabel="Задания"
                subtitle="Практика с дедлайном и порогом. Порог обязателен: без него задание засчитывалось бы за клик."
            />

            <div className="flex flex-col gap-4">
                <Card>
                    <CardContent>
                        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-ink-3">
                            1. Что и зачем
                        </h2>
                        <div className="flex flex-col gap-3">
                            <TextInput
                                label="Название"
                                required
                                value={title}
                                error={title.length > 0 ? (titleFailure ?? undefined) : undefined}
                                onChange={(changeEvent) => setTitle(changeEvent.target.value)}
                                placeholder="Отработка возражения «дорого»"
                            />
                            <Textarea
                                label="Цель"
                                rows={2}
                                value={goal}
                                onChange={(changeEvent) => setGoal(changeEvent.target.value)}
                                placeholder="После тренинга 14 августа. Нужно, чтобы отработали три сценария и не сыпались на скидке."
                            />
                            <div className="flex flex-wrap gap-4">
                                {SOURCE_TYPE_OPTIONS.map((option) => (
                                    <label
                                        key={option.value}
                                        className="flex items-center gap-2 text-sm text-ink-2"
                                    >
                                        <input
                                            type="radio"
                                            name="assignment-source-type"
                                            checked={sourceType === option.value}
                                            disabled={isBusy}
                                            onChange={() => setSourceType(option.value)}
                                        />
                                        {option.label}
                                    </label>
                                ))}
                            </div>
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardContent>
                        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-ink-3">
                            2. Что делать
                        </h2>
                        <AssignmentContentPicker
                            items={contentItems}
                            onChange={setContentItems}
                            disabled={isBusy}
                        />
                    </CardContent>
                </Card>

                <Card>
                    <CardContent>
                        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-ink-3">
                            3. Что считаем выполнением
                        </h2>
                        <CompletionRuleEditor
                            draft={completionRuleDraft}
                            contentKinds={contentKinds}
                            onChange={setCompletionRuleDraft}
                            disabled={isBusy}
                            error={completionRuleFailure}
                        />
                    </CardContent>
                </Card>

                <Card>
                    <CardContent>
                        <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-ink-3">
                            4. Кому и когда
                        </h2>
                        <div className="flex flex-col gap-4">
                            <AudiencePicker
                                audienceKind={audienceKind}
                                selectedUserIds={selectedUserIds}
                                onAudienceKindChange={setAudienceKind}
                                onSelectedUserIdsChange={setSelectedUserIds}
                                disabled={isBusy}
                                error={audienceFailure}
                            />

                            <div className="grid gap-3 sm:grid-cols-2">
                                <TextInput
                                    type="datetime-local"
                                    label="Открыть"
                                    hint="Пусто — задание открывается сразу."
                                    error={opensAtFailure ?? undefined}
                                    value={opensAtInput}
                                    disabled={isBusy}
                                    onChange={(changeEvent) =>
                                        setOpensAtInput(changeEvent.target.value)
                                    }
                                />
                                <TextInput
                                    type="date"
                                    label="Срок"
                                    hint="Срок считается до конца выбранного дня."
                                    error={deadlineFailure ?? undefined}
                                    value={deadlineInput}
                                    disabled={isBusy}
                                    onChange={(changeEvent) =>
                                        setDeadlineInput(changeEvent.target.value)
                                    }
                                />
                            </div>

                            <div className="flex flex-col gap-2">
                                <label className="flex items-center gap-2 text-sm text-ink-2">
                                    <input
                                        type="checkbox"
                                        checked={isRepeatEnabled}
                                        disabled={isBusy}
                                        onChange={(changeEvent) =>
                                            setIsRepeatEnabled(changeEvent.target.checked)
                                        }
                                    />
                                    Повторить сокращённо
                                </label>

                                {isRepeatEnabled && (
                                    <div className="ml-6 flex flex-col gap-2">
                                        <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
                                            через
                                            {repeatOffsetDays.map((offsetDay, index) => (
                                                <input
                                                    key={index}
                                                    type="number"
                                                    aria-label={`Интервал повтора ${index + 1}`}
                                                    className="w-20 rounded-lg border border-line bg-surface px-2 py-1 text-sm text-ink"
                                                    min={REPEAT_SCHEDULE_LIMITS.minimumOffsetDays}
                                                    max={REPEAT_SCHEDULE_LIMITS.maximumOffsetDays}
                                                    value={offsetDay}
                                                    disabled={isBusy}
                                                    onChange={(changeEvent) =>
                                                        setRepeatOffsetDays(
                                                            repeatOffsetDays.map(
                                                                (existing, position) =>
                                                                    position === index
                                                                        ? Number(
                                                                              changeEvent.target
                                                                                  .value
                                                                          )
                                                                        : existing
                                                            )
                                                        )
                                                    }
                                                />
                                            ))}
                                            дней
                                            {repeatOffsetDays.length <
                                                REPEAT_SCHEDULE_LIMITS.maximumWaveCount && (
                                                <Button
                                                    size="sm"
                                                    variant="ghost"
                                                    disabled={isBusy}
                                                    onClick={() =>
                                                        setRepeatOffsetDays([
                                                            ...repeatOffsetDays,
                                                            (repeatOffsetDays[
                                                                repeatOffsetDays.length - 1
                                                            ] ?? 0) + 7,
                                                        ])
                                                    }
                                                >
                                                    + ещё волна
                                                </Button>
                                            )}
                                            {repeatOffsetDays.length > 1 && (
                                                <Button
                                                    size="sm"
                                                    variant="ghost"
                                                    disabled={isBusy}
                                                    onClick={() =>
                                                        setRepeatOffsetDays(
                                                            repeatOffsetDays.slice(0, -1)
                                                        )
                                                    }
                                                >
                                                    убрать волну
                                                </Button>
                                            )}
                                        </div>
                                        <p className="text-xs text-ink-4">
                                            Повтор получат те же люди. Планка не снижается,
                                            сокращается только объём: теория убирается, разговоров
                                            вдвое меньше.
                                        </p>
                                        {repeatFailure && (
                                            <p
                                                className="text-xs"
                                                style={{ color: "var(--heart)" }}
                                                role="alert"
                                            >
                                                {repeatFailure}
                                            </p>
                                        )}
                                    </div>
                                )}
                            </div>
                        </div>
                    </CardContent>
                </Card>

                {submitFailure && (
                    <p className="text-sm" style={{ color: "var(--heart)" }} role="alert">
                        {submitFailure}
                    </p>
                )}

                {createdAssignmentId !== null && (
                    <p className="text-sm text-ink-3">
                        Черновик уже сохранён — повторное нажатие «Выдать команде» не создаст второе
                        задание.
                    </p>
                )}

                <div className="flex flex-wrap items-center justify-end gap-3">
                    {!canIssue && (
                        <span className="text-xs text-ink-4">
                            {completionRuleValidation ??
                                contentFailure ??
                                titleFailure ??
                                audienceFailure ??
                                scheduleFailure ??
                                repeatFailure}
                        </span>
                    )}
                    <Button
                        variant="secondary"
                        disabled={
                            isBusy ||
                            titleFailure !== null ||
                            completionRuleValidation !== null ||
                            scheduleFailure !== null
                        }
                        loading={submitPhase === "creating"}
                        onClick={() => void saveDraft()}
                    >
                        Сохранить черновик
                    </Button>
                    <Button
                        variant="primary"
                        disabled={!canIssue || isBusy}
                        loading={submitPhase === "activating"}
                        onClick={() => void issueToTeam()}
                    >
                        {submitPhase === "creating"
                            ? "Создаём…"
                            : submitPhase === "activating"
                              ? "Выдаём…"
                              : "Выдать команде"}
                    </Button>
                </div>
            </div>
        </>
    );
}
