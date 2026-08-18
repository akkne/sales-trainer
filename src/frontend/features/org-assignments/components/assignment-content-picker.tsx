"use client";

import Link from "next/link";
import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Icon } from "@/shared/components/icon";
import { Modal } from "@/shared/components/modal";
import { Select, TextInput, Textarea } from "@/shared/components/input";
import type { AssignmentContentKind } from "@/features/assignments/utils/completion-rule";
import {
    PERSONA_DIFFICULTY_OPTIONS,
    describeContentKind,
} from "@/features/org-assignments/constants/assignment-dictionary";
import {
    containsContentItem,
    moveContentItem,
    EMPTY_PERSONA,
    type AssignmentContentDraftItem,
} from "@/features/org-assignments/utils/content-draft";
import {
    findLatestPublishedVersion,
    useAssignableLessons,
    useDialogBundles,
    useDialogModes,
    useLessonVersions,
    useReferenceMaterialSearch,
    type LessonChoice,
} from "@/features/org-assignments/hooks/use-assignment-content-sources";

interface AssignmentContentPickerProps {
    items: AssignmentContentDraftItem[];
    onChange: (items: AssignmentContentDraftItem[]) => void;
    disabled?: boolean;
}

type PickerKind = AssignmentContentKind | null;

const PICKER_TITLES: Record<AssignmentContentKind, string> = {
    lesson_version: "Упражнения из урока",
    dialog_scenario: "Разговор",
    reference_material: "Теория",
};

/**
 * What the team is asked to do, in order.
 *
 * Two rules the server enforces and this screen enforces first: a lesson is referenced by its frozen
 * published version and never by the lesson itself, and a `(kind, reference)` pair may appear once.
 * A lesson with nothing published cannot be chosen at all — its version is the thing an assignment
 * points at, and there is none.
 */
export function AssignmentContentPicker({
    items,
    onChange,
    disabled = false,
}: AssignmentContentPickerProps) {
    const [openPickerKind, setOpenPickerKind] = useState<PickerKind>(null);
    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

    const addItem = (item: AssignmentContentDraftItem) => {
        if (containsContentItem(items, item.kind, item.reference)) return;
        onChange([...items, item]);
        setOpenPickerKind(null);
    };

    const removeItem = (index: number) => onChange(items.filter((_, position) => position !== index));

    const updatePersona = (index: number, personaPatch: Partial<AssignmentContentDraftItem["persona"]>) => {
        onChange(
            items.map((item, position) =>
                position === index
                    ? { ...item, persona: { ...(item.persona ?? EMPTY_PERSONA), ...personaPatch } }
                    : item
            )
        );
    };

    return (
        <div className="flex flex-col gap-3">
            <div className="flex flex-wrap gap-2">
                <Button
                    variant="secondary"
                    size="sm"
                    disabled={disabled}
                    onClick={() => setOpenPickerKind("lesson_version")}
                >
                    + Упражнения из урока
                </Button>
                <Button
                    variant="secondary"
                    size="sm"
                    disabled={disabled}
                    onClick={() => setOpenPickerKind("dialog_scenario")}
                >
                    + Разговор
                </Button>
                <Button
                    variant="secondary"
                    size="sm"
                    disabled={disabled}
                    onClick={() => setOpenPickerKind("reference_material")}
                >
                    + Теория
                </Button>
            </div>

            {items.length === 0 && (
                <p className="text-sm text-ink-3">
                    Пока пусто. Задание без содержания просит людей ничего не делать — добавьте
                    упражнения, разговор или теорию.
                </p>
            )}

            <ol className="flex flex-col gap-2">
                {items.map((item, index) => (
                    <li
                        key={`${item.kind}:${item.reference}`}
                        draggable={!disabled}
                        onDragStart={() => setDraggedIndex(index)}
                        onDragOver={(dragEvent) => dragEvent.preventDefault()}
                        onDrop={() => {
                            if (draggedIndex !== null) {
                                onChange(moveContentItem(items, draggedIndex, index));
                            }
                            setDraggedIndex(null);
                        }}
                        className="rounded-xl border border-line bg-surface p-3"
                    >
                        <div className="flex items-start gap-2">
                            <span className="mt-0.5 cursor-grab text-ink-4" aria-hidden>
                                ⠿
                            </span>
                            <span className="tnum mt-0.5 text-sm text-ink-3">{index + 1}.</span>
                            <div className="min-w-0 flex-1">
                                <div className="text-sm text-ink">
                                    {describeContentKind(item.kind)} ·{" "}
                                    {item.title ?? item.reference}
                                </div>
                            </div>
                            <button
                                type="button"
                                aria-label="Поднять выше"
                                disabled={disabled || index === 0}
                                onClick={() => onChange(moveContentItem(items, index, index - 1))}
                                className="text-ink-3 disabled:opacity-30"
                            >
                                <Icon name="chevron-up" size="sm" />
                            </button>
                            <button
                                type="button"
                                aria-label="Опустить ниже"
                                disabled={disabled || index === items.length - 1}
                                onClick={() => onChange(moveContentItem(items, index, index + 1))}
                                className="text-ink-3 disabled:opacity-30"
                            >
                                <Icon name="chevron-down" size="sm" />
                            </button>
                            <button
                                type="button"
                                aria-label="Убрать из задания"
                                disabled={disabled}
                                onClick={() => removeItem(index)}
                                className="text-ink-3 hover:text-ink"
                            >
                                <Icon name="close" size="sm" />
                            </button>
                        </div>

                        {item.kind === "dialog_scenario" && (
                            <div className="mt-3 grid gap-2 pl-8 sm:grid-cols-2">
                                <TextInput
                                    label="Имя"
                                    inputSize="sm"
                                    disabled={disabled}
                                    value={item.persona?.name ?? ""}
                                    onChange={(changeEvent) =>
                                        updatePersona(index, { name: changeEvent.target.value || null })
                                    }
                                />
                                <TextInput
                                    label="Должность"
                                    inputSize="sm"
                                    disabled={disabled}
                                    value={item.persona?.position ?? ""}
                                    onChange={(changeEvent) =>
                                        updatePersona(index, {
                                            position: changeEvent.target.value || null,
                                        })
                                    }
                                />
                                <Select
                                    label="Сложность"
                                    inputSize="sm"
                                    disabled={disabled}
                                    value={item.persona?.difficulty ?? ""}
                                    onChange={(changeEvent) =>
                                        updatePersona(index, {
                                            difficulty: changeEvent.target.value || null,
                                        })
                                    }
                                >
                                    {PERSONA_DIFFICULTY_OPTIONS.map((option) => (
                                        <option key={option.label} value={option.value}>
                                            {option.label}
                                        </option>
                                    ))}
                                </Select>
                                <Textarea
                                    label="Характер"
                                    inputSize="sm"
                                    rows={2}
                                    disabled={disabled}
                                    className="sm:col-span-2"
                                    value={item.persona?.personality ?? ""}
                                    onChange={(changeEvent) =>
                                        updatePersona(index, {
                                            personality: changeEvent.target.value || null,
                                        })
                                    }
                                />
                            </div>
                        )}
                    </li>
                ))}
            </ol>

            <Modal
                open={openPickerKind !== null}
                onClose={() => setOpenPickerKind(null)}
                title={openPickerKind ? PICKER_TITLES[openPickerKind] : ""}
                size="lg"
            >
                {openPickerKind === "lesson_version" && (
                    <LessonVersionPicker items={items} onPick={addItem} />
                )}
                {openPickerKind === "dialog_scenario" && (
                    <DialogScenarioPicker items={items} onPick={addItem} />
                )}
                {openPickerKind === "reference_material" && (
                    <ReferenceMaterialPicker items={items} onPick={addItem} />
                )}
            </Modal>
        </div>
    );
}

