"use client";

import { useState } from "react";
import { Button, IconButton } from "@/shared/components/button";
import { Card, CardContent } from "@/shared/components/card";
import { ConfirmDialog } from "@/shared/components/confirm-dialog";
import { Chip } from "@/shared/components/common";
import { TextInput, Textarea } from "@/shared/components/input";
import {
    ORGANIZATION_PROFILE_FIELDS,
    PROFILE_FIELD_LABELS,
    TONE_SUGGESTIONS,
} from "../constants/profile-fields";
import type {
    OrganizationProfile,
    UpdateOrganizationProfileRequest,
} from "../types/organization-profile";
import {
    findRemovedBannedClaims,
    moveListItem,
    toProfileFormState,
    toUpdateProfileRequest,
    validateProfileForm,
    type ProfileFormState,
} from "../utils/profile-form";

interface ProfileFullFormProps {
    profile: OrganizationProfile | null;
    isSaving: boolean;
    saveError: string | null;
    onSave: (request: UpdateOrganizationProfileRequest) => void;
    onClose: () => void;
}

function FormSection({
    fieldCode,
    description,
    error,
    children,
}: {
    fieldCode: string;
    description?: string;
    error?: string;
    children: React.ReactNode;
}) {
    return (
        <section className="py-5 border-t border-line first:border-t-0 first:pt-0">
            <h3 className="text-sm font-medium text-ink mb-1">
                {PROFILE_FIELD_LABELS[fieldCode] ?? fieldCode}
            </h3>
            {description && <p className="text-xs text-ink-3 mb-3">{description}</p>}
            {children}
            {error && (
                <p className="mt-2 text-xs text-bad" role="alert">
                    {error}
                </p>
            )}
        </section>
    );
}

/**
 * All seven fields at once, saved with one `PUT`.
 *
 * It is hidden behind a link rather than shown by default because thirty empty inputs is the state
 * the interview exists to avoid. It stays reachable because it is the only place two things can
 * happen: clearing a field, and removing a banned claim — neither of which any other path into the
 * profile can do, deliberately.
 */
