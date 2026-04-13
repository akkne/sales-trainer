"use client";

import { useState } from "react";
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

import {
    EXERCISE_TYPES,
    TYPE_LABELS,
    ExerciseType,
    ExerciseContent,
    MultipleChoiceContent,
    FillBlankContent,
    OpenQuestionContent,
    OrderingContent,
    MatchingContent,
    CategorizingContent,
    FindErrorContent,
    RewriteBetterContent,
    AiDialogContent,
    RateCallContent,
    WrittenAnswerContent,
    emptyMultipleChoice,
    emptyFillBlank,
    emptyOpenQuestion,
    emptyOrdering,
    emptyMatching,
    emptyCategorizing,
    emptyFindError,
    emptyRewriteBetter,
    emptyAiDialog,
    emptyRateCall,
    emptyWrittenAnswer,
    inputCls,
    labelCls,
} from "@/components/admin/exercise-editors";

import { MultipleChoiceEditor } from "@/components/admin/exercise-editors/MultipleChoiceEditor";
import { FillBlankEditor } from "@/components/admin/exercise-editors/FillBlankEditor";
import { OpenQuestionEditor } from "@/components/admin/exercise-editors/OpenQuestionEditor";
import { OrderingEditor } from "@/components/admin/exercise-editors/OrderingEditor";
import { MatchingEditor } from "@/components/admin/exercise-editors/MatchingEditor";
import { CategorizingEditor } from "@/components/admin/exercise-editors/CategorizingEditor";
import { FindErrorEditor } from "@/components/admin/exercise-editors/FindErrorEditor";
import { RewriteBetterEditor } from "@/components/admin/exercise-editors/RewriteBetterEditor";
import { AiDialogEditor } from "@/components/admin/exercise-editors/AiDialogEditor";
import { RateCallEditor } from "@/components/admin/exercise-editors/RateCallEditor";
import { WrittenAnswerEditor } from "@/components/admin/exercise-editors/WrittenAnswerEditor";

// Monochrome badge styling
function typeBadgeColor(): string {
    return "bg-surface-container text-on-surface-variant border border-outline-variant";
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
}

function getEmptyContent(type: ExerciseType): ExerciseContent {
    switch (type) {
        case "multiple_choice": return emptyMultipleChoice();
        case "fill_blank": return emptyFillBlank();
        case "open_question": return emptyOpenQuestion();
        case "ordering": return emptyOrdering();
        case "matching": return emptyMatching();
        case "categorizing": return emptyCategorizing();
        case "find_error": return emptyFindError();
        case "rewrite_better": return emptyRewriteBetter();
        case "ai_dialog": return emptyAiDialog();
        case "rate_call": return emptyRateCall();
        case "written_answer": return emptyWrittenAnswer();
    }
}

function contentEditor(
    type: ExerciseType,
    content: ExerciseContent,
    onChange: (c: ExerciseContent) => void
) {
    switch (type) {
        case "multiple_choice":
            return <MultipleChoiceEditor content={content as MultipleChoiceContent} onChange={onChange} />;
        case "fill_blank":
            return <FillBlankEditor content={content as FillBlankContent} onChange={onChange} />;
        case "open_question":
            return <OpenQuestionEditor content={content as OpenQuestionContent} onChange={onChange} />;
        case "ordering":
            return <OrderingEditor content={content as OrderingContent} onChange={onChange} />;
        case "matching":
            return <MatchingEditor content={content as MatchingContent} onChange={onChange} />;
        case "categorizing":
            return <CategorizingEditor content={content as CategorizingContent} onChange={onChange} />;
        case "find_error":
            return <FindErrorEditor content={content as FindErrorContent} onChange={onChange} />;
        case "rewrite_better":
            return <RewriteBetterEditor content={content as RewriteBetterContent} onChange={onChange} />;
        case "ai_dialog":
            return <AiDialogEditor content={content as AiDialogContent} onChange={onChange} />;
        case "rate_call":
            return <RateCallEditor content={content as RateCallContent} onChange={onChange} />;
        case "written_answer":
            return <WrittenAnswerEditor content={content as WrittenAnswerContent} onChange={onChange} />;
    }
}

