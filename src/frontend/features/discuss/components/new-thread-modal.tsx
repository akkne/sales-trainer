"use client";

import { useEffect, useState } from "react";
import { Button } from "@/shared/components/button";
import { Icon } from "@/shared/components/icon";
import { TextInput, Textarea } from "@/shared/components/input";
import { PhotoPicker } from "./photo-picker";
import { useCreateThread, useDiscussTags, useUploadThreadPhotos } from "../hooks/use-discuss";

export function NewThreadModal({ onClose }: { onClose: (createdId?: string) => void }) {
    const { data: curatedTags } = useDiscussTags(true);
    const createThread = useCreateThread();

    const [title, setTitle] = useState("");
    const [body, setBody] = useState("");
    const [selectedTags, setSelectedTags] = useState<string[]>([]);
    const [customTag, setCustomTag] = useState("");
    const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
    const [createdThreadId, setCreatedThreadId] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);

    const uploadThreadPhotos = useUploadThreadPhotos(createdThreadId ?? "");

    useEffect(() => {
        if (!createdThreadId) return;
        if (selectedFiles.length === 0) {
            onClose(createdThreadId);
            return;
        }
        uploadThreadPhotos.mutate(selectedFiles, {
            onSuccess: () => onClose(createdThreadId),
            onError: () => setError("Тема создана, но не удалось загрузить фото. Закрой это окно, чтобы открыть тему."),
        });
        // eslint-disable-next-line react-hooks/exhaustive-deps -- run once when the thread id is set
    }, [createdThreadId]);

    const toggleTag = (name: string) => {
        setSelectedTags((current) =>
            current.includes(name) ? current.filter((tag) => tag !== name) : [...current, name]
        );
    };

    const addCustomTag = () => {
        const trimmed = customTag.trim();
        if (trimmed && !selectedTags.includes(trimmed)) {
            setSelectedTags((current) => [...current, trimmed]);
        }
        setCustomTag("");
    };

    const submit = async () => {
        if (!title.trim() || !body.trim()) {
            setError("Заполни заголовок и текст");
            return;
        }
        setError(null);
        try {
            const created = await createThread.mutateAsync({ title, body, tags: selectedTags });
            setCreatedThreadId(created.id);
        } catch (submitError) {
            setError(submitError instanceof Error ? submitError.message : "Не удалось создать тему");
        }
    };

    const isBusy = createThread.isPending || uploadThreadPhotos.isPending;

    const closeModal = () => onClose(createdThreadId ?? undefined);

    return (
        <div className="dsc-modal-backdrop" onClick={closeModal}>
            <div className="card card-pad dsc-modal" onClick={(event) => event.stopPropagation()}>
                <div className="row between" style={{ marginBottom: 16 }}>
                    <h2 className="h3">Новая тема</h2>
                    <button className="icon-btn" onClick={closeModal} aria-label="Закрыть">
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="col" style={{ gap: 14 }}>
                    <TextInput
                        label="Заголовок"
                        placeholder="Например: как обойти секретаря?"
                        value={title}
                        maxLength={300}
                        onChange={(event) => setTitle(event.target.value)}
                    />
                    <Textarea
                        label="Текст"
                        placeholder="Опиши свой вопрос или поделись скриптом…"
                        value={body}
                        rows={6}
                        onChange={(event) => setBody(event.target.value)}
                    />

                    <div className="col" style={{ gap: 8 }}>
                        <label className="text-sm font-medium text-ink">Теги</label>
                        <div className="row gap-2 wrap">
                            {(curatedTags ?? []).map((tag) => (
                                <button
                                    key={tag.id}
                                    type="button"
                                    className={`chip${selectedTags.includes(tag.name) ? " solid" : ""}`}
                                    onClick={() => toggleTag(tag.name)}
                                >
                                    {tag.name}
                                </button>
                            ))}
                            {selectedTags
                                .filter((name) => !(curatedTags ?? []).some((tag) => tag.name === name))
                                .map((name) => (
                                    <button
                                        key={name}
                                        type="button"
                                        className="chip solid"
                                        onClick={() => toggleTag(name)}
                                    >
                                        {name} <Icon name="close" size={12} />
                                    </button>
                                ))}
                        </div>
                        <div className="row gap-2" style={{ marginTop: 4 }}>
                            <TextInput
                                placeholder="Свой тег…"
                                value={customTag}
                                maxLength={60}
                                onChange={(event) => setCustomTag(event.target.value)}
                                onKeyDown={(event) => {
                                    if (event.key === "Enter") {
                                        event.preventDefault();
                                        addCustomTag();
                                    }
                                }}
                            />
                            <Button variant="outline" size="md" onClick={addCustomTag} iconLeft="plus">
                                Добавить
                            </Button>
                        </div>
                    </div>

                    <div className="col" style={{ gap: 8 }}>
                        <label className="text-sm font-medium text-ink">Фото</label>
                        <PhotoPicker files={selectedFiles} onChange={setSelectedFiles} disabled={isBusy} />
                    </div>

                    {error && <p className="text-xs text-bad">{error}</p>}

                    <div className="row gap-2" style={{ justifyContent: "flex-end", marginTop: 8 }}>
                        <Button variant="ghost" disabled={isBusy} onClick={closeModal}>Отмена</Button>
                        <Button variant="primary" loading={isBusy} onClick={submit} iconLeft="check">
                            Опубликовать
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
}