export function ProfileFullForm({
    profile,
    isSaving,
    saveError,
    onSave,
    onClose,
}: ProfileFullFormProps) {
    const [formState, setFormState] = useState<ProfileFormState>(() =>
        toProfileFormState(profile)
    );
    const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
    const [claimPendingRemovalIndex, setClaimPendingRemovalIndex] = useState<number | null>(null);
    const [claimsPendingConfirmation, setClaimsPendingConfirmation] = useState<string[] | null>(
        null
    );

    const storedBannedClaims = profile?.bannedClaims ?? [];

    const patchFormState = (patch: Partial<ProfileFormState>) =>
        setFormState((current) => ({ ...current, ...patch }));

    const submit = () => {
        const nextValidationErrors = validateProfileForm(formState);
        setValidationErrors(nextValidationErrors);
        if (Object.keys(nextValidationErrors).length > 0) return;

        const removedClaims = findRemovedBannedClaims(
            storedBannedClaims,
            formState.bannedClaims
        );
        if (removedClaims.length > 0) {
            setClaimsPendingConfirmation(removedClaims);
            return;
        }

        onSave(toUpdateProfileRequest(formState));
    };

    const confirmSaveWithRemovedClaims = () => {
        setClaimsPendingConfirmation(null);
        onSave(toUpdateProfileRequest(formState));
    };

    return (
        <Card>
            <CardContent>
                <div className="rounded-xl bg-warn-soft/60 border border-line p-3 mb-5">
                    <p className="text-xs text-ink-2">
                        Здесь сохраняются все поля разом. Если над профилем работает кто-то ещё,
                        безопаснее отвечать по одному вопросу в интервью.
                    </p>
                </div>

                <FormSection
                    fieldCode={ORGANIZATION_PROFILE_FIELDS.product}
                    description="Что вы продаёте — так, как объяснили бы новому менеджеру в первый день."
                >
                    <Textarea
                        value={formState.product}
                        rows={3}
                        onChange={(event) => patchFormState({ product: event.target.value })}
                        disabled={isSaving}
                    />
                </FormSection>

                <FormSection
                    fieldCode={ORGANIZATION_PROFILE_FIELDS.icp}
                    description="Сегмент, кто принимает решение, средний размер сделки."
                >
                    <Textarea
                        value={formState.icp}
                        rows={3}
                        onChange={(event) => patchFormState({ icp: event.target.value })}
                        disabled={isSaving}
                    />
                </FormSection>

                <FormSection
                    fieldCode={ORGANIZATION_PROFILE_FIELDS.objections}
                    description="Возражения, которые слышат ваши менеджеры, и лучший ответ на каждое."
                    error={validationErrors[ORGANIZATION_PROFILE_FIELDS.objections]}
                >
                    <div className="space-y-3">
                        {formState.objections.map((objection, index) => (
                            <div key={index} className="rounded-xl border border-line p-3 space-y-2">
                                <div className="flex items-center gap-2">
                                    <div className="flex-1 min-w-0">
                                        <TextInput
                                            value={objection.text}
                                            inputSize="sm"
                                            placeholder="Возражение"
                                            disabled={isSaving}
                                            onChange={(event) =>
                                                patchFormState({
                                                    objections: formState.objections.map(
                                                        (entry, entryIndex) =>
                                                            entryIndex === index
                                                                ? {
                                                                      ...entry,
                                                                      text: event.target.value,
                                                                  }
                                                                : entry
                                                    ),
                                                })
                                            }
                                        />
                                    </div>
                                    <IconButton
                                        icon="delete"
                                        variant="ghost"
                                        size="sm"
                                        aria-label={`Удалить возражение ${index + 1}`}
                                        disabled={isSaving}
                                        onClick={() =>
                                            patchFormState({
                                                objections: formState.objections.filter(
                                                    (_, entryIndex) => entryIndex !== index
                                                ),
                                            })
                                        }
                                    />
                                </div>
                                <TextInput
                                    value={objection.bestResponse}
                                    inputSize="sm"
                                    placeholder="Лучший ответ (необязательно)"
                                    disabled={isSaving}
                                    onChange={(event) =>
                                        patchFormState({
                                            objections: formState.objections.map(
                                                (entry, entryIndex) =>
                                                    entryIndex === index
                                                        ? {
                                                              ...entry,
                                                              bestResponse: event.target.value,
                                                          }
                                                        : entry
                                            ),
                                        })
                                    }
                                />
                            </div>
                        ))}
                        <Button
                            variant="ghost"
                            size="sm"
                            iconLeft="plus"
                            disabled={isSaving}
                            onClick={() =>
                                patchFormState({
                                    objections: [
                                        ...formState.objections,
                                        { text: "", frequency: "", bestResponse: "" },
                                    ],
                                })
                            }
                        >
                            Ещё возражение
                        </Button>
                    </div>
                </FormSection>

                <FormSection
                    fieldCode={ORGANIZATION_PROFILE_FIELDS.scriptStages}
                    description="Порядок важен: этапы подставляются в уроки одной строкой через стрелки."
                >
                    <div className="space-y-2">
                        {formState.scriptStages.map((stage, index) => (
                            <div key={index} className="flex items-center gap-2">
                                <span className="w-6 shrink-0 text-center text-xs text-ink-3 font-mono tabular-nums">
                                    {index + 1}
                                </span>
                                <div className="flex-1 min-w-0">
                                    <TextInput
                                        value={stage}
                                        inputSize="sm"
                                        disabled={isSaving}
                                        onChange={(event) =>
                                            patchFormState({
                                                scriptStages: formState.scriptStages.map(
                                                    (entry, entryIndex) =>
                                                        entryIndex === index
                                                            ? event.target.value
                                                            : entry
                                                ),
                                            })
                                        }
                                    />
                                </div>
                                <IconButton
                                    icon="arrow-up"
                                    variant="ghost"
                                    size="sm"
                                    aria-label={`Поднять этап ${index + 1}`}
                                    disabled={isSaving || index === 0}
                                    onClick={() =>
                                        patchFormState({
                                            scriptStages: moveListItem(
                                                formState.scriptStages,
                                                index,
                                                index - 1
                                            ),
                                        })
                                    }
                                />
                                <IconButton
                                    icon="delete"
                                    variant="ghost"
                                    size="sm"
                                    aria-label={`Удалить этап ${index + 1}`}
                                    disabled={isSaving}
                                    onClick={() =>
                                        patchFormState({
                                            scriptStages: formState.scriptStages.filter(
                                                (_, entryIndex) => entryIndex !== index
                                            ),
                                        })
                                    }
                                />
                            </div>
                        ))}
                        <Button
                            variant="ghost"
                            size="sm"
                            iconLeft="plus"
                            disabled={isSaving}
                            onClick={() =>
                                patchFormState({ scriptStages: [...formState.scriptStages, ""] })
                            }
                        >
                            Ещё этап
                        </Button>
                    </div>
                </FormSection>

                <FormSection
                    fieldCode={ORGANIZATION_PROFILE_FIELDS.tone}
                    description="Как ваши менеджеры разговаривают с клиентом. Свободный текст."
                >
                    <div className="space-y-2">
                        <Textarea
                            value={formState.tone}
                            rows={2}
                            disabled={isSaving}
                            onChange={(event) => patchFormState({ tone: event.target.value })}
                        />
                        <div className="flex flex-wrap gap-2">
                            {TONE_SUGGESTIONS.map((suggestion) => (
                                <Chip
                                    key={suggestion}
                                    disabled={isSaving}
                                    onClick={() =>
                                        patchFormState({
                                            tone:
                                                formState.tone.trim().length > 0
                                                    ? formState.tone
                                                    : suggestion,
                                        })
                                    }
                                >
                                    {suggestion}
                                </Chip>
                            ))}
                        </div>
                    </div>
                </FormSection>

                <FormSection
                    fieldCode={ORGANIZATION_PROFILE_FIELDS.bannedClaims}
                    description="Фразы, которые запрещено обещать клиенту. Собеседник-ИИ их не произнесёт, а проверяющий снизит за них оценку. Это единственное место, где запрет можно снять."
                    error={validationErrors[ORGANIZATION_PROFILE_FIELDS.bannedClaims]}
                >
                    <div className="space-y-2">
                        {formState.bannedClaims.length === 0 && (
                            <p className="text-xs text-ink-4">Список пуст — ничего не запрещено.</p>
                        )}
                        {formState.bannedClaims.map((claim, index) => (
                            <div key={index} className="flex items-center gap-2">
                                <span className="w-6 shrink-0 text-center text-bad" aria-hidden>
                                    ✕
                                </span>
                                <div className="flex-1 min-w-0">
                                    <TextInput
                                        value={claim}
                                        inputSize="sm"
                                        placeholder="Например: гарантируем рост выручки на 30%"
                                        disabled={isSaving}
                                        onChange={(event) =>
                                            patchFormState({
                                                bannedClaims: formState.bannedClaims.map(
                                                    (entry, entryIndex) =>
                                                        entryIndex === index
                                                            ? event.target.value
                                                            : entry
                                                ),
                                            })
                                        }
                                    />
                                </div>
                                <IconButton
                                    icon="delete"
                                    variant="ghost"
                                    size="sm"
                                    aria-label={`Снять запрет ${index + 1}`}
                                    disabled={isSaving}
                                    onClick={() => setClaimPendingRemovalIndex(index)}
                                />
                            </div>
                        ))}
                        <Button
                            variant="ghost"
                            size="sm"
                            iconLeft="plus"
                            disabled={isSaving}
                            onClick={() =>
                                patchFormState({ bannedClaims: [...formState.bannedClaims, ""] })
                            }
                        >
                            Ещё запрет
                        </Button>
                    </div>
                </FormSection>

                <FormSection
                    fieldCode={ORGANIZATION_PROFILE_FIELDS.glossary}
                    description="Ваши слова вместо общих: «сделка» → «проект»."
                    error={validationErrors[ORGANIZATION_PROFILE_FIELDS.glossary]}
                >
                    <div className="space-y-2">
                        {formState.glossaryEntries.map((entry, index) => (
                            <div key={index} className="flex items-center gap-2">
                                <div className="w-40 shrink-0">
                                    <TextInput
                                        value={entry.term}
                                        inputSize="sm"
                                        placeholder="термин"
                                        disabled={isSaving}
                                        onChange={(event) =>
                                            patchFormState({
                                                glossaryEntries: formState.glossaryEntries.map(
                                                    (glossaryEntry, entryIndex) =>
                                                        entryIndex === index
                                                            ? {
                                                                  ...glossaryEntry,
                                                                  term: event.target.value,
                                                              }
                                                            : glossaryEntry
                                                ),
                                            })
                                        }
                                    />
                                </div>
                                <span className="text-ink-3" aria-hidden>
                                    →
                                </span>
                                <div className="flex-1 min-w-0">
                                    <TextInput
                                        value={entry.definition}
                                        inputSize="sm"
                                        placeholder="значение"
                                        disabled={isSaving}
                                        onChange={(event) =>
                                            patchFormState({
                                                glossaryEntries: formState.glossaryEntries.map(
                                                    (glossaryEntry, entryIndex) =>
                                                        entryIndex === index
                                                            ? {
                                                                  ...glossaryEntry,
                                                                  definition: event.target.value,
                                                              }
                                                            : glossaryEntry
                                                ),
                                            })
                                        }
                                    />
                                </div>
                                <IconButton
                                    icon="delete"
                                    variant="ghost"
                                    size="sm"
                                    aria-label={`Удалить термин ${index + 1}`}
                                    disabled={isSaving}
                                    onClick={() =>
                                        patchFormState({
                                            glossaryEntries: formState.glossaryEntries.filter(
                                                (_, entryIndex) => entryIndex !== index
                                            ),
                                        })
                                    }
                                />
                            </div>
                        ))}
                        <Button
                            variant="ghost"
                            size="sm"
                            iconLeft="plus"
                            disabled={isSaving}
                            onClick={() =>
                                patchFormState({
                                    glossaryEntries: [
                                        ...formState.glossaryEntries,
                                        { term: "", definition: "" },
                                    ],
                                })
                            }
                        >
                            Ещё термин
                        </Button>
                    </div>
                </FormSection>

                {saveError && (
                    <p className="mt-4 text-xs text-bad" role="alert">
                        {saveError}
                    </p>
                )}

                <div className="mt-6 flex flex-wrap items-center justify-end gap-2">
                    <Button variant="secondary" onClick={onClose} disabled={isSaving}>
                        Вернуться к интервью
                    </Button>
                    <Button
                        variant="primary"
                        loading={isSaving}
                        disabled={isSaving}
                        onClick={submit}
                    >
                        {isSaving ? "Сохраняем…" : "Сохранить"}
                    </Button>
                </div>
            </CardContent>

            <ConfirmDialog
                open={claimPendingRemovalIndex !== null}
                title="Снять запрет?"
                tone="danger"
                confirmLabel="Снять запрет"
                isPending={false}
                body={
                    <span>
                        После сохранения собеседник-ИИ снова сможет произнести{" "}
                        <b>
                            «
                            {claimPendingRemovalIndex !== null
                                ? formState.bannedClaims[claimPendingRemovalIndex]
                                : ""}
                            »
                        </b>
                        , а проверяющий перестанет снижать за это оценку.
                    </span>
                }
                onCancel={() => setClaimPendingRemovalIndex(null)}
                onConfirm={() => {
                    if (claimPendingRemovalIndex === null) return;
                    patchFormState({
                        bannedClaims: formState.bannedClaims.filter(
                            (_, entryIndex) => entryIndex !== claimPendingRemovalIndex
                        ),
                    });
                    setClaimPendingRemovalIndex(null);
                }}
            />

            <ConfirmDialog
                open={claimsPendingConfirmation !== null}
                title="Сохранить и снять запреты?"
                tone="danger"
                confirmLabel="Снять и сохранить"
                isPending={isSaving}
                body={
                    <div className="space-y-2">
                        <p>Эти обещания перестанут быть запрещёнными:</p>
                        <ul className="space-y-1">
                            {(claimsPendingConfirmation ?? []).map((claim) => (
                                <li key={claim} className="text-ink">
                                    «{claim}»
                                </li>
                            ))}
                        </ul>
                    </div>
                }
                onCancel={() => setClaimsPendingConfirmation(null)}
                onConfirm={confirmSaveWithRemovedClaims}
            />
        </Card>
    );
}
