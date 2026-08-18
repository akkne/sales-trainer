"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Card, CardContent } from "@/shared/components/card";
import { TextInput, Textarea } from "@/shared/components/input";
import { describeCompletionRule } from "@/features/assignments/utils/completion-rule";
import { AudiencePicker } from "@/features/org-assignments/components/audience-picker";
import { describeContentKind } from "@/features/org-assignments/constants/assignment-dictionary";
import { useUpdateAssignment } from "@/features/org-assignments/hooks/use-org-assignments";
import type {
    Assignment,
    AssignmentAudienceKind,
} from "@/features/org-assignments/types/assignment";
import { describeAssignmentWriteFailure } from "@/features/org-assignments/utils/api-failure";
import { buildAudienceRule, validateAudienceRule } from "@/features/org-assignments/utils/audience-rule";
import {
    buildRepeatScheduleDocument,
    readRepeatOffsetDays,
    validateRepeatOffsetDays,
    REPEAT_SCHEDULE_LIMITS,
} from "@/features/org-assignments/utils/repeat-schedule";
import {
    readDeadlineInput,
    readOpensAtInput,
    writeDateInput,
    writeDateTimeInput,
} from "@/features/org-assignments/utils/schedule-input";

interface AssignmentSettingsPanelProps {
    assignment: Assignment;
}

/**
 * «Содержание и настройки», collapsed by default.
 *
 * On an issued assignment `sourceType`, `sourceRef`, `content` and `completionRule` are shown but
 * frozen — every recorded attempt was scored against them, and the database refuses to change them
 * regardless of what this screen sends. Title, goal, audience, dates and repeats stay editable,
 * because adding a new hire and extending a deadline are ordinary acts of running a team.
 */
