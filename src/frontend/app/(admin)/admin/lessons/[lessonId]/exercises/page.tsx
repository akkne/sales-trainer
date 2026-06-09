"use client";

import { useState, useRef } from "react";
import { use } from "react";
import Link from "next/link";
import {
    useAdminExercises,
    useCreateExercise,
    useDeleteExercise,
    useImportExercises,
    AdminExercise,
    type ExercisesImportResult,
} from "@/features/admin/hooks/use-admin";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

import {
    EXERCISE_TYPES,
    TYPE_LABELS,
    ExerciseType,
    ExerciseContent,
    ChooseOptionContent,
    FillBlankContent,
    ReorderContent,
    MatchPairsContent,
    CategorizeContent,
    SpotMistakeContent,
    RewriteContent,
    AiDialogueContent,
    EvaluateCallContent,
    FreeTextContent,
    emptyChooseOption,
    emptyFillBlank,
    emptyReorder,
    emptyMatchPairs,
    emptyCategorize,
    emptySpotMistake,
    emptyRewrite,
    emptyAiDialogue,
    emptyEvaluateCall,
    emptyFreeText,
    inputCls,
    labelCls,
} from "@/features/admin/components/exercise-editors";

import {
    MultipleChoiceEditor,
    FillBlankEditor,
    OpenQuestionEditor,
    OrderingEditor,
    MatchingEditor,
    CategorizingEditor,
    FindErrorEditor,
    RewriteBetterEditor,
    AiDialogEditor,
    RateCallEditor,
    WrittenAnswerEditor,
} from "@/features/admin/components/exercise-editors";

function typeBadgeColor(): string {
    return "bg-bg-2 text-ink-3 border border-line";
}

function moveExercise(exercises: ExerciseRow[], from: number, to: number): ExerciseRow[] {
    const result = [...exercises];
    const [moved] = result.splice(from, 1);
    result.splice(to, 0, moved);
    result.forEach((ex, i) => { ex.sortOrder = i + 1; });
    return result;
}

interface ExerciseRow {
    id: string | null;
    type: ExerciseType;
    sortOrder: number;
    content: ExerciseContent;
    customAiPrompt: string | null;
}

function getEmptyContent(type: ExerciseType): ExerciseContent {
    switch (type) {
        case "choose_option": return emptyChooseOption();
        case "fill_blank": return emptyFillBlank();
        case "reorder": return emptyReorder();
        case "match_pairs": return emptyMatchPairs();
        case "categorize": return emptyCategorize();
        case "spot_mistake": return emptySpotMistake();
        case "rewrite": return emptyRewrite();
        case "ai_dialogue": return emptyAiDialogue();
        case "evaluate_call": return emptyEvaluateCall();
        case "free_text": return emptyFreeText();
    }
}

function contentEditor(
    type: ExerciseType,
    content: ExerciseContent,
    onChange: (c: ExerciseContent) => void
) {
    switch (type) {
        case "choose_option":
            return <MultipleChoiceEditor content={content as ChooseOptionContent} onChange={onChange} />;
        case "fill_blank":
            return <FillBlankEditor content={content as FillBlankContent} onChange={onChange} />;
        case "free_text":
            return <OpenQuestionEditor content={content as FreeTextContent} onChange={onChange} />;
        case "reorder":
            return <OrderingEditor content={content as ReorderContent} onChange={onChange} />;
        case "match_pairs":
            return <MatchingEditor content={content as MatchPairsContent} onChange={onChange} />;
        case "categorize":
            return <CategorizingEditor content={content as CategorizeContent} onChange={onChange} />;
        case "spot_mistake":
            return <FindErrorEditor content={content as SpotMistakeContent} onChange={onChange} />;
        case "rewrite":
            return <RewriteBetterEditor content={content as RewriteContent} onChange={onChange} />;
        case "ai_dialogue":
            return <AiDialogEditor content={content as AiDialogueContent} onChange={onChange} />;
        case "evaluate_call":
            return <RateCallEditor content={content as EvaluateCallContent} onChange={onChange} />;
    }
}

