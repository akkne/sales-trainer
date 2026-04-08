"use client";

import { useState } from "react";
import Link from "next/link";
import { use } from "react";
import {
    useAdminLessons,
    useUpdateLesson,
    useAdminExercises,
    useCreateExercise,
    useUpdateExercise,
    useDeleteExercise,
} from "@/lib/hooks/useAdmin";

const EXERCISE_TYPES = ["multiple_choice", "fill_blank", "open_question"];

const CONTENT_TEMPLATES: Record<string, object> = {
    multiple_choice: {
        situation: "",
        question: "",
        options: ["", "", "", ""],
        correctOptionIndex: 0,
        explanation: "",
    },
    fill_blank: {
        characterName: "",
        characterLine: "___",
        options: ["", "", "", ""],
        correctOptionIndex: 0,
        explanation: "",
    },
    open_question: {
        question: "",
        aiPrompt: "",
    },
};

export default function AdminLessonDetailPage({
    params,
}: {
    params: Promise<{ id: string; lessonId: string }>;
}) {
    const { id: skillId, lessonId } = use(params);

    const { data: lessons = [] } = useAdminLessons(skillId);
    const lesson = lessons.find((l) => l.id === lessonId);

    const updateLesson = useUpdateLesson(skillId, lessonId);
    const [editLessonMode, setEditLessonMode] = useState(false);
    const [lessonForm, setLessonForm] = useState({
        title: "",
        sortOrder: 0,
        difficultyLevel: 1,
        xpReward: 10,
    });

    const { data: exercises = [], isLoading } = useAdminExercises(lessonId);
    const createExercise = useCreateExercise(lessonId);
    const deleteExercise = useDeleteExercise(lessonId);

    const [showExForm, setShowExForm] = useState(false);
    const [exType, setExType] = useState(EXERCISE_TYPES[0]);
    const [exSortOrder, setExSortOrder] = useState(0);
    const [exContentJson, setExContentJson] = useState(
        JSON.stringify(CONTENT_TEMPLATES[EXERCISE_TYPES[0]], null, 2)
    );
    const [jsonError, setJsonError] = useState<string | null>(null);

    const [editExerciseId, setEditExerciseId] = useState<string | null>(null);
    const [editExType, setEditExType] = useState("");
    const [editExSortOrder, setEditExSortOrder] = useState(0);
    const [editExJson, setEditExJson] = useState("");
    const [editJsonError, setEditJsonError] = useState<string | null>(null);

    const updateExercise = useUpdateExercise(
        lessonId,
        editExerciseId ?? ""
    );

    const [confirmDeleteExId, setConfirmDeleteExId] = useState<string | null>(null);

    function startEditLesson() {
        if (!lesson) return;
        setLessonForm({
            title: lesson.title,
            sortOrder: lesson.sortOrder,
            difficultyLevel: lesson.difficultyLevel,
            xpReward: lesson.xpReward,
        });
        setEditLessonMode(true);
    }

    async function handleSaveLesson() {
        await updateLesson.mutateAsync(lessonForm);
        setEditLessonMode(false);
    }

    async function handleCreateExercise() {
        try {
            const content = JSON.parse(exContentJson);
            setJsonError(null);
            await createExercise.mutateAsync({ type: exType, sortOrder: exSortOrder, content });
            setExType(EXERCISE_TYPES[0]);
            setExSortOrder(0);
            setExContentJson(JSON.stringify(CONTENT_TEMPLATES[EXERCISE_TYPES[0]], null, 2));
            setShowExForm(false);
        } catch {
            setJsonError("Invalid JSON");
        }
    }

    function startEditExercise(ex: {
        id: string;
        type: string;
        sortOrder: number;
        content: Record<string, unknown>;
    }) {
        setEditExerciseId(ex.id);
        setEditExType(ex.type);
        setEditExSortOrder(ex.sortOrder);
        setEditExJson(JSON.stringify(ex.content, null, 2));
        setEditJsonError(null);
    }

    async function handleSaveExercise() {
        try {
            const content = JSON.parse(editExJson);
            setEditJsonError(null);
            await updateExercise.mutateAsync({
                type: editExType,
                sortOrder: editExSortOrder,
                content,
            });
            setEditExerciseId(null);
        } catch {
            setEditJsonError("Invalid JSON");
        }
    }

    if (!lesson) {
        return <p className="text-sm text-on-surface-variant">Loading lesson...</p>;
    }

    return (
        <div>
            <div className="mb-6">
                <Link
                    href={`/admin/skills/${skillId}`}
                    className="text-xs text-on-surface-variant hover:text-on-surface transition-colors"
                >
                    ← {lesson.title ? `Back to skill` : "Back"}
                </Link>
            </div>

            {/* Lesson info */}
            <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-5 mb-8">
                <div className="flex items-center justify-between mb-4">
                    <h1 className="text-lg font-semibold text-on-surface">{lesson.title}</h1>
                    {!editLessonMode && (
                        <button
                            onClick={startEditLesson}
                            className="text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                        >
                            Edit
                        </button>
                    )}
                </div>
                {editLessonMode ? (
                    <div>
                        <div className="grid grid-cols-2 gap-4">
                            <label className="block col-span-2">
                                <span className="text-xs text-on-surface-variant">Title</span>
                                <input
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={lessonForm.title}
                                    onChange={(e) =>
                                        setLessonForm({ ...lessonForm, title: e.target.value })
                                    }
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">Sort order</span>
                                <input
                                    type="number"
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={lessonForm.sortOrder}
                                    onChange={(e) =>
                                        setLessonForm({
                                            ...lessonForm,
                                            sortOrder: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">Difficulty</span>
                                <input
                                    type="number"
                                    min={1}
                                    max={3}
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={lessonForm.difficultyLevel}
                                    onChange={(e) =>
                                        setLessonForm({
                                            ...lessonForm,
                                            difficultyLevel: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">XP reward</span>
                                <input
                                    type="number"
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={lessonForm.xpReward}
                                    onChange={(e) =>
                                        setLessonForm({
                                            ...lessonForm,
                                            xpReward: Number(e.target.value),
                                        })
                                    }
                                />
                            </label>
                        </div>
                        <div className="flex gap-3 mt-4">
                            <button
                                onClick={handleSaveLesson}
                                disabled={updateLesson.isPending}
                                className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                            >
                                {updateLesson.isPending ? "Saving..." : "Save"}
                            </button>
                            <button
                                onClick={() => setEditLessonMode(false)}
                                className="px-4 py-2 text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                            >
                                Cancel
                            </button>
                        </div>
                    </div>
                ) : (
                    <dl className="grid grid-cols-3 gap-3 text-sm">
                        <div>
                            <dt className="text-xs text-on-surface-variant">Order</dt>
                            <dd className="text-on-surface">{lesson.sortOrder}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-on-surface-variant">Difficulty</dt>
                            <dd className="text-on-surface">{lesson.difficultyLevel}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-on-surface-variant">XP reward</dt>
                            <dd className="text-on-surface">{lesson.xpReward}</dd>
                        </div>
                    </dl>
                )}
            </div>

            {/* Exercises */}
            <div className="flex items-center justify-between mb-4">
                <h2 className="text-base font-semibold text-on-surface">Exercises</h2>
                <div className="flex gap-2">
                    <Link
                        href={`/admin/skills/${skillId}/lessons/${lessonId}/exercises`}
                        className="px-3 py-1.5 text-sm bg-secondary text-on-secondary rounded-md hover:bg-secondary/90 transition-colors"
                    >
                        Edit Exercises
                    </Link>
                    <button
                        onClick={() => setShowExForm((v) => !v)}
                        className="px-3 py-1.5 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim transition-colors"
                    >
                        {showExForm ? "Cancel" : "+ New exercise"}
                    </button>
                </div>
            </div>

            {showExForm && (
                <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-5 mb-4">
                    <div className="grid grid-cols-2 gap-4 mb-4">
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Type</span>
                            <select
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={exType}
                                onChange={(e) => {
                                    setExType(e.target.value);
                                    setExContentJson(
                                        JSON.stringify(
                                            CONTENT_TEMPLATES[e.target.value] ?? {},
                                            null,
                                            2
                                        )
                                    );
                                }}
                            >
                                {EXERCISE_TYPES.map((t) => (
                                    <option key={t} value={t}>
                                        {t}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Sort order</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={exSortOrder}
                                onChange={(e) => setExSortOrder(Number(e.target.value))}
                            />
                        </label>
                    </div>
                    <label className="block">
                        <span className="text-xs text-on-surface-variant">Content (JSON)</span>
                        <textarea
                            rows={10}
                            className={`mt-1 w-full border rounded-md px-3 py-2 text-xs font-mono focus:outline-none focus:ring-1 focus:ring-primary ${
                                jsonError ? "border-error" : "border-outline-variant"
                            }`}
                            value={exContentJson}
                            onChange={(e) => {
                                setExContentJson(e.target.value);
                                setJsonError(null);
                            }}
                        />
                        {jsonError && (
                            <p className="mt-1 text-xs text-error">{jsonError}</p>
                        )}
                    </label>
                    <button
                        onClick={handleCreateExercise}
                        disabled={createExercise.isPending}
                        className="mt-4 px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                    >
                        {createExercise.isPending ? "Saving..." : "Create exercise"}
                    </button>
                </div>
            )}

            {isLoading ? (
                <p className="text-sm text-on-surface-variant">Loading...</p>
            ) : exercises.length === 0 ? (
                <p className="text-sm text-on-surface-variant">No exercises yet.</p>
            ) : (
                <div className="space-y-3">
                    {exercises.map((ex) => (
                        <div
                            key={ex.id}
                            className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-4"
                        >
                            {editExerciseId === ex.id ? (
                                <div>
                                    <div className="grid grid-cols-2 gap-4 mb-4">
                                        <label className="block">
                                            <span className="text-xs text-on-surface-variant">Type</span>
                                            <select
                                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                                value={editExType}
                                                onChange={(e) => setEditExType(e.target.value)}
                                            >
                                                {EXERCISE_TYPES.map((t) => (
                                                    <option key={t} value={t}>
                                                        {t}
                                                    </option>
                                                ))}
                                            </select>
                                        </label>
                                        <label className="block">
                                            <span className="text-xs text-on-surface-variant">
                                                Sort order
                                            </span>
                                            <input
                                                type="number"
                                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                                value={editExSortOrder}
                                                onChange={(e) =>
                                                    setEditExSortOrder(Number(e.target.value))
                                                }
                                            />
                                        </label>
                                    </div>
                                    <label className="block">
                                        <span className="text-xs text-on-surface-variant">
                                            Content (JSON)
                                        </span>
                                        <textarea
                                            rows={10}
                                            className={`mt-1 w-full border rounded-md px-3 py-2 text-xs font-mono focus:outline-none focus:ring-1 focus:ring-primary ${
                                                editJsonError
                                                    ? "border-error"
                                                    : "border-outline-variant"
                                            }`}
                                            value={editExJson}
                                            onChange={(e) => {
                                                setEditExJson(e.target.value);
                                                setEditJsonError(null);
                                            }}
                                        />
                                        {editJsonError && (
                                            <p className="mt-1 text-xs text-error">
                                                {editJsonError}
                                            </p>
                                        )}
                                    </label>
                                    <div className="flex gap-3 mt-3">
                                        <button
                                            onClick={handleSaveExercise}
                                            disabled={updateExercise.isPending}
                                            className="px-3 py-1.5 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                                        >
                                            {updateExercise.isPending ? "Saving..." : "Save"}
                                        </button>
                                        <button
                                            onClick={() => setEditExerciseId(null)}
                                            className="px-3 py-1.5 text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div className="flex items-start justify-between gap-4">
                                    <div className="flex-1 min-w-0">
                                        <div className="flex items-center gap-2 mb-2">
                                            <span className="text-xs bg-surface-container text-on-surface-variant px-2 py-0.5 rounded font-mono">
                                                {ex.type}
                                            </span>
                                            <span className="text-xs text-on-surface-variant">
                                                order: {ex.sortOrder}
                                            </span>
                                        </div>
                                        <pre className="text-xs text-on-surface-variant font-mono overflow-x-auto whitespace-pre-wrap line-clamp-3">
                                            {JSON.stringify(ex.content, null, 2)}
                                        </pre>
                                    </div>
                                    <div className="flex gap-2 shrink-0">
                                        <button
                                            onClick={() => startEditExercise(ex)}
                                            className="text-xs text-on-surface-variant hover:text-on-surface transition-colors"
                                        >
                                            Edit
                                        </button>
                                        {confirmDeleteExId === ex.id ? (
                                            <>
                                                <button
                                                    onClick={() => {
                                                        deleteExercise.mutate(ex.id);
                                                        setConfirmDeleteExId(null);
                                                    }}
                                                    className="text-xs text-error hover:underline"
                                                >
                                                    Confirm
                                                </button>
                                                <button
                                                    onClick={() => setConfirmDeleteExId(null)}
                                                    className="text-xs text-on-surface-variant hover:underline"
                                                >
                                                    Cancel
                                                </button>
                                            </>
                                        ) : (
                                            <button
                                                onClick={() => setConfirmDeleteExId(ex.id)}
                                                className="text-xs text-on-surface-variant hover:text-error transition-colors"
                                            >
                                                Delete
                                            </button>
                                        )}
                                    </div>
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}