export function AssignmentSettingsPanel({ assignment }: AssignmentSettingsPanelProps) {
    const [isExpanded, setIsExpanded] = useState(false);
    const [title, setTitle] = useState(assignment.title);
    const [goal, setGoal] = useState(assignment.goal ?? "");
    const [audienceKind, setAudienceKind] = useState<AssignmentAudienceKind>(
        (assignment.audience.kind as AssignmentAudienceKind) ?? "whole_team"
    );
    const [selectedUserIds, setSelectedUserIds] = useState<string[]>(
        assignment.audience.userIds ?? []
    );
    const [opensAtInput, setOpensAtInput] = useState(writeDateTimeInput(assignment.opensAt));
    const [deadlineInput, setDeadlineInput] = useState(writeDateInput(assignment.deadline));
    const [isRepeatEnabled, setIsRepeatEnabled] = useState(assignment.repeatSchedule !== null);
    const [repeatOffsetDays, setRepeatOffsetDays] = useState<number[]>(
        readRepeatOffsetDays(assignment.repeatSchedule)
    );
    const [saveFailure, setSaveFailure] = useState<string | null>(null);
    const [wasSaved, setWasSaved] = useState(false);

    const updateAssignmentMutation = useUpdateAssignment(assignment.id);

    const isClosed = assignment.status === "closed";
    const isActive = assignment.status === "active";
    const audience = buildAudienceRule(audienceKind, selectedUserIds);
    const audienceFailure = validateAudienceRule(audience);
    const repeatFailure = isRepeatEnabled ? validateRepeatOffsetDays(repeatOffsetDays) : null;
    const completionRuleSentence = describeCompletionRule(assignment.completionRule);

    const save = async () => {
        setSaveFailure(null);
        setWasSaved(false);
        try {
            await updateAssignmentMutation.mutateAsync({
                title: title.trim(),
                goal: goal.trim().length > 0 ? goal.trim() : null,
                sourceType: assignment.sourceType,
                sourceRef: assignment.sourceRef,
                content: assignment.content,
                audience,
                opensAt: readOpensAtInput(opensAtInput),
                deadline: readDeadlineInput(deadlineInput),
                completionRule: assignment.completionRule,
                repeatSchedule: buildRepeatScheduleDocument(isRepeatEnabled, repeatOffsetDays),
            });
            setWasSaved(true);
        } catch (failure) {
            setSaveFailure(describeAssignmentWriteFailure(failure, "save"));
        }
    };

    return (
        <Card>
            <CardContent>
                <div className="flex items-center justify-between gap-3">
                    <h2 className="text-xs font-semibold uppercase tracking-wide text-ink-3">
                        Содержание и настройки
                    </h2>
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setIsExpanded(!isExpanded)}
                        aria-expanded={isExpanded}
                    >
                        {isExpanded ? "Свернуть" : "Развернуть"}
                    </Button>
                </div>

                {isExpanded && (
                    <div className="mt-4 flex flex-col gap-4">
                        {isActive && (
                            <p className="text-xs text-ink-4">
                                После выдачи содержание и порог заморожены: люди уже отвечают против
                                них. Название, цель, состав, сроки и повторы менять можно.
                            </p>
                        )}

                        <div>
                            <div className="mb-1 text-xs text-ink-3">Содержание</div>
                            <ol className="flex flex-col gap-1 text-sm text-ink-2">
                                {assignment.content.length === 0 && (
                                    <li className="text-ink-4">Содержания нет.</li>
                                )}
                                {[...assignment.content]
                                    .sort((left, right) => left.orderIndex - right.orderIndex)
                                    .map((item) => (
                                        <li key={`${item.kind}:${item.reference}`}>
                                            {item.orderIndex + 1}. {describeContentKind(item.kind)} ·{" "}
                                            <span className="text-ink-3">{item.reference}</span>
                                            {item.persona?.name && (
                                                <span className="text-ink-3">
                                                    {" "}
                                                    · персона {item.persona.name}
                                                </span>
                                            )}
                                        </li>
                                    ))}
                            </ol>
                        </div>

                        <div>
                            <div className="mb-1 text-xs text-ink-3">Порог</div>
                            <p className="text-sm text-ink-2">
                                {completionRuleSentence ?? "Порог задан правилом, которое эта версия панели не умеет показывать."}
                            </p>
                        </div>

                        <TextInput
                            label="Название"
                            value={title}
                            disabled={isClosed}
                            onChange={(changeEvent) => setTitle(changeEvent.target.value)}
                        />
                        <Textarea
                            label="Цель"
                            rows={2}
                            value={goal}
                            disabled={isClosed}
                            onChange={(changeEvent) => setGoal(changeEvent.target.value)}
                        />

                        <div>
                            <div className="mb-2 text-xs text-ink-3">Кому</div>
                            <AudiencePicker
                                audienceKind={audienceKind}
                                selectedUserIds={selectedUserIds}
                                onAudienceKindChange={setAudienceKind}
                                onSelectedUserIdsChange={setSelectedUserIds}
                                disabled={isClosed}
                                error={audienceFailure}
                            />
                            <p className="mt-1 text-xs text-ink-4">
                                Нанялся человек после выдачи — добавьте его сюда и нажмите
                                «Сохранить». Строки дописываются, никто не удаляется.
                            </p>
                        </div>

                        <div className="grid gap-3 sm:grid-cols-2">
                            <TextInput
                                type="datetime-local"
                                label="Открыть"
                                value={opensAtInput}
                                disabled={isClosed}
                                onChange={(changeEvent) => setOpensAtInput(changeEvent.target.value)}
                            />
                            <TextInput
                                type="date"
                                label="Срок"
                                value={deadlineInput}
                                disabled={isClosed}
                                onChange={(changeEvent) => setDeadlineInput(changeEvent.target.value)}
                            />
                        </div>

                        <div className="flex flex-col gap-2">
                            <label className="flex items-center gap-2 text-sm text-ink-2">
                                <input
                                    type="checkbox"
                                    checked={isRepeatEnabled}
                                    disabled={isClosed}
                                    onChange={(changeEvent) =>
                                        setIsRepeatEnabled(changeEvent.target.checked)
                                    }
                                />
                                Повторить сокращённо
                            </label>
                            {isRepeatEnabled && (
                                <div className="ml-6 flex flex-wrap items-center gap-2 text-sm text-ink-2">
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
                                            disabled={isClosed}
                                            onChange={(changeEvent) =>
                                                setRepeatOffsetDays(
                                                    repeatOffsetDays.map((existing, position) =>
                                                        position === index
                                                            ? Number(changeEvent.target.value)
                                                            : existing
                                                    )
                                                )
                                            }
                                        />
                                    ))}
                                    дней
                                </div>
                            )}
                            {repeatFailure && (
                                <p className="text-xs" style={{ color: "var(--heart)" }} role="alert">
                                    {repeatFailure}
                                </p>
                            )}
                            {isActive && (
                                <p className="text-xs text-ink-4">
                                    Отменить оставшиеся волны можно только сейчас: у закрытого
                                    задания расписание заморожено вместе со всем остальным.
                                </p>
                            )}
                        </div>

                        {saveFailure && (
                            <p className="text-sm" style={{ color: "var(--heart)" }} role="alert">
                                {saveFailure}
                            </p>
                        )}
                        {wasSaved && <p className="text-sm text-ink-3">Изменения сохранены.</p>}

                        {!isClosed && (
                            <div className="flex justify-end">
                                <Button
                                    variant="primary"
                                    loading={updateAssignmentMutation.isPending}
                                    disabled={audienceFailure !== null || repeatFailure !== null}
                                    onClick={() => void save()}
                                >
                                    Сохранить
                                </Button>
                            </div>
                        )}
                    </div>
                )}
            </CardContent>
        </Card>
    );
}
