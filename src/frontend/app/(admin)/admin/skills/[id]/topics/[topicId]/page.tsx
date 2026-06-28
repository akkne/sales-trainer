"use client";

import { useState, useRef } from "react";
import Link from "next/link";
import { use } from "react";
import {
    useAdminTopics,
    useUpdateTopic,
    useAdminLessons,
    useCreateLesson,
    useDeleteLesson,
    useImportLessons,
    type AdminTopic,
    type LessonsImportResult,
} from "@/features/admin/hooks/use-admin";

const LESSONS_TEMPLATE = JSON.stringify([
    {
        topicIconicName: "introduction",
        title: "First Steps",
        orderInTopic: 1,
        exercises: [
            {
                type: "choose_option",
                orderInLesson: 1,
                content: {
                    situation: "The prospect says: 'This is too expensive'",
                    options: [
                        { text: "I understand. I can offer a discount.", is_correct: false },
                        { text: "Expensive compared to what, exactly?", is_correct: true },
                        { text: "This is the best price on the market.", is_correct: false }
                    ],
                    explanation: "It's better to clarify the objection than to immediately lower the price."
                },
                customAiPrompt: null
            }
        ]
    }
], null, 2);

function downloadLessonsTemplate() {
    const blob = new Blob([LESSONS_TEMPLATE], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement("a");
    anchorElement.href = url;
    anchorElement.download = "lessons_template.json";
    anchorElement.click();
    URL.revokeObjectURL(url);
}

export default function AdminTopicDetailPage({
    params,
}: {
    params: Promise<{ id: string; topicId: string }>;
}) {
    const { id: skillId, topicId } = use(params);

    const { data: topics = [] } = useAdminTopics(skillId);
    const topic = topics.find((t) => t.id === topicId);

    const updateTopic = useUpdateTopic(topicId);
    const [editMode, setEditMode] = useState(false);
    const [form, setForm] = useState<Omit<AdminTopic, "id" | "skillId"> | null>(null);

    const { data: lessons = [], isLoading: lessonsLoading } = useAdminLessons(topic?.iconicName || "");
    const createLesson = useCreateLesson(topic?.iconicName || "");
    const deleteLesson = useDeleteLesson(topicId);
    const importLessons = useImportLessons();

    const [showLessonForm, setShowLessonForm] = useState(false);
    const [lessonForm, setLessonForm] = useState({
        title: "",
        orderInTopic: 1,
    });
    const [confirmDeleteLessonId, setConfirmDeleteLessonId] = useState<string | null>(null);

    const fileInputRef = useRef<HTMLInputElement>(null);
    const [showImport, setShowImport] = useState(false);
    const [importResult, setImportResult] = useState<LessonsImportResult | null>(null);

    function startEdit() {
        if (!topic) return;
        setForm({
            iconicName: topic.iconicName,
            title: topic.title,
            orderInSkill: topic.orderInSkill,
        });
        setEditMode(true);
    }

    async function handleSaveTopic() {
        if (!form) return;
        await updateTopic.mutateAsync(form);
        setEditMode(false);
    }

    async function handleCreateLesson() {
        await createLesson.mutateAsync(lessonForm);
        setLessonForm({ title: "", orderInTopic: lessons.length + 2 });
        setShowLessonForm(false);
    }

    async function handleImport(e: React.ChangeEvent<HTMLInputElement>) {
        const file = e.target.files?.[0];
        if (!file) return;
        try {
            const result = await importLessons.mutateAsync(file);
            setImportResult(result);
        } catch {
            // Error handled by hook
        }
        if (fileInputRef.current) fileInputRef.current.value = "";
    }

    if (!topic) {
        return <p className="text-sm text-ink-3">Loading topic...</p>;
    }

    return (
        <div>
            <div className="mb-6">
                <Link
                    href={`/admin/skills/${skillId}`}
                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    ← Back to skill
                </Link>
            </div>

            {/* Topic card */}
            <div className="bg-surface rounded-2xl p-5 mb-8">
                <div className="flex flex-wrap gap-3 items-center justify-between mb-4">
                    <h1 className="text-lg font-semibold text-ink">{topic.title}</h1>
                    {!editMode && (
                        <button
                            onClick={startEdit}
                            className="text-sm text-ink-3 hover:text-ink transition-colors"
                        >
                            Edit
                        </button>
                    )}
                </div>

                {editMode && form ? (
                    <div>
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                            <label className="block">
                                <span className="text-xs text-ink-3">Iconic Name (English ID)</span>
                                <input
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={form.iconicName}
                                    onChange={(e) => setForm({ ...form, iconicName: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-ink-3">Title</span>
                                <input
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={form.title}
                                    onChange={(e) => setForm({ ...form, title: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-ink-3">Order in skill</span>
                                <input
                                    type="number"
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={form.orderInSkill}
                                    onChange={(e) =>
                                        setForm({ ...form, orderInSkill: Number(e.target.value) })
                                    }
                                />
                            </label>
                        </div>
                        <div className="flex gap-3 mt-4">
                            <button
                                onClick={handleSaveTopic}
                                disabled={updateTopic.isPending}
                                className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                            >
                                {updateTopic.isPending ? "Saving..." : "Save"}
                            </button>
                            <button
                                onClick={() => setEditMode(false)}
                                className="px-4 py-2 text-sm text-ink-3 hover:text-ink transition-colors"
                            >
                                Cancel
                            </button>
                        </div>
                    </div>
                ) : (
                    <dl className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-sm">
                        <div>
                            <dt className="text-xs text-ink-3">Iconic Name</dt>
                            <dd className="text-ink font-mono text-xs">{topic.iconicName}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-ink-3">Title</dt>
                            <dd className="text-ink">{topic.title}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-ink-3">Order in skill</dt>
                            <dd className="text-ink">{topic.orderInSkill}</dd>
                        </div>
                    </dl>
                )}
            </div>

            {/* Import Section */}
            {showImport && (
                <div className="bg-surface border border-line rounded-2xl p-5 mb-6">
                    <div className="flex flex-wrap gap-3 items-center justify-between mb-3">
                        <h2 className="text-sm font-medium text-ink">Import Lessons from JSON</h2>
                        <button
                            onClick={downloadLessonsTemplate}
                            className="text-xs text-ink-3 hover:text-ink transition-colors underline"
                        >
                            Download template
                        </button>
                    </div>
                    <p className="text-xs text-ink-3 mb-3">
                        JSON array with: <code className="bg-bg-2 px-1 rounded">{"{ topicIconicName, title, orderInTopic, exercises[] }"}</code>
                    </p>
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept=".json"
                        onChange={handleImport}
                        className="block w-full text-sm text-ink-3 file:mr-4 file:py-2 file:px-4 file:rounded-md file:border file:border-line file:text-sm file:bg-bg-2 file:text-ink hover:file:bg-surface-2 cursor-pointer"
                    />
                    {importLessons.isPending && (
                        <p className="mt-3 text-xs text-ink-3">Importing...</p>
                    )}
                    {importLessons.isError && (
                        <p className="mt-3 text-xs text-bad">{(importLessons.error as Error).message}</p>
                    )}
                    {importResult && (
                        <div className="mt-3 p-3 bg-bg-2 rounded-md">
                            <p className="text-xs text-ink">
                                Lessons created: <span className="font-medium">{importResult.lessonsCreated}</span> |
                                Updated: <span className="font-medium">{importResult.lessonsUpdated}</span> |
                                Exercises created: <span className="font-medium">{importResult.exercisesCreated}</span> |
                                Updated: <span className="font-medium">{importResult.exercisesUpdated}</span>
                            </p>
                            {importResult.errors.length > 0 && (
                                <div className="mt-2">
                                    <p className="text-xs text-bad font-medium">{importResult.errors.length} error(s):</p>
                                    <ul className="mt-1 text-xs text-bad font-mono max-h-32 overflow-y-auto">
                                        {importResult.errors.map((e, i) => <li key={i}>{e}</li>)}
                                    </ul>
                                </div>
                            )}
                        </div>
                    )}
                </div>
            )}

            {/* Lessons */}
            <div className="flex flex-wrap gap-3 items-center justify-between mb-4">
                <h2 className="text-base font-semibold text-ink">Lessons</h2>
                <div className="flex flex-wrap items-center gap-3">
                    <button
                        onClick={() => { setShowImport(v => !v); setImportResult(null); }}
                        className="px-3 py-1.5 text-sm border border-line text-ink-3 rounded-md hover:bg-bg-2 transition-colors"
                    >
                        {showImport ? "Close Import" : "Import JSON"}
                    </button>
                    <button
                        onClick={() => setShowLessonForm((v) => !v)}
                        className="px-3 py-1.5 text-sm bg-ink text-bg rounded-md hover:opacity-90 transition-colors"
                    >
                        {showLessonForm ? "Cancel" : "+ New lesson"}
                    </button>
                </div>
            </div>

            {showLessonForm && (
                <div className="bg-surface rounded-2xl p-5 mb-4">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <label className="block">
                            <span className="text-xs text-ink-3">Title</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={lessonForm.title}
                                onChange={(e) =>
                                    setLessonForm({ ...lessonForm, title: e.target.value })
                                }
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Order in topic</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={lessonForm.orderInTopic}
                                onChange={(e) =>
                                    setLessonForm({
                                        ...lessonForm,
                                        orderInTopic: Number(e.target.value),
                                    })
                                }
                            />
                        </label>
                    </div>
                    <button
                        onClick={handleCreateLesson}
                        disabled={createLesson.isPending || !lessonForm.title}
                        className="mt-4 px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                    >
                        {createLesson.isPending ? "Saving..." : "Create lesson"}
                    </button>
                </div>
            )}

            {lessonsLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : lessons.length === 0 ? (
                <p className="text-sm text-ink-3">No lessons yet.</p>
            ) : (
                <div className="overflow-x-auto -mx-4 px-4">
                <table className="w-full text-sm border-collapse min-w-[360px]">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Title
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Order
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {lessons.map((lesson) => (
                            <tr
                                key={lesson.id}
                                className="border-b border-line hover:bg-bg-2"
                            >
                                <td className="py-2.5 px-3 font-medium text-ink">
                                    <Link
                                        href={`/admin/skills/${skillId}/topics/${topicId}/lessons/${lesson.id}`}
                                        className="hover:underline"
                                    >
                                        {lesson.title}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 text-ink-3">
                                    {lesson.orderInTopic}
                                </td>
                                <td className="py-2.5 px-3 text-right">
                                    {confirmDeleteLessonId === lesson.id ? (
                                        <span className="inline-flex gap-2">
                                            <button
                                                onClick={() => {
                                                    deleteLesson.mutate(lesson.id);
                                                    setConfirmDeleteLessonId(null);
                                                }}
                                                className="text-xs text-bad hover:underline"
                                            >
                                                Confirm
                                            </button>
                                            <button
                                                onClick={() => setConfirmDeleteLessonId(null)}
                                                className="text-xs text-ink-3 hover:underline"
                                            >
                                                Cancel
                                            </button>
                                        </span>
                                    ) : (
                                        <span className="inline-flex gap-3">
                                            <Link
                                                href={`/admin/skills/${skillId}/topics/${topicId}/lessons/${lesson.id}/exercises`}
                                                className="text-xs text-ink-3 hover:text-ink transition-colors"
                                            >
                                                Exercises →
                                            </Link>
                                            <button
                                                onClick={() => setConfirmDeleteLessonId(lesson.id)}
                                                className="text-xs text-ink-3 hover:text-bad transition-colors"
                                            >
                                                Delete
                                            </button>
                                        </span>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                </div>
            )}
        </div>
    );
}
