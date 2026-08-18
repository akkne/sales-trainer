"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Modal } from "@/shared/components/modal";
import { TextInput, Textarea } from "@/shared/components/input";
import { MATERIAL_MAXIMUM_LENGTH } from "@/features/org-content-generation/constants/generation-dictionary";
import {
    validateStartMaterial,
    validateStartTitle,
} from "@/features/org-content-generation/utils/api-failure";

interface StartGenerationModalProps {
    onClose: () => void;
    onStart: (request: { title: string; material: string }) => void;
    isPending: boolean;
    startErrorMessage: string | null;
}

/**
 * O10's «Сделать урок из материалов».
 *
 * **Client-side validation is emptiness and the 60 000-character ceiling, and nothing else.** Thin
 * material is not a form error: it becomes a run in `insufficient` carrying a list of what to
 * bring, and refusing it here would replace an answerable refusal with a red field and teach
 * nobody anything (docs/CONTENT_PIPELINE.md §4a).
 *
 * Pasted text, not a file: parsing uploads and the call recordings that would make it worth
 * building are roadmap 40.30, and there is no upload route to call.
 *
 * Mounted by the caller only while it is open, so «отмена» throws the draft away by unmounting
 * rather than by a reset nobody would remember to keep in step with the fields.
 */
export function StartGenerationModal({
    onClose,
    onStart,
    isPending,
    startErrorMessage,
}: StartGenerationModalProps) {
    const [title, setTitle] = useState("");
    const [material, setMaterial] = useState("");
    const [titleError, setTitleError] = useState<string | null>(null);
    const [materialError, setMaterialError] = useState<string | null>(null);

    const handleStart = () => {
        const nextTitleError = validateStartTitle(title);
        const nextMaterialError = validateStartMaterial(material);
        setTitleError(nextTitleError);
        setMaterialError(nextMaterialError);
        if (nextTitleError || nextMaterialError) return;

        onStart({ title: title.trim(), material });
    };

    return (
        <Modal
            open
            onClose={onClose}
            title="Сделать урок из материалов"
            size="lg"
            footer={
                <>
                    <Button variant="ghost" size="md" onClick={onClose} disabled={isPending}>
                        Отмена
                    </Button>
                    <Button variant="primary" size="md" loading={isPending} onClick={handleStart}>
                        Разобрать материал
                    </Button>
                </>
            }
        >
            <p className="text-sm text-ink-3">
                ИИ прочитает материал и покажет, что он в нём увидел: продукт, клиента, возражения,
                этапы скрипта. Упражнения появятся только после того, как вы это подтвердите.
            </p>

            <div className="mt-4 flex flex-col gap-4">
                <TextInput
                    label="Название"
                    required
                    value={title}
                    error={titleError ?? undefined}
                    hint="По нему вы найдёте урок потом — например «Возражения по цене, октябрь»."
                    placeholder="Возражения по цене, октябрь"
                    onChange={(changeEvent) => setTitle(changeEvent.target.value)}
                />

                <Textarea
                    label="Материал"
                    required
                    rows={12}
                    value={material}
                    maxLength={MATERIAL_MAXIMUM_LENGTH}
                    error={materialError ?? undefined}
                    hint={`Текст презентации, скрипт звонка, расшифровка разговора, заметки с планёрки. ${material.length.toLocaleString("ru-RU")} из ${MATERIAL_MAXIMUM_LENGTH.toLocaleString("ru-RU")} символов.`}
                    placeholder="Вставьте текст материалов…"
                    onChange={(changeEvent) => setMaterial(changeEvent.target.value)}
                />
            </div>

            {startErrorMessage && (
                <p className="mt-3 text-xs" style={{ color: "var(--bad)" }} role="alert">
                    {startErrorMessage}
                </p>
            )}
        </Modal>
    );
}
