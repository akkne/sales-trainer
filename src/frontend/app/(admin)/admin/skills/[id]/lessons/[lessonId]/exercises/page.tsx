"use client";

import { useEffect, useRef, useState } from "react";
import { use } from "react";
import Link from "next/link";
import {
    useAdminExercises,
    useCreateExercise,
    useDeleteExercise,
    AdminExercise,
} from "@/lib/hooks/useAdmin";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

const EXERCISE_TYPES = ["multiple_choice", "fill_blank", "open_question"] as const;
type ExerciseType = (typeof EXERCISE_TYPES)[number];

// --- Type-specific content interfaces ---

interface MultipleChoiceContent {
    situation: string;
    question: string;
    options: string[];
    correctOptionIndex: number;
    explanation: string;
}

interface FillBlankContent {
    characterName: string;
    characterLine: string;
    options: string[];
    correctOptionIndex: number;
    explanation: string;
}

interface OpenQuestionContent {
    question: string;
    aiPrompt: string;
}

function emptyMultipleChoice(): MultipleChoiceContent {
    return { situation: "", question: "", options: ["", "", "", ""], correctOptionIndex: 0, explanation: "" };
}

function emptyFillBlank(): FillBlankContent {
    return { characterName: "", characterLine: "___", options: ["", "", "", ""], correctOptionIndex: 0, explanation: "" };
}

function emptyOpenQuestion(): OpenQuestionContent {
    return { question: "", aiPrompt: "" };
}

// --- Type badge helpers ---

function typeLabel(type: string): string {
    const labels: Record<string, string> = {
        multiple_choice: "Multiple Choice",
        fill_blank: "Fill Blank",
        open_question: "Open Question",
    };
    return labels[type] ?? type;
}

function typeBadgeColor(type: string): string {
    const colors: Record<string, string> = {
        multiple_choice: "bg-blue-100 text-blue-700",
        fill_blank: "bg-green-100 text-green-700",
        open_question: "bg-purple-100 text-purple-700",
    };
    return colors[type] ?? "bg-gray-100 text-gray-600";
}

// --- Sortable exercise list drag helpers ---

function moveExercise(exercises: ExerciseRow[], from: number, to: number): ExerciseRow[] {
    const result = [...exercises];
    const [moved] = result.splice(from, 1);
    result.splice(to, 0, moved);
    result.forEach((ex, i) => { ex.sortOrder = i + 1; });
    return result;
}

interface ExerciseRow {
    id: string | null; // null = new, not yet saved
    type: ExerciseType;
    sortOrder: number;
    content: MultipleChoiceContent | FillBlankContent | OpenQuestionContent;
}

function cloneExercise(ex: ExerciseRow): ExerciseRow {
    return { ...ex, content: JSON.parse(JSON.stringify(ex.content)) };
}

// --- Content editors per type ---

function MultipleChoiceEditor({ content, onChange }: {
    content: MultipleChoiceContent;
    onChange: (c: MultipleChoiceContent) => void;
}) {
    const inputCls = "mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400";
    const labelCls = "text-xs text-gray-500";

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Situation (context)</span>
                <input className={inputCls} value={content.situation}
                    onChange={(e) => onChange({ ...content, situation: e.target.value })} />
            </label>
            <label className="block">
                <span className={labelCls}>Question</span>
                <input className={inputCls} value={content.question}
                    onChange={(e) => onChange({ ...content, question: e.target.value })} />
            </label>
            <div>
                <span className={labelCls}>Options</span>
                {content.options.map((opt, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            type="radio"
                            checked={content.correctOptionIndex === i}
                            onChange={() => onChange({ ...content, correctOptionIndex: i })}
                            className="shrink-0"
                        />
                        <input className={inputCls} value={opt}
                            onChange={(e) => {
                                const opts = [...content.options];
                                opts[i] = e.target.value;
                                onChange({ ...content, options: opts });
                            }}
                            placeholder={`Option ${i + 1}`}
                        />
                    </div>
                ))}
                <span className="text-[10px] text-gray-400 mt-1 block">
                    Radio button marks the correct answer
                </span>
            </div>
            <label className="block">
                <span className={labelCls}>Explanation (shown after answer)</span>
                <input className={inputCls} value={content.explanation}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })} />
            </label>
        </div>
    );
}

