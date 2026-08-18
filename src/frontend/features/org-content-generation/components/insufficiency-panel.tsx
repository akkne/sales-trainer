"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import { Icon } from "@/shared/components/icon";
import { Textarea } from "@/shared/components/input";
import {
    MATERIAL_MAXIMUM_LENGTH,
    describeSufficiencyGapMessage,
} from "@/features/org-content-generation/constants/generation-dictionary";
import { readableSufficiencyGaps } from "@/features/org-content-generation/utils/job-state";
import { validateStartMaterial } from "@/features/org-content-generation/utils/api-failure";
import type { ContentInsufficiency } from "@/features/org-content-generation/types/content-generation";

interface InsufficiencyPanelProps {
    insufficiency: ContentInsufficiency;
    /** True only at `stage: "structure"` — at `stage: "material"` there is no structure to open. */
    canOpenStructure: boolean;
    onOpenStructure: () => void;
    onSupplementMaterial: (material: string) => void;
    isSupplementPending: boolean;
    supplementErrorMessage: string | null;
}

/**
 * O11 layout (б) — the refusal, as a screen and not as a toast.
 *
 * **There is no «сгенерировать всё равно», and no route behind one.** The threshold and the
 * checkpoint are the two things standing between a thin deck and fifteen bland exercises the
 * customer blames us for; a bypass button cancels both, and the backend would answer it with the
 * same 409 anyway. The refusal is arguable instead, by two doors that both cost almost nothing:
 * add the material that was missing, or type the four objections you already know.
 *
 * The model's `note` is not rendered. It is a developer's diagnostic; the customer's text is the
 * gap list, which is a list of bullets precisely because usually only one of them is actionable
 * today.
 */
export function InsufficiencyPanel({
    insufficiency,
    canOpenStructure,
    onOpenStructure,
    onSupplementMaterial,
    isSupplementPending,
    supplementErrorMessage,
}: InsufficiencyPanelProps) {
    const [supplementText, setSupplementText] = useState("");
    const [validationMessage, setValidationMessage] = useState<string | null>(null);

    const gaps = readableSufficiencyGaps(insufficiency);

    const handleSupplement = () => {
        const failure = validateStartMaterial(supplementText);
        setValidationMessage(failure);
        if (failure) return;

        onSupplementMaterial(supplementText);
        setSupplementText("");
    };

    return (
        <div className="flex flex-col gap-4">
            <Card padding={20} style={{ background: "var(--warn-soft)", border: "none" }}>
                <div className="flex items-start gap-3">
                    <Icon name="warning" size="md" style={{ color: "oklch(0.45 0.10 80)" }} />
                    <div className="min-w-0">
                        <h2 className="text-base font-bold text-ink">
                            Из этого материала хороших упражнений не получится
                        </h2>

                        <ul className="mt-3 flex flex-col gap-2">
                            {gaps.map((gap) => (
                                <li key={gap.code} className="flex gap-2 text-sm text-ink-2">
                                    <span aria-hidden="true">•</span>
                                    <span>{describeSufficiencyGapMessage(gap.code, gap.message)}</span>
                                </li>
                            ))}
                        </ul>

                        <p className="mt-4 text-sm text-ink-2">
                            Четыре хороших упражнения лучше пятнадцати водянистых, поэтому мы не
                            генерируем на таком материале. Есть два пути.
                        </p>
                    </div>
                </div>
            </Card>

            <Card padding={20}>
                <h3 className="text-sm font-bold text-ink">Добавить материал</h3>
                <p className="mt-1 text-xs text-ink-3">
                    Прочитаем только то, что вы допишете — за уже разобранное платить второй раз не
                    придётся.
                </p>

                <div className="mt-3">
                    <Textarea
                        rows={6}
                        value={supplementText}
                        maxLength={MATERIAL_MAXIMUM_LENGTH}
                        placeholder="Скрипт звонка, список возражений, расшифровка разговора…"
                        onChange={(changeEvent) => setSupplementText(changeEvent.target.value)}
                        error={validationMessage ?? undefined}
                        aria-label="Дополнительный материал"
                    />
                </div>

                <div className="mt-3 flex items-center justify-end gap-3">
                    <Button
                        variant="outline"
                        size="md"
                        loading={isSupplementPending}
                        onClick={handleSupplement}
                    >
                        Добавить
                    </Button>
                </div>

                {supplementErrorMessage && (
                    <p className="mt-2 text-xs" style={{ color: "var(--bad)" }} role="alert">
                        {supplementErrorMessage}
                    </p>
                )}
            </Card>

            {canOpenStructure && (
                <Card padding={20}>
                    <h3 className="text-sm font-bold text-ink">Или впишите сами</h3>
                    <p className="mt-1 text-xs text-ink-3">
                        Если вы знаете свои четыре возражения — просто напишите их. Мы перечитаем
                        структуру и, если её станет достаточно, вернём прогон к проверке.
                    </p>
                    <div className="mt-3 flex justify-end">
                        <Button variant="outline" size="md" onClick={onOpenStructure}>
                            Открыть структуру
                        </Button>
                    </div>
                </Card>
            )}
        </div>
    );
}
