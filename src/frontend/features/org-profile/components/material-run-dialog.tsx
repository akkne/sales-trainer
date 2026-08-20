"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Modal } from "@/shared/components/modal";
import { TextInput, Textarea } from "@/shared/components/input";

const MINIMUM_MATERIAL_LENGTH = 200;

interface MaterialRunDialogProps {
    open: boolean;
    isStarting: boolean;
    startError: string | null;
    onStart: (title: string, material: string) => void;
    onClose: () => void;
}

/**
 * «Заполнить по материалам». It starts an ordinary 40.27 pipeline run and hands over to the
 * checkpoint screen, where the extracted structure is corrected before anything reaches the profile.
 *
 * The side effect is stated before the button rather than discovered afterwards: the same run also
 * produces a draft lesson. A customer who pasted a deck to fill in their profile and later finds an
 * unexplained lesson in «Контент» has been surprised by their own tool.
 */
export function MaterialRunDialog({
    open,
    isStarting,
    startError,
    onStart,
    onClose,
}: MaterialRunDialogProps) {
    const [title, setTitle] = useState("");
    const [material, setMaterial] = useState("");

    const titleFailure = title.trim().length === 0 ? "Впишите название прогона." : null;
    const isReadyToStart =
        titleFailure === null && material.trim().length >= MINIMUM_MATERIAL_LENGTH;

    return (
        <Modal
            open={open}
            onClose={onClose}
            title="Заполнить по материалам"
            size="lg"
            footer={
                <div className="flex flex-wrap justify-end gap-2">
                    <Button variant="secondary" onClick={onClose} disabled={isStarting}>
                        Отмена
                    </Button>
                    <Button
                        variant="primary"
                        loading={isStarting}
                        disabled={isStarting || !isReadyToStart}
                        onClick={() => onStart(title.trim(), material.trim())}
                    >
                        Разобрать материалы
                    </Button>
                </div>
            }
        >
            <div className="space-y-4">
                <p className="text-sm text-ink-3">
                    Вставьте презентацию продукта, скрипт звонка или заметки. ИИ разберёт их на
                    структуру, вы проверите её на следующем экране — и оттуда перенесёте в профиль.
                </p>

                <TextInput
                    label="Название прогона"
                    required
                    placeholder="Например: презентация продукта, август"
                    value={title}
                    error={titleFailure ?? undefined}
                    disabled={isStarting}
                    onChange={(event) => setTitle(event.target.value)}
                />

                <Textarea
                    label="Материалы"
                    hint={
                        material.trim().length < MINIMUM_MATERIAL_LENGTH
                            ? `Нужно хотя бы ${MINIMUM_MATERIAL_LENGTH} символов — сейчас ${material.trim().length}.`
                            : undefined
                    }
                    rows={10}
                    value={material}
                    disabled={isStarting}
                    onChange={(event) => setMaterial(event.target.value)}
                />

                <p className="text-xs text-ink-3">
                    Из тех же материалов получится и черновик урока — он появится в разделе
                    «Контент» скрытым от команды, пока вы его не проверите.
                </p>

                {startError && (
                    <p className="text-xs text-bad" role="alert">
                        {startError}
                    </p>
                )}
            </div>
        </Modal>
    );
}
