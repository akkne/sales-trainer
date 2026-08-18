"use client";

import { Button, IconButton } from "@/shared/components/button";
import { Divider } from "@/shared/components/common";
import { TextInput } from "@/shared/components/input";
import { STRUCTURE_VALUE_MAXIMUM_LENGTH } from "@/features/org-content-generation/constants/generation-dictionary";
import {
    clampStructureValue,
    describeStructureListCount,
    isStructureListAtCap,
    type ContentStructureDraft,
    type StructureListName,
} from "@/features/org-content-generation/utils/structure-draft";

interface StructureEditorProps {
    draft: ContentStructureDraft;
    onDraftChange: (draft: ContentStructureDraft) => void;
    /** False while the run is being re-inspected after an edit — the document is briefly not ours. */
    isDisabled?: boolean;
}

interface ListSectionHeaderProps {
    title: string;
    countLabel: string;
    isAtCap: boolean;
    onAdd: () => void;
    addLabel: string;
    isDisabled: boolean;
}

function ListSectionHeader({
    title,
    countLabel,
    isAtCap,
    onAdd,
    addLabel,
    isDisabled,
}: ListSectionHeaderProps) {
    return (
        <div className="flex items-center justify-between gap-3">
            <h3 className="text-sm font-bold text-ink">
                {title}{" "}
                <span className="mono text-xs font-normal text-ink-3">({countLabel})</span>
            </h3>
            <Button
                variant="ghost"
                size="sm"
                disabled={isDisabled || isAtCap}
                onClick={onAdd}
                aria-label={addLabel}
                title={isAtCap ? "Достигнут предел — дальше сервер всё равно обрежет" : undefined}
            >
                + добавить
            </Button>
        </div>
    );
}

/** «Запрещённые обещания (0 из 20) — пусто». A gap is shown as a gap, never hidden. */
function EmptyListNote() {
    return <p className="text-xs text-ink-3">пусто</p>;
}

/**
 * O11 layout (в) — the checkpoint, and the reason this whole block exists.
 *
 * Two rules run through every field here. **A gap stays a gap**: an empty product renders as an
 * empty field with its cap counter, not as something the model guessed, because a fabricated ICP is
 * indistinguishable on this screen from an extracted one and approving would ratify it. And **every
 * cap is the server's**: the counters read «7 из 10» from `ContentStructureDocumentSerializer`, so
 * «+ добавить» goes grey at the same number the server would silently truncate at.
 *
 * The component is pure presentation over a draft — the debounce, the `PUT` and the «сохранено
 * 14:22» indicator belong to the page, because they are about the run and not about the document.
 */