interface PickerProps {
    items: AssignmentContentDraftItem[];
    onPick: (item: AssignmentContentDraftItem) => void;
}

function LessonVersionPicker({ items, onPick }: PickerProps) {
    const lessonsQuery = useAssignableLessons();
    const [expandedLesson, setExpandedLesson] = useState<LessonChoice | null>(null);
    const versionsQuery = useLessonVersions(expandedLesson?.id ?? null);
    const latestPublishedVersion = findLatestPublishedVersion(versionsQuery.data);

    if (lessonsQuery.isLoading) return <p className="text-sm text-ink-3">Загружаем уроки…</p>;
    if (lessonsQuery.isError) {
        return (
            <p className="text-sm" style={{ color: "var(--heart)" }}>
                Не удалось загрузить список уроков.
            </p>
        );
    }
    if ((lessonsQuery.data ?? []).length === 0) {
        return <p className="text-sm text-ink-3">Уроков пока нет.</p>;
    }

    return (
        <div className="flex flex-col gap-1">
            {(lessonsQuery.data ?? []).map((lesson) => {
                const isExpanded = expandedLesson?.id === lesson.id;

                return (
                    <div key={lesson.id} className="rounded-lg border border-line p-2">
                        <button
                            type="button"
                            className="w-full text-left text-sm text-ink"
                            onClick={() => setExpandedLesson(isExpanded ? null : lesson)}
                        >
                            {lesson.title}
                            <span className="ml-2 text-xs text-ink-3">{lesson.topicTitle}</span>
                        </button>

                        {isExpanded && (
                            <div className="mt-2 flex flex-wrap items-center gap-2 text-xs">
                                {versionsQuery.isLoading && (
                                    <span className="text-ink-3">Смотрим версии…</span>
                                )}
                                {!versionsQuery.isLoading && latestPublishedVersion === null && (
                                    <>
                                        <span className="text-ink-3">
                                            у урока нет опубликованной версии — опубликуйте её в
                                            редакторе
                                        </span>
                                        <Link
                                            href={`/org/content/lessons/${lesson.id}`}
                                            className="text-primary-ink underline"
                                        >
                                            Открыть редактор
                                        </Link>
                                    </>
                                )}
                                {latestPublishedVersion !== null && (
                                    <Button
                                        size="sm"
                                        variant="secondary"
                                        disabled={containsContentItem(
                                            items,
                                            "lesson_version",
                                            latestPublishedVersion.id
                                        )}
                                        onClick={() =>
                                            onPick({
                                                kind: "lesson_version",
                                                reference: latestPublishedVersion.id,
                                                title: `«${lesson.title}» · версия ${latestPublishedVersion.versionNumber}`,
                                                persona: null,
                                            })
                                        }
                                    >
                                        {containsContentItem(
                                            items,
                                            "lesson_version",
                                            latestPublishedVersion.id
                                        )
                                            ? "✓ уже добавлено"
                                            : `Выбрать версию ${latestPublishedVersion.versionNumber}`}
                                    </Button>
                                )}
                            </div>
                        )}
                    </div>
                );
            })}
        </div>
    );
}

