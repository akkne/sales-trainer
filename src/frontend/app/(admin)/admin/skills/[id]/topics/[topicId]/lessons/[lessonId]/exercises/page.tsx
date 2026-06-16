"use client";

import { useState } from "react";
import { use } from "react";
import Link from "next/link";
import {
    useAdminExercises,
    useCreateExercise,
    useDeleteExercise,
    useImportExercises,
    AdminExercise,
} from "@/features/admin/hooks/use-admin";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { ImportPanel } from "@/features/admin/components/import-panel";

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
    TheoryCardContent,
    emptyContentFor,
    buildExerciseImportTemplate,
    validateExerciseContent,
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
    TheoryCardEditor,
} from "@/features/admin/components/exercise-editors";

function typeBadgeColor(): string {
    return "bg-bg-2 text-ink-3 border border-line";
}

function moveExercise(exercises: ExerciseRow[], from: number, to: number): ExerciseRow[] {
    const result = [...exercises];
    const [moved] = result.splice(from, 1);
    result.splice(to, 0, moved);
    return result.map((ex, i) => ({ ...ex, sortOrder: i + 1 }));
}

interface ExerciseRow {
    id: string | null;
    type: ExerciseType;
    sortOrder: number;
    content: ExerciseContent;
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
        case "theory_card":
            return <TheoryCardEditor content={content as TheoryCardContent} onChange={onChange} />;
    }
}

function renderContentPreview(row: ExerciseRow): string {
    const c = row.content as unknown as Record<string, unknown>;
    switch (row.type) {
        case "choose_option":
            return String(c.situation || "(no situation)").slice(0, 60);
        case "fill_blank":
            return String(c.before || "(no before)").slice(0, 60);
        case "free_text":
            return String(c.instruction || "(no instruction)").slice(0, 60);
        case "reorder":
            return String(c.instruction || "(no instruction)").slice(0, 60);
        case "match_pairs":
            return String(c.instruction || "(no instruction)").slice(0, 60);
        case "categorize":
            return String(c.instruction || "(no instruction)").slice(0, 60);
        case "spot_mistake": {
            const dialogue = c.dialogue as Array<{ speaker: string; text: string }> | undefined;
            return dialogue ? `${dialogue.length} lines` : "(no dialogue)";
        }
        case "rewrite":
            return String(c.original || "(no original)").slice(0, 60);
        case "ai_dialogue":
            return String(c.scenario || "(no scenario)").slice(0, 60);
        case "evaluate_call": {
            const transcript = c.transcript as unknown[] | undefined;
            const axes = c.evaluation_axes as unknown[] | undefined;
            return `${transcript?.length ?? 0} lines, ${axes?.length ?? 0} axes`;
        }
        case "theory_card":
            return `theory · ${String(c.layout ?? "?")}`;
        default:
            return "(preview)";
    }
}

const IMPORT_TEMPLATE = EXERCISE_TYPES.map((t, i) => buildExerciseImportTemplate(t, i + 1));