function renderContentPreview(row: ExerciseRow): string {
    const c = row.content;
    switch (row.type) {
        case "choose_option":
            return (c as ChooseOptionContent).question || "(no question)";
        case "fill_blank":
            return `${(c as FillBlankContent).characterName}: ${(c as FillBlankContent).characterLine}`;
        case "free_text":
            return (c as FreeTextContent).prompt?.slice(0, 50) || "(no prompt)";
        case "reorder":
            return (c as ReorderContent).instruction || "(no instruction)";
        case "match_pairs":
            return (c as MatchPairsContent).instruction || "(no instruction)";
        case "categorize":
            return (c as CategorizeContent).instruction || "(no instruction)";
        case "spot_mistake":
            return (c as SpotMistakeContent).instruction || "(no instruction)";
        case "rewrite":
            return (c as RewriteContent).originalText?.slice(0, 50) || "(no text)";
        case "ai_dialogue":
            return (c as AiDialogueContent).scenario?.slice(0, 50) || "(no scenario)";
        case "evaluate_call":
            return `${(c as EvaluateCallContent).transcript?.length || 0} lines, ${(c as EvaluateCallContent).criteria?.length || 0} criteria`;
        default:
            return "(preview)";
    }
}

export default function AdminLessonExercisesPage({
    params,
}: {
    params: Promise<{ lessonId: string }>;
}) {
    const { lessonId } = use(params);
    const { data: exercises = [], isLoading } = useAdminExercises(lessonId);

    const createMut = useCreateExercise(lessonId);
    const deleteMut = useDeleteExercise(lessonId);
    const importMut = useImportExercises(lessonId);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [importResult, setImportResult] = useState<ExercisesImportResult | null>(null);
    const qc = useQueryClient();
    const updateExerciseMut = useMutation({
        mutationFn: ({ exerciseId, body }: { exerciseId: string; body: Omit<AdminExercise, "id" | "lessonId"> }) =>
            apiClient.put<AdminExercise>(`/admin/exercises/${exerciseId}`, body),
        onSuccess: () => {
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
    });

    const [localRows, setLocalRows] = useState<ExerciseRow[] | null>(null);

    const rows: ExerciseRow[] = localRows ?? exercises.map((ex) => ({
        id: ex.id,
        type: ex.type as ExerciseType,
        sortOrder: ex.orderInLesson,
        content: ex.content as unknown as ExerciseContent,
        customAiPrompt: ex.customAiPrompt ?? null,
    }));

    function setRows(newRows: ExerciseRow[] | ((prev: ExerciseRow[]) => ExerciseRow[])) {
        if (typeof newRows === "function") {
            setLocalRows((prev) => newRows(prev ?? rows));
        } else {
            setLocalRows(newRows);
        }
    }

    const [editingId, setEditingId] = useState<string | null>(null);
    const cardCls = "bg-surface border border-line rounded-2xl p-4";

    async function saveExercise(row: ExerciseRow) {
        if (row.id) {
            await updateExerciseMut.mutateAsync({
                exerciseId: row.id,
                body: {
                    type: row.type,
                    orderInLesson: row.sortOrder,
                    content: row.content as unknown as Record<string, unknown>,
                    customAiPrompt: row.customAiPrompt,
                },
            });
        } else {
            await createMut.mutateAsync({
                type: row.type,
                orderInLesson: row.sortOrder,
                content: row.content as unknown as Record<string, unknown>,
                customAiPrompt: row.customAiPrompt,
            });
        }
        setEditingId(null);
    }

    function addExercise() {
        const newRow: ExerciseRow = {
            id: null,
            type: "choose_option",
            sortOrder: rows.length + 1,
            content: emptyChooseOption(),
            customAiPrompt: null,
        };
        setRows([...rows, newRow]);
        setEditingId("__new__");
    }

    function deleteRow(id: string | null) {
        if (!id) return;
        if (!confirm("Delete this exercise?")) return;
        setRows(rows.filter((r) => r.id !== id));
        if (editingId === "__new__" && !id) setEditingId(null);
        deleteMut.mutate(id);
        if (editingId === id) setEditingId(null);
    }

    function exportExercises() {
        // Dump all saved exercises as an import-ready JSON array (not a single object).
        const payload = [...exercises]
            .sort((a, b) => a.orderInLesson - b.orderInLesson)
            .map((ex) => ({
                type: ex.type,
                orderInLesson: ex.orderInLesson,
                content: ex.content,
                customAiPrompt: ex.customAiPrompt ?? null,
            }));
        const blob = new Blob([JSON.stringify(payload, null, 2)], { type: "application/json" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = `exercises_${lessonId}.json`;
        anchor.click();
        URL.revokeObjectURL(url);
    }

    async function handleImport(e: React.ChangeEvent<HTMLInputElement>) {
        const file = e.target.files?.[0];
        if (!file) return;
        setImportResult(null);
        try {
            const parsed = JSON.parse(await file.text());
            if (!Array.isArray(parsed)) {
                alert("JSON must be an array of exercise objects.");
                return;
            }
            const result = await importMut.mutateAsync(parsed);
            setImportResult(result);
            setLocalRows(null);
        } catch (err) {
            alert(`Import failed: ${(err as Error).message}`);
        }
        if (fileInputRef.current) fileInputRef.current.value = "";
    }

    const isLoadingMut = createMut.isPending || updateExerciseMut.isPending;

    // AI-powered types that show custom prompt field
    const aiPoweredTypes: ExerciseType[] = ["spot_mistake", "rewrite", "ai_dialogue", "evaluate_call", "free_text"];

    return (
        <div>
            <div className="mb-6">
                <Link
                    href="/admin/lessons"
                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    ← Back to lessons
                </Link>
            </div>

            <div className="flex items-center justify-between mb-4">
                <h1 className="text-lg font-semibold text-ink">Edit Exercises</h1>
                <div className="flex items-center gap-2">
                    <button
                        onClick={exportExercises}
                        disabled={exercises.length === 0}
                        className="px-3 py-1.5 text-sm border border-line text-ink-3 rounded-md hover:bg-bg-2 disabled:opacity-40 transition-colors"
                    >
                        Export JSON
                    </button>
                    <button
                        onClick={() => fileInputRef.current?.click()}
                        disabled={importMut.isPending}
                        className="px-3 py-1.5 text-sm border border-line text-ink-3 rounded-md hover:bg-bg-2 disabled:opacity-40 transition-colors"
                    >
                        {importMut.isPending ? "Importing…" : "Import JSON"}
                    </button>
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept=".json"
                        onChange={handleImport}
                        className="hidden"
                    />
                    <button
                        onClick={addExercise}
                        className="px-3 py-1.5 text-sm bg-ink text-bg rounded-md hover:opacity-90 transition-colors"
                    >
                        + Add exercise
                    </button>
                </div>
            </div>

            {importResult && (
                <div className="mb-4 p-3 bg-bg-2 rounded-md">
                    <p className="text-xs text-ink">
                        Imported — Created: <span className="font-medium">{importResult.exercisesCreated}</span> | Updated: <span className="font-medium">{importResult.exercisesUpdated}</span>
                    </p>
                    {importResult.errors.length > 0 && (
                        <ul className="mt-1 text-xs text-bad font-mono max-h-32 overflow-y-auto">
                            {importResult.errors.map((err, i) => <li key={i}>{err}</li>)}
                        </ul>
                    )}
                </div>
            )}

            {rows.length === 0 && !isLoading && (
                <p className="text-sm text-ink-3">No exercises yet. Click &quot;+ Add exercise&quot; to create one.</p>
            )}

            {isLoading && <p className="text-sm text-ink-3">Loading...</p>}

            <div className="space-y-4">
                {rows.map((row, index) => {
                    const isEditing = editingId === row.id || (editingId === "__new__" && row.id === null);

                    return (
                        <div key={row.id ?? "__new__"} className={cardCls}>
                            <div className="flex items-center justify-between mb-3">
                                <div className="flex items-center gap-2">
                                    <div className="flex flex-col gap-0.5">
                                        <button
                                            disabled={index === 0}
                                            onClick={() => setRows(moveExercise(rows, index, index - 1))}
                                            className="text-ink-3 hover:text-ink disabled:opacity-30 text-xs leading-none"
                                            title="Move up"
                                        >
                                            ▲
                                        </button>
                                        <button
                                            disabled={index === rows.length - 1}
                                            onClick={() => setRows(moveExercise(rows, index, index + 1))}
                                            className="text-ink-3 hover:text-ink disabled:opacity-30 text-xs leading-none"
                                            title="Move down"
                                        >
                                            ▼
                                        </button>
                                    </div>
                                    <span className={`text-xs px-2 py-0.5 rounded font-mono ${typeBadgeColor()}`}>
                                        {TYPE_LABELS[row.type] ?? row.type}
                                    </span>
                                    <span className="text-xs text-ink-3">#{row.sortOrder}</span>
                                    {row.customAiPrompt && (
                                        <span className="text-xs px-1.5 py-0.5 bg-indigo-soft text-indigo rounded">
                                            AI
                                        </span>
                                    )}
                                </div>
                                {!isEditing && (
                                    <div className="flex gap-3">
                                        <button
                                            onClick={() => setEditingId(row.id ?? "__new__")}
                                            className="text-xs text-ink-3 hover:text-ink transition-colors"
                                        >
                                            Edit
                                        </button>
                                        {row.id && (
                                            <button
                                                onClick={() => deleteRow(row.id)}
                                                className="text-xs text-bad hover:text-bad transition-colors"
                                            >
                                                Delete
                                            </button>
                                        )}
                                    </div>
                                )}
                            </div>

                            {isEditing ? (
                                <div>
                                    <label className="block mb-3">
                                        <span className={labelCls}>Type</span>
                                        <select
                                            className={inputCls}
                                            value={row.type}
                                            onChange={(e) => {
                                                const newType = e.target.value as ExerciseType;
                                                setRows(rows.map((r, ri) => {
                                                    if (ri !== index) return r;
                                                    if (newType === r.type) return r;
                                                    return { ...r, type: newType, content: getEmptyContent(newType) };
                                                }));
                                            }}
                                        >
                                            {EXERCISE_TYPES.map((t) => (
                                                <option key={t} value={t}>{TYPE_LABELS[t]}</option>
                                            ))}
                                        </select>
                                    </label>

                                    {contentEditor(row.type, row.content, (newContent) => {
                                        setRows(rows.map((r, ri) => ri === index ? { ...r, content: newContent } : r));
                                    })}

                                    {/* Custom AI Prompt */}
                                    {aiPoweredTypes.includes(row.type) && (
                                        <label className="block mt-4">
                                            <span className={labelCls}>Custom AI Prompt (optional)</span>
                                            <textarea
                                                className={`${inputCls} font-mono`}
                                                rows={3}
                                                value={row.customAiPrompt ?? ""}
                                                onChange={(e) => {
                                                    setRows(rows.map((r, ri) => ri === index
                                                        ? { ...r, customAiPrompt: e.target.value || null }
                                                        : r
                                                    ));
                                                }}
                                                placeholder="Additional evaluation criteria specific to this exercise..."
                                            />
                                            <span className="text-[10px] text-ink-3 mt-1 block">
                                                This prompt is appended to the global type prompt for AI evaluation
                                            </span>
                                        </label>
                                    )}

                                    <div className="flex gap-3 mt-4">
                                        <button
                                            onClick={async () => await saveExercise(row)}
                                            disabled={isLoadingMut}
                                            className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                                        >
                                            {isLoadingMut ? "Saving..." : (row.id ? "Save changes" : "Create")}
                                        </button>
                                        {row.id ? (
                                            <button
                                                onClick={() => {
                                                    const orig = exercises.find((ex) => ex.id === row.id);
                                                    if (orig) {
                                                        setRows(rows.map((r, ri) => ri === index
                                                            ? {
                                                                id: orig.id,
                                                                type: orig.type as ExerciseType,
                                                                sortOrder: orig.orderInLesson,
                                                                content: orig.content as unknown as ExerciseContent,
                                                                customAiPrompt: orig.customAiPrompt ?? null,
                                                            }
                                                            : r
                                                        ));
                                                    }
                                                    setEditingId(null);
                                                }}
                                                className="px-4 py-2 text-sm text-ink-3 hover:text-ink transition-colors"
                                            >
                                                Cancel
                                            </button>
                                        ) : (
                                            <button
                                                onClick={() => setRows(rows.filter((r) => r.id !== null))}
                                                className="px-4 py-2 text-sm text-ink-3 hover:text-ink transition-colors"
                                            >
                                                Remove
                                            </button>
                                        )}
                                    </div>
                                </div>
                            ) : (
                                <p className="text-xs text-ink-3 font-mono">
                                    {renderContentPreview(row)}
                                </p>
                            )}
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