function FillBlankEditor({ content, onChange }: {
    content: FillBlankContent;
    onChange: (c: FillBlankContent) => void;
}) {
    const inputCls = "mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400";
    const labelCls = "text-xs text-gray-500";

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Character Name</span>
                <input className={inputCls} value={content.characterName}
                    onChange={(e) => onChange({ ...content, characterName: e.target.value })} />
            </label>
            <label className="block">
                <span className={labelCls}>Line with blank (use ___ for the gap)</span>
                <textarea rows={2} className={inputCls} value={content.characterLine}
                    onChange={(e) => onChange({ ...content, characterLine: e.target.value })} />
            </label>
            <div>
                <span className={labelCls}>Options</span>
                {content.options.map((opt, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            type="radio"
                            checked={content.correctOptionIndex === i}
                            onChange={() => onChange({ ...content, correctOptionIndex: i })}
                            className="shrink-0"
                        />
                        <input className={inputCls} value={opt}
                            onChange={(e) => {
                                const opts = [...content.options];
                                opts[i] = e.target.value;
                                onChange({ ...content, options: opts });
                            }}
                            placeholder={`Option ${i + 1}`}
                        />
                    </div>
                ))}
                <span className="text-[10px] text-gray-400 mt-1 block">
                    Radio button marks the correct answer
                </span>
            </div>
            <label className="block">
                <span className={labelCls}>Explanation</span>
                <input className={inputCls} value={content.explanation}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })} />
            </label>
        </div>
    );
}

function OpenQuestionEditor({ content, onChange }: {
    content: OpenQuestionContent;
    onChange: (c: OpenQuestionContent) => void;
}) {
    const inputCls = "mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400";
    const labelCls = "text-xs text-gray-500";

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Question text</span>
                <input className={inputCls} value={content.question}
                    onChange={(e) => onChange({ ...content, question: e.target.value })} />
            </label>
            <label className="block">
                <span className={labelCls}>AI evaluation prompt (criteria)</span>
                <textarea rows={6} className={inputCls} value={content.aiPrompt}
                    onChange={(e) => onChange({ ...content, aiPrompt: e.target.value })}
                    placeholder="Rate the answer based on whether the user mentions..." />
            </label>
        </div>
    );
}

// --- Main page ---