function DialogScenarioPicker({ items, onPick }: PickerProps) {
    const bundlesQuery = useDialogBundles();
    const [selectedBundleId, setSelectedBundleId] = useState<string | null>(null);
    const modesQuery = useDialogModes(selectedBundleId);

    if (bundlesQuery.isLoading) return <p className="text-sm text-ink-3">Загружаем сценарии…</p>;
    if (bundlesQuery.isError) {
        return (
            <p className="text-sm" style={{ color: "var(--heart)" }}>
                Не удалось загрузить сценарии разговоров.
            </p>
        );
    }

    return (
        <div className="flex flex-col gap-3">
            <Select
                label="Набор"
                value={selectedBundleId ?? ""}
                onChange={(changeEvent) => setSelectedBundleId(changeEvent.target.value || null)}
            >
                <option value="">Выберите набор</option>
                {(bundlesQuery.data ?? []).map((bundle) => (
                    <option key={bundle.id} value={bundle.id}>
                        {bundle.title}
                    </option>
                ))}
            </Select>

            {selectedBundleId && modesQuery.isLoading && (
                <p className="text-sm text-ink-3">Загружаем режимы…</p>
            )}

            <div className="flex flex-col gap-1">
                {(modesQuery.data ?? []).map((mode) => {
                    const isAlreadyAdded = containsContentItem(items, "dialog_scenario", mode.key);

                    return (
                        <button
                            key={mode.id}
                            type="button"
                            disabled={isAlreadyAdded}
                            onClick={() =>
                                onPick({
                                    kind: "dialog_scenario",
                                    reference: mode.key,
                                    title: `режим «${mode.title}»`,
                                    persona: EMPTY_PERSONA,
                                })
                            }
                            className="rounded-lg border border-line p-2 text-left text-sm text-ink disabled:opacity-50"
                        >
                            {isAlreadyAdded ? "✓ " : ""}
                            {mode.title}
                            <span className="ml-2 text-xs text-ink-3">{mode.key}</span>
                        </button>
                    );
                })}
            </div>
        </div>
    );
}

function ReferenceMaterialPicker({ items, onPick }: PickerProps) {
    const [searchTerm, setSearchTerm] = useState("");
    const referenceQuery = useReferenceMaterialSearch(searchTerm);

    return (
        <div className="flex flex-col gap-3">
            <TextInput
                label="Поиск"
                placeholder="Название или тема"
                value={searchTerm}
                onChange={(changeEvent) => setSearchTerm(changeEvent.target.value)}
            />

            {referenceQuery.isLoading && <p className="text-sm text-ink-3">Ищем…</p>}
            {referenceQuery.isError && (
                <p className="text-sm" style={{ color: "var(--heart)" }}>
                    Не удалось загрузить справочные материалы.
                </p>
            )}
            {!referenceQuery.isLoading && (referenceQuery.data ?? []).length === 0 && (
                <p className="text-sm text-ink-3">Ничего не нашлось.</p>
            )}

            <div className="flex flex-col gap-1">
                {(referenceQuery.data ?? []).map((material) => {
                    const isAlreadyAdded = containsContentItem(
                        items,
                        "reference_material",
                        material.materialId
                    );

                    return (
                        <button
                            key={material.materialId}
                            type="button"
                            disabled={isAlreadyAdded}
                            onClick={() =>
                                onPick({
                                    kind: "reference_material",
                                    reference: material.materialId,
                                    title: `«${material.title}»`,
                                    persona: null,
                                })
                            }
                            className="rounded-lg border border-line p-2 text-left text-sm text-ink disabled:opacity-50"
                        >
                            {isAlreadyAdded ? "✓ " : ""}
                            {material.title}
                        </button>
                    );
                })}
            </div>
        </div>
    );
}