function renderContentPreview(row: ExerciseRow): string {
    const c = row.content;
    switch (row.type) {
        case "multiple_choice":
            return (c as MultipleChoiceContent).question || "(no question)";
        case "fill_blank":
            return `${(c as FillBlankContent).characterName}: ${(c as FillBlankContent).characterLine}`;
        case "open_question":
            return (c as OpenQuestionContent).question || "(no question)";
        case "ordering":
            return (c as OrderingContent).instruction || "(no instruction)";
        case "matching":
            return (c as MatchingContent).instruction || "(no instruction)";
        case "categorizing":
            return (c as CategorizingContent).instruction || "(no instruction)";
        case "find_error":
            return (c as FindErrorContent).instruction || "(no instruction)";
        case "rewrite_better":
            return (c as RewriteBetterContent).originalText?.slice(0, 50) || "(no text)";
        case "ai_dialog":
            return (c as AiDialogContent).scenario?.slice(0, 50) || "(no scenario)";
        case "rate_call":
            return `${(c as RateCallContent).transcript?.length || 0} lines, ${(c as RateCallContent).criteria?.length || 0} criteria`;
        case "written_answer":
            return (c as WrittenAnswerContent).prompt?.slice(0, 50) || "(no prompt)";
        default:
            return "(preview)";
    }
}

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

    // Local state for editing - initialized lazily from server data
    const [localRows, setLocalRows] = useState<ExerciseRow[] | null>(null);

    // Use local state if available, otherwise derive from server data
    const rows: ExerciseRow[] = localRows ?? exercises.map((ex) => ({
        id: ex.id,
        type: ex.type as ExerciseType,
        sortOrder: ex.sortOrder,
        content: ex.content as unknown as ExerciseContent,
    }));

    // Wrapper to set local state
    function setRows(newRows: ExerciseRow[] | ((prev: ExerciseRow[]) => ExerciseRow[])) {
        if (typeof newRows === "function") {
            setLocalRows((prev) => newRows(prev ?? rows));
        } else {
            setLocalRows(newRows);
        }
    }

    const [editingId, setEditingId] = useState<string | null>(null);
    const cardCls = "bg-surface-container-lowest border border-outline-variant rounded-2xl p-4";

    async function saveExercise(row: ExerciseRow) {
        if (row.id) {
            await updateExerciseMut.mutateAsync({
                exerciseId: row.id,
                body: { type: row.type, sortOrder: row.sortOrder, content: row.content as unknown as Record<string, unknown> },
            });
        } else {
            await createMut.mutateAsync({
                type: row.type,
                sortOrder: row.sortOrder,
                content: row.content as unknown as Record<string, unknown>,
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

    const isLoadingMut = createMut.isPending || updateExerciseMut.isPending;

    return (
        <div>
            <div className="mb-6">
                <Link
                    href={`/admin/skills/${skillId}/lessons/${lessonId}`}
                    className="text-xs text-on-surface-variant hover:text-on-surface transition-colors"
                >
                    ← Back to lesson
                </Link>
            </div>

            <div className="flex items-center justify-between mb-4">
                <h1 className="text-lg font-semibold text-on-surface">Edit Exercises</h1>
                <button
                    onClick={addExercise}
                    className="px-3 py-1.5 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim transition-colors"
                >
                    + Add exercise
                </button>
            </div>

            {rows.length === 0 && !isLoading && (
                <p className="text-sm text-on-surface-variant">No exercises yet. Click &quot;+ Add exercise&quot; to create one.</p>
            )}

            {isLoading && <p className="text-sm text-on-surface-variant">Loading...</p>}

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
                                            className="text-on-surface-variant hover:text-on-surface disabled:opacity-30 text-xs leading-none"
                                            title="Move up"
                                        >
                                            ▲
                                        </button>
                                        <button
                                            disabled={index === rows.length - 1}
                                            onClick={() => setRows(moveExercise(rows, index, index + 1))}
                                            className="text-on-surface-variant hover:text-on-surface disabled:opacity-30 text-xs leading-none"
                                            title="Move down"
                                        >
                                            ▼
                                        </button>
                                    </div>
                                    <span className={`text-xs px-2 py-0.5 rounded font-mono ${typeBadgeColor()}`}>
                                        {TYPE_LABELS[row.type]}
                                    </span>
                                    <span className="text-xs text-on-surface-variant">#{row.sortOrder}</span>
                                </div>
                                {!isEditing && (
                                    <div className="flex gap-3">
                                        <button
                                            onClick={() => setEditingId(row.id ?? "__new__")}
                                            className="text-xs text-on-surface-variant hover:text-on-surface transition-colors"
                                        >
                                            Edit
                                        </button>
                                        {row.id && (
                                            <button
                                                onClick={() => deleteRow(row.id)}
                                                className="text-xs text-error hover:text-error transition-colors"
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

                                    <div className="flex gap-3 mt-4">
                                        <button
                                            onClick={async () => await saveExercise(row)}
                                            disabled={isLoadingMut}
                                            className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                                        >
                                            {isLoadingMut ? "Saving..." : (row.id ? "Save changes" : "Create")}
                                        </button>
                                        {row.id ? (
                                            <button
                                                onClick={() => {
                                                    const orig = exercises.find((ex) => ex.id === row.id);
                                                    if (orig) {
                                                        setRows(rows.map((r, ri) => ri === index
                                                            ? { id: orig.id, type: orig.type as ExerciseType, sortOrder: orig.sortOrder, content: orig.content as unknown as ExerciseContent }
                                                            : r
                                                        ));
                                                    }
                                                    setEditingId(null);
                                                }}
                                                className="px-4 py-2 text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                                            >
                                                Cancel
                                            </button>
                                        ) : (
                                            <button
                                                onClick={() => setRows(rows.filter((r) => r.id !== null))}
                                                className="px-4 py-2 text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                                            >
                                                Remove
                                            </button>
                                        )}
                                    </div>
                                </div>
                            ) : (
                                <p className="text-xs text-on-surface-variant font-mono">
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