export function StructureEditor({
    draft,
    onDraftChange,
    isDisabled = false,
}: StructureEditorProps) {
    const updateDraft = (patch: Partial<ContentStructureDraft>) => {
        onDraftChange({ ...draft, ...patch });
    };

    const isAtCap = (listName: StructureListName) => isStructureListAtCap(draft, listName);

    return (
        <div className="flex flex-col gap-5">
            <div className="flex flex-col gap-3">
                <TextInput
                    label="Продукт"
                    value={draft.product}
                    disabled={isDisabled}
                    maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                    placeholder="Что именно вы продаёте"
                    onChange={(changeEvent) =>
                        updateDraft({ product: clampStructureValue(changeEvent.target.value) })
                    }
                />
                <TextInput
                    label="Кому продаём"
                    value={draft.icp}
                    disabled={isDisabled}
                    maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                    placeholder="Сегмент, кто принимает решение, размер сделки"
                    onChange={(changeEvent) =>
                        updateDraft({ icp: clampStructureValue(changeEvent.target.value) })
                    }
                />
                <TextInput
                    label="Тон"
                    value={draft.tone}
                    disabled={isDisabled}
                    maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                    placeholder="Например: консультативный, с опорой на цифры"
                    onChange={(changeEvent) =>
                        updateDraft({ tone: clampStructureValue(changeEvent.target.value) })
                    }
                />
            </div>

            <Divider />

            <section className="flex flex-col gap-3">
                <ListSectionHeader
                    title="Возражения"
                    countLabel={describeStructureListCount(draft, "objections")}
                    isAtCap={isAtCap("objections")}
                    isDisabled={isDisabled}
                    addLabel="Добавить возражение"
                    onAdd={() =>
                        updateDraft({
                            objections: [...draft.objections, { text: "", bestResponse: "" }],
                        })
                    }
                />

                {draft.objections.length === 0 && <EmptyListNote />}

                {draft.objections.map((objection, objectionIndex) => (
                    <div
                        key={`objection-${objectionIndex}`}
                        className="flex flex-col gap-2 sm:flex-row sm:items-start"
                    >
                        <div className="flex-1 min-w-0">
                            <TextInput
                                value={objection.text}
                                disabled={isDisabled}
                                maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                                placeholder="«Дорого»"
                                aria-label={`Возражение ${objectionIndex + 1}`}
                                onChange={(changeEvent) =>
                                    updateDraft({
                                        objections: draft.objections.map((candidate, index) =>
                                            index === objectionIndex
                                                ? {
                                                      ...candidate,
                                                      text: clampStructureValue(
                                                          changeEvent.target.value
                                                      ),
                                                  }
                                                : candidate
                                        ),
                                    })
                                }
                            />
                        </div>
                        <div className="flex-1 min-w-0">
                            <TextInput
                                value={objection.bestResponse}
                                disabled={isDisabled}
                                maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                                placeholder="Ответ, который работает"
                                aria-label={`Ответ на возражение ${objectionIndex + 1}`}
                                onChange={(changeEvent) =>
                                    updateDraft({
                                        objections: draft.objections.map((candidate, index) =>
                                            index === objectionIndex
                                                ? {
                                                      ...candidate,
                                                      bestResponse: clampStructureValue(
                                                          changeEvent.target.value
                                                      ),
                                                  }
                                                : candidate
                                        ),
                                    })
                                }
                            />
                        </div>
                        <IconButton
                            icon="close"
                            variant="ghost"
                            size="md"
                            disabled={isDisabled}
                            aria-label={`Убрать возражение ${objectionIndex + 1}`}
                            onClick={() =>
                                updateDraft({
                                    objections: draft.objections.filter(
                                        (_, index) => index !== objectionIndex
                                    ),
                                })
                            }
                        />
                    </div>
                ))}
            </section>

            <Divider />

            <section className="flex flex-col gap-3">
                <ListSectionHeader
                    title="Этапы скрипта"
                    countLabel={describeStructureListCount(draft, "scriptStages")}
                    isAtCap={isAtCap("scriptStages")}
                    isDisabled={isDisabled}
                    addLabel="Добавить этап скрипта"
                    onAdd={() => updateDraft({ scriptStages: [...draft.scriptStages, ""] })}
                />

                {draft.scriptStages.length === 0 && <EmptyListNote />}

                {draft.scriptStages.map((stage, stageIndex) => (
                    <div key={`stage-${stageIndex}`} className="flex items-start gap-2">
                        <div className="flex-1 min-w-0">
                            <TextInput
                                value={stage}
                                disabled={isDisabled}
                                maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                                placeholder="Приветствие"
                                aria-label={`Этап скрипта ${stageIndex + 1}`}
                                onChange={(changeEvent) =>
                                    updateDraft({
                                        scriptStages: draft.scriptStages.map((candidate, index) =>
                                            index === stageIndex
                                                ? clampStructureValue(changeEvent.target.value)
                                                : candidate
                                        ),
                                    })
                                }
                            />
                        </div>
                        <IconButton
                            icon="close"
                            variant="ghost"
                            size="md"
                            disabled={isDisabled}
                            aria-label={`Убрать этап скрипта ${stageIndex + 1}`}
                            onClick={() =>
                                updateDraft({
                                    scriptStages: draft.scriptStages.filter(
                                        (_, index) => index !== stageIndex
                                    ),
                                })
                            }
                        />
                    </div>
                ))}
            </section>

            <Divider />

            <section className="flex flex-col gap-3">
                <ListSectionHeader
                    title="Глоссарий"
                    countLabel={describeStructureListCount(draft, "glossaryEntries")}
                    isAtCap={isAtCap("glossaryEntries")}
                    isDisabled={isDisabled}
                    addLabel="Добавить термин"
                    onAdd={() =>
                        updateDraft({
                            glossaryEntries: [
                                ...draft.glossaryEntries,
                                { term: "", definition: "" },
                            ],
                        })
                    }
                />

                {draft.glossaryEntries.length === 0 && <EmptyListNote />}

                {draft.glossaryEntries.map((entry, entryIndex) => (
                    <div
                        key={`glossary-${entryIndex}`}
                        className="flex flex-col gap-2 sm:flex-row sm:items-start"
                    >
                        <div className="sm:w-56 min-w-0">
                            <TextInput
                                value={entry.term}
                                disabled={isDisabled}
                                maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                                placeholder="СДЭК"
                                aria-label={`Термин ${entryIndex + 1}`}
                                onChange={(changeEvent) =>
                                    updateDraft({
                                        glossaryEntries: draft.glossaryEntries.map(
                                            (candidate, index) =>
                                                index === entryIndex
                                                    ? {
                                                          ...candidate,
                                                          term: clampStructureValue(
                                                              changeEvent.target.value
                                                          ),
                                                      }
                                                    : candidate
                                        ),
                                    })
                                }
                            />
                        </div>
                        <div className="flex-1 min-w-0">
                            <TextInput
                                value={entry.definition}
                                disabled={isDisabled}
                                maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                                placeholder="Что это значит у вас"
                                aria-label={`Значение термина ${entryIndex + 1}`}
                                onChange={(changeEvent) =>
                                    updateDraft({
                                        glossaryEntries: draft.glossaryEntries.map(
                                            (candidate, index) =>
                                                index === entryIndex
                                                    ? {
                                                          ...candidate,
                                                          definition: clampStructureValue(
                                                              changeEvent.target.value
                                                          ),
                                                      }
                                                    : candidate
                                        ),
                                    })
                                }
                            />
                        </div>
                        <IconButton
                            icon="close"
                            variant="ghost"
                            size="md"
                            disabled={isDisabled}
                            aria-label={`Убрать термин ${entryIndex + 1}`}
                            onClick={() =>
                                updateDraft({
                                    glossaryEntries: draft.glossaryEntries.filter(
                                        (_, index) => index !== entryIndex
                                    ),
                                })
                            }
                        />
                    </div>
                ))}
            </section>

            <Divider />

            <section className="flex flex-col gap-3">
                <ListSectionHeader
                    title="Запрещённые обещания"
                    countLabel={describeStructureListCount(draft, "bannedClaims")}
                    isAtCap={isAtCap("bannedClaims")}
                    isDisabled={isDisabled}
                    addLabel="Добавить запрещённое обещание"
                    onAdd={() => updateDraft({ bannedClaims: [...draft.bannedClaims, ""] })}
                />

                <p className="text-xs text-ink-3">
                    Ни один правильный ответ в упражнениях не будет содержать эти формулировки.
                </p>

                {draft.bannedClaims.length === 0 && <EmptyListNote />}

                {draft.bannedClaims.map((claim, claimIndex) => (
                    <div key={`banned-claim-${claimIndex}`} className="flex items-start gap-2">
                        <div className="flex-1 min-w-0">
                            <TextInput
                                value={claim}
                                disabled={isDisabled}
                                maxLength={STRUCTURE_VALUE_MAXIMUM_LENGTH}
                                placeholder="гарантированная доходность"
                                aria-label={`Запрещённое обещание ${claimIndex + 1}`}
                                onChange={(changeEvent) =>
                                    updateDraft({
                                        bannedClaims: draft.bannedClaims.map((candidate, index) =>
                                            index === claimIndex
                                                ? clampStructureValue(changeEvent.target.value)
                                                : candidate
                                        ),
                                    })
                                }
                            />
                        </div>
                        <IconButton
                            icon="close"
                            variant="ghost"
                            size="md"
                            disabled={isDisabled}
                            aria-label={`Убрать запрещённое обещание ${claimIndex + 1}`}
                            onClick={() =>
                                updateDraft({
                                    bannedClaims: draft.bannedClaims.filter(
                                        (_, index) => index !== claimIndex
                                    ),
                                })
                            }
                        />
                    </div>
                ))}
            </section>
        </div>
    );
}