export default function AdminTopicLessonExercisesPage({
    params,
}: {
    params: Promise<{ id: string; topicId: string; lessonId: string }>;
}) {
    const { id: skillId, topicId, lessonId } = use(params);
    const { data: exercises = [], isLoading } = useAdminExercises(lessonId);

    const createMut = useCreateExercise(lessonId);
    const deleteMut = useDeleteExercise(lessonId);
    const importMut = useImportExercises(lessonId);
    const qc = useQueryClient();
    const updateExerciseMut = useMutation({
        mutationFn: ({ exerciseId, body }: { exerciseId: string; body: Omit<AdminExercise, "id" | "lessonId"> }) =>
            apiClient.put<AdminExercise>(`/admin/exercises/${exerciseId}`, body),
        onSuccess: () => {
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
    });

    const [localRows, setLocalRows] = useState<ExerciseRow[] | null>(null);
    const [editingId, setEditingId] = useState<string | null>(null);
    const [validationErrors, setValidationErrors] = useState<Record<string, string[]>>({});

    const rows: ExerciseRow[] = localRows ?? exercises.map((ex) => ({
        id: ex.id,
        type: ex.type as ExerciseType,
        sortOrder: ex.orderInLesson,
        content: ex.content as unknown as ExerciseContent,
    }));

    function setRows(newRows: ExerciseRow[] | ((prev: ExerciseRow[]) => ExerciseRow[])) {
        if (typeof newRows === "function") {
            setLocalRows((prev) => newRows(prev ?? rows));
        } else {
            setLocalRows(newRows);
        }
    }

    const cardCls = "bg-surface border border-line rounded-2xl p-4";

    async function saveExercise(row: ExerciseRow) {
        const errors = validateExerciseContent(row.type, row.content);
        if (errors.length > 0) {
            setValidationErrors((prev) => ({ ...prev, [row.id ?? "__new__"]: errors }));
            return;
        }
        setValidationErrors((prev) => { const next = { ...prev }; delete next[row.id ?? "__new__"]; return next; });

        if (row.id) {
            await updateExerciseMut.mutateAsync({
                exerciseId: row.id,
                body: {
                    type: row.type,
                    orderInLesson: row.sortOrder,
                    content: row.content as unknown as Record<string, unknown>,
                    customAiPrompt: null,
                },
            });
        } else {
            await createMut.mutateAsync({
                type: row.type,
                orderInLesson: row.sortOrder,
                content: row.content as unknown as Record<string, unknown>,
                customAiPrompt: null,
            });
        }
        setEditingId(null);
    }

    function addExercise() {
        const newRow: ExerciseRow = {
            id: null,
            type: "choose_option",
            sortOrder: rows.length + 1,
            content: emptyContentFor("choose_option"),
        };
        setRows([...rows, newRow]);
        setEditingId("__new__");
    }

    function deleteRow(id: string | null) {
        if (!id) return;
        if (!confirm("Delete this exercise?")) return;
        setRows(rows.filter((r) => r.id !== id));
        deleteMut.mutate(id);
        if (editingId === id) setEditingId(null);
    }

    const isLoadingMut = createMut.isPending || updateExerciseMut.isPending;

    return (
        <div>
            <div className="mb-6">
                <Link
                    href={`/admin/skills/${skillId}/topics/${topicId}`}
                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    ← Back to topic
                </Link>
            </div>

            <div className="flex flex-wrap gap-3 items-center justify-between mb-4">
                <h1 className="text-lg font-semibold text-ink">Edit Exercises</h1>
                <button
                    onClick={addExercise}
                    className="px-3 py-1.5 text-sm bg-ink text-bg rounded-md hover:opacity-90 transition-colors"
                >
                    + Add exercise
                </button>
            </div>

            <ImportPanel
                title="Import Exercises"
                description="Paste a JSON array of exercise objects. Download the template to see all supported types."
                templateData={IMPORT_TEMPLATE}
                templateFilename="exercises_template.json"
                validate={(parsed) => {
                    if (!Array.isArray(parsed)) return ["JSON must be an array of exercise objects."];
                    const errs: string[] = [];
                    (parsed as Array<Record<string, unknown>>).forEach((item, idx) => {
                        const type = item.type as ExerciseType;
                        if (!EXERCISE_TYPES.includes(type)) {
                            errs.push(`[${idx}] unknown type "${String(item.type)}"`);
                            return;
                        }
                        const contentErrors = validateExerciseContent(type, item.content);
                        contentErrors.forEach((e) => errs.push(`[${idx}] ${e}`));
                    });
                    return errs;
                }}
                onImport={async ({ parsed }) => {
                    const result = await importMut.mutateAsync(parsed as Omit<AdminExercise, "id" | "lessonId">[]);
                    setLocalRows(null);
                    return {
                        created: result.exercisesCreated,
                        updated: result.exercisesUpdated,
                        errors: result.errors,
                    };
                }}
            />

            {rows.length === 0 && !isLoading && (
                <p className="text-sm text-ink-3">No exercises yet. Click &quot;+ Add exercise&quot; to create one.</p>
            )}
            {isLoading && <p className="text-sm text-ink-3">Loading...</p>}

            <div className="space-y-4">
                {rows.map((row, index) => {
                    const isEditing = editingId === row.id || (editingId === "__new__" && row.id === null);
                    const rowKey = row.id ?? "__new__";
                    const rowErrors = validationErrors[rowKey] ?? [];

                    return (
                        <div key={rowKey} className={cardCls}>
                            <div className="flex items-center justify-between mb-3">
                                <div className="flex items-center gap-2">
                                    <div className="flex flex-col gap-0.5">
                                        <button
                                            disabled={index === 0}
                                            onClick={() => setRows(moveExercise(rows, index, index - 1))}
                                            className="text-ink-3 hover:text-ink disabled:opacity-30 text-xs leading-none"
                                            title="Move up"
                                        >▲</button>
                                        <button
                                            disabled={index === rows.length - 1}
                                            onClick={() => setRows(moveExercise(rows, index, index + 1))}
                                            className="text-ink-3 hover:text-ink disabled:opacity-30 text-xs leading-none"
                                            title="Move down"
                                        >▼</button>
                                    </div>
                                    <span className={`text-xs px-2 py-0.5 rounded font-mono ${typeBadgeColor()}`}>
                                        {TYPE_LABELS[row.type] ?? row.type}
                                    </span>
                                    <span className="text-xs text-ink-3">#{row.sortOrder}</span>
                                </div>
                                {!isEditing && (
                                    <div className="flex gap-3">
                                        <button
                                            onClick={() => setEditingId(row.id ?? "__new__")}
                                            className="text-xs text-ink-3 hover:text-ink transition-colors"
                                        >Edit</button>
                                        {row.id && (
                                            <button
                                                onClick={() => deleteRow(row.id)}
                                                className="text-xs text-bad hover:text-bad transition-colors"
                                            >Delete</button>
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
                                                    if (ri !== index || newType === r.type) return r;
                                                    return { ...r, type: newType, content: emptyContentFor(newType) };
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

                                    {rowErrors.length > 0 && (
                                        <div className="mt-3 p-3 bg-bad/10 rounded-md">
                                            <p className="text-xs text-bad font-medium">Fix these errors before saving:</p>
                                            <ul className="mt-1 text-xs text-bad font-mono list-disc pl-4">
                                                {rowErrors.map((e, i) => <li key={i}>{e}</li>)}
                                            </ul>
                                        </div>
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
                                                            }
                                                            : r
                                                        ));
                                                    }
                                                    setEditingId(null);
                                                }}
                                                className="px-4 py-2 text-sm text-ink-3 hover:text-ink transition-colors"
                                            >Cancel</button>
                                        ) : (
                                            <button
                                                onClick={() => setRows(rows.filter((r) => r.id !== null))}
                                                className="px-4 py-2 text-sm text-ink-3 hover:text-ink transition-colors"
                                            >Remove</button>
                                        )}
                                    </div>
                                </div>
                            ) : (
                                <p className="text-xs text-ink-3 font-mono">{renderContentPreview(row)}</p>
                            )}
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