export default function AdminLessonExercisesPage({
    params,
}: {
    params: Promise<{ id: string; lessonId: string }>;
}) {
    const { id: skillId, lessonId } = use(params);
    const { data: exercises = [], isLoading } = useAdminExercises(lessonId);

    const createMut = useCreateExercise(lessonId);
    const deleteMut = useDeleteExercise(lessonId);
    const qc = useQueryClient();
    const updateExerciseMut = useMutation({
        mutationFn: ({ exerciseId, body }: { exerciseId: string; body: Omit<AdminExercise, "id" | "lessonId"> }) =>
            apiClient.put<AdminExercise>(`/admin/exercises/${exerciseId}`, body),
        onSuccess: () => {
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
    });

    const [rows, setRows] = useState<ExerciseRow[]>([]);
    // Initialize rows once when query resolves
    const initRef = useRef(false);
    useEffect(() => {
        if (initRef.current) return;
        initRef.current = true;
        const mapped: ExerciseRow[] = exercises.map((ex) => ({
            id: ex.id,
            type: ex.type as ExerciseType,
            sortOrder: ex.sortOrder,
            content: ex.content as ExerciseRow["content"],
        }));
        setRows(mapped);
    }, [exercises]);

    const [editingId, setEditingId] = useState<string | null>(null);

    const inputCls = "mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400";
    const labelCls = "text-xs text-gray-500";
    const cardCls = "bg-white border border-gray-200 rounded-lg p-4";

    async function saveExercise(row: ExerciseRow) {
        if (row.id) {
            await updateExerciseMut.mutateAsync({
                exerciseId: row.id,
                body: { type: row.type, sortOrder: row.sortOrder, content: row.content as Record<string, unknown> },
            });
        } else {
            await createMut.mutateAsync({
                type: row.type,
                sortOrder: row.sortOrder,
                content: row.content as Record<string, unknown>,
            });
        }
        setEditingId(null);
    }

    function addExercise() {
        const newRow: ExerciseRow = {
            id: null,
            type: "multiple_choice",
            sortOrder: rows.length + 1,
            content: emptyMultipleChoice(),
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

    function contentEditor(type: ExerciseType, content: ExerciseRow["content"], onChange: (c: ExerciseRow["content"]) => void) {
        if (type === "multiple_choice") {
            return (
                <MultipleChoiceEditor
                    content={content as MultipleChoiceContent}
                    onChange={(c) => onChange(c as ExerciseRow["content"])}
                />
            );
        }
        if (type === "fill_blank") {
            return (
                <FillBlankEditor
                    content={content as FillBlankContent}
                    onChange={(c) => onChange(c as ExerciseRow["content"])}
                />
            );
        }
        return (
            <OpenQuestionEditor
                content={content as OpenQuestionContent}
                onChange={(c) => onChange(c as ExerciseRow["content"])}
            />
        );
    }

    const isLoadingMut = createMut.isPending || updateExerciseMut.isPending;

    return (
        <div>
            <div className="mb-6">
                <Link
                    href={`/admin/skills/${skillId}/lessons/${lessonId}`}
                    className="text-xs text-gray-400 hover:text-gray-600 transition-colors"
                >
                    ← Back to lesson
                </Link>
            </div>

            <div className="flex items-center justify-between mb-4">
                <h1 className="text-lg font-semibold text-gray-900">Edit Exercises</h1>
                <button
                    onClick={addExercise}
                    className="px-3 py-1.5 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 transition-colors"
                >
                    + Add exercise
                </button>
            </div>

            {rows.length === 0 && !isLoading && (
                <p className="text-sm text-gray-400">No exercises yet. Click "+ Add exercise" to create one.</p>
            )}

            {isLoading && <p className="text-sm text-gray-400">Loading...</p>}

            <div className="space-y-4">
                {rows.map((row, index) => {
                    const isEditing = editingId === row.id || (editingId === "__new__" && row.id === null);

                    return (
                        <div key={row.id ?? "__new__"} className={cardCls}>
                            {/* Drag handles and header */}
                            <div className="flex items-center justify-between mb-3">
                                <div className="flex items-center gap-2">
                                    <div className="flex flex-col gap-0.5">
                                        <button
                                            disabled={index === 0}
                                            onClick={() => setRows(moveExercise(rows, index, index - 1))}
                                            className="text-gray-400 hover:text-gray-600 disabled:opacity-30 text-xs leading-none"
                                            title="Move up"
                                        >
                                            ▲
                                        </button>
                                        <button
                                            disabled={index === rows.length - 1}
                                            onClick={() => setRows(moveExercise(rows, index, index + 1))}
                                            className="text-gray-400 hover:text-gray-600 disabled:opacity-30 text-xs leading-none"
                                            title="Move down"
                                        >
                                            ▼
                                        </button>
                                    </div>
                                    <span className={`text-xs px-2 py-0.5 rounded font-mono ${typeBadgeColor(row.type)}`}>
                                        {typeLabel(row.type)}
                                    </span>
                                    <span className="text-xs text-gray-400">order: {row.sortOrder}</span>
                                </div>
                                {!isEditing && (
                                    <div className="flex gap-3">
                                        <button
                                            onClick={() => setEditingId(row.id ?? "__new__")}
                                            className="text-xs text-gray-500 hover:text-gray-800 transition-colors"
                                        >
                                            Edit
                                        </button>
                                        {row.id && (
                                            <button
                                                onClick={() => deleteRow(row.id)}
                                                className="text-xs text-red-400 hover:text-red-600 transition-colors"
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
                                                    if (newType === "multiple_choice") {
                                                        return { ...r, type: newType as "multiple_choice", content: emptyMultipleChoice() };
                                                    }
                                                    if (newType === "fill_blank") {
                                                        return { ...r, type: newType as "fill_blank", content: emptyFillBlank() };
                                                    }
                                                    return { ...r, type: newType as "open_question", content: emptyOpenQuestion() };
                                                }));
                                            }}
                                        >
                                            {EXERCISE_TYPES.map((t) => (
                                                <option key={t} value={t}>{typeLabel(t)}</option>
                                            ))}
                                        </select>
                                    </label>

                                    {contentEditor(row.type, row.content, (newContent) => {
                                        setRows(rows.map((r, ri) => ri === index ? { ...r, content: newContent } : r));
                                    })}

                                    <div className="flex gap-3 mt-4">
                                        <button
                                            onClick={async () => await saveExercise(row)}
                                            disabled={isLoadingMut}
                                            className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                                        >
                                            {isLoadingMut ? "Saving..." : (row.id ? "Save changes" : "Create")}
                                        </button>
                                        {row.id ? (
                                            <button
                                                onClick={() => {
                                                    // Reset to original
                                                    const orig = exercises.find((ex) => ex.id === row.id);
                                                    if (orig) {
                                                        setRows(rows.map((r, ri) => ri === index
                                                            ? { id: orig.id, type: orig.type as ExerciseType, sortOrder: orig.sortOrder, content: orig.content as ExerciseRow["content"] }
                                                            : r
                                                        ));
                                                    }
                                                    setEditingId(null);
                                                }}
                                                className="px-4 py-2 text-sm text-gray-500 hover:text-gray-700 transition-colors"
                                            >
                                                Cancel
                                            </button>
                                        ) : (
                                            <button
                                                onClick={() => setRows(rows.filter((r) => r.id !== null))}
                                                className="px-4 py-2 text-sm text-gray-500 hover:text-gray-700 transition-colors"
                                            >
                                                Remove
                                            </button>
                                        )}
                                    </div>
                                </div>
                            ) : (
                                <p className="text-xs text-gray-500 font-mono">
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

function renderContentPreview(row: ExerciseRow) {
    if (row.type === "multiple_choice") {
        const c = row.content as MultipleChoiceContent;
        return c.question || "(no question)";
    }
    if (row.type === "fill_blank") {
        const c = row.content as FillBlankContent;
        return `${c.characterName}: ${c.characterLine}`;
    }
    const c = row.content as OpenQuestionContent;
    return c.question || "(no question)";
}
