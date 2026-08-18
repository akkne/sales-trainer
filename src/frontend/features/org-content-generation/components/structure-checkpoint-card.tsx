"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import { Divider } from "@/shared/components/common";
import { StructureEditor } from "@/features/org-content-generation/components/structure-editor";
import { STRUCTURE_AUTOSAVE_DEBOUNCE_MILLISECONDS } from "@/features/org-content-generation/constants/generation-dictionary";
import { useUpdateJobStructure } from "@/features/org-content-generation/hooks/use-content-generation";
import { describeContentGenerationFailure } from "@/features/org-content-generation/utils/api-failure";
import {
    formatSavedAtTime,
    hasStructurePayloadChanged,
    toStructureDraft,
    toStructurePayload,
    type ContentStructureDraft,
} from "@/features/org-content-generation/utils/structure-draft";
import type { ContentStructure } from "@/features/org-content-generation/types/content-generation";

interface StructureCheckpointCardProps {
    jobId: string;
    /** The document as the server last handed it over. Owned from here on by the local draft. */
    structure: ContentStructure | null;
    /** True only at `awaiting_review`: on a refused run the same editor has nothing to approve yet. */
    canApprove: boolean;
    onApprove: () => void;
    isApprovePending: boolean;
    approveErrorMessage: string | null;
    onFillProfile: () => void;
}

/**
 * O11 layout (в) — the checkpoint, with its autosave.
 *
 * **The caller must key this component on the run's `structuredAt`.** The draft is seeded once, at
 * mount, and a structuring pass that lands while somebody is looking replaces the document
 * underneath them — remounting is what re-seeds it. Without that, a run refused before anything was
 * extracted (`structure: null`) would hold an empty draft, and the autosave would write it over the
 * structure the added material just produced.
 *
 * Everything above the divider costs seconds; the one button below it starts spending money, which
 * is why it is the only `variant="primary"` on the screen and why it is labelled with what it costs
 * rather than with what it does.
 */
export function StructureCheckpointCard({
    jobId,
    structure,
    canApprove,
    onApprove,
    isApprovePending,
    approveErrorMessage,
    onFillProfile,
}: StructureCheckpointCardProps) {
    const updateStructure = useUpdateJobStructure(jobId);

    const [draft, setDraft] = useState<ContentStructureDraft>(() => toStructureDraft(structure));
    const [savedAtLabel, setSavedAtLabel] = useState<string | null>(null);

    /**
     * What the server currently holds, so the debounce can tell «ничего не изменилось» from «ещё не
     * сохранили». A ref rather than state because changing it must not re-run the debounce.
     */
    const lastSavedStructureRef = useRef<ContentStructure | null>(structure);

    const saveStructure = updateStructure.mutate;

    useEffect(() => {
        if (!hasStructurePayloadChanged(draft, lastSavedStructureRef.current)) return;

        const debounceHandle = window.setTimeout(() => {
            saveStructure(toStructurePayload(draft), {
                onSuccess: (savedJob) => {
                    lastSavedStructureRef.current = savedJob.structure;
                    setSavedAtLabel(formatSavedAtTime(new Date()));
                },
            });
        }, STRUCTURE_AUTOSAVE_DEBOUNCE_MILLISECONDS);

        return () => window.clearTimeout(debounceHandle);
    }, [draft, saveStructure]);

    return (
        <Card padding={24}>
            <h2 className="text-base font-bold text-ink">Всё верно? Что убрать, что добавить?</h2>
            <p className="mt-1 text-sm text-ink-3">
                Правка здесь стоит тридцать секунд. Та же правка после генерации — это переписывание
                пятнадцати упражнений.
            </p>

            <div className="mt-5">
                <StructureEditor draft={draft} onDraftChange={setDraft} isDisabled={isApprovePending} />
            </div>

            <div className="mt-5 flex flex-wrap items-center justify-between gap-2">
                <p className="text-xs text-ink-3" aria-live="polite">
                    Сохраняется автоматически.
                    {updateStructure.isPending && " Сохраняем…"}
                    {!updateStructure.isPending && savedAtLabel && ` Сохранено ${savedAtLabel}`}
                </p>
                {updateStructure.isError && (
                    <p className="text-xs" style={{ color: "var(--bad)" }} role="alert">
                        {describeContentGenerationFailure(updateStructure.error, "saveStructure")}
                    </p>
                )}
            </div>

            <Divider className="my-5" />

            <div className="flex flex-wrap items-center justify-between gap-3">
                <Button variant="ghost" size="md" onClick={onFillProfile}>
                    Заполнить профиль компании из этой структуры
                </Button>

                {canApprove && (
                    <div className="flex flex-col items-end gap-1">
                        <Button
                            variant="primary"
                            size="lg"
                            loading={isApprovePending}
                            onClick={onApprove}
                        >
                            Сгенерировать упражнения
                        </Button>
                        <span className="text-xs text-ink-3">
                            Дальше начнётся платная генерация.
                        </span>
                    </div>
                )}
            </div>

            {approveErrorMessage && (
                <p className="mt-3 text-xs" style={{ color: "var(--bad)" }} role="alert">
                    {approveErrorMessage}
                </p>
            )}
        </Card>
    );
}
