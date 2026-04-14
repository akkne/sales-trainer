"use client";

import { useState, useMemo, useRef } from "react";
import Link from "next/link";
import {
    useAdminAllLessons,
    useAdminAllTopics,
    useAdminSkills,
    useUpdateLesson,
    useDeleteLesson,
    useImportLessons,
    type AdminLessonWithTopic,
    type LessonsImportResult,
} from "@/lib/hooks/useAdmin";

type SortKey = "topicTitle" | "title" | "orderInTopic";
type SortDir = "asc" | "desc";

const LESSONS_TEMPLATE = JSON.stringify([
    {
        topicTitle: "Introduction",
        title: "First Steps",
        orderInTopic: 1,
        exercises: [
            {
                type: "multiple_choice",
                orderInLesson: 1,
                content: {
                    situation: "Client is hesitant",
                    question: "What is the best approach?",
                    options: ["Option A", "Option B", "Option C", "Option D"],
                    correctOptionIndex: 0,
                    explanation: "Option A is best because..."
                },
                customAiPrompt: null
            }
        ]
    }
], null, 2);

function downloadLessonsTemplate() {
    const blob = new Blob([LESSONS_TEMPLATE], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "lessons_template.json";
    a.click();
    URL.revokeObjectURL(url);
}

export default function LessonsPage() {
    const { data: lessons = [], isLoading } = useAdminAllLessons();
    const { data: topics = [] } = useAdminAllTopics();
    const { data: skills = [] } = useAdminSkills();
    const importLessons = useImportLessons();

    const [filterTopicId, setFilterTopicId] = useState("");
    const [filterSkillId, setFilterSkillId] = useState("");
    const [search, setSearch] = useState("");
    const [sortKey, setSortKey] = useState<SortKey>("topicTitle");
    const [sortDir, setSortDir] = useState<SortDir>("asc");

    const [editState, setEditState] = useState<{
        lessonId: string;
        topicId: string;
        title: string;
        orderInTopic: string;
    } | null>(null);
    const [editError, setEditError] = useState("");

    const [deleteConfirm, setDeleteConfirm] = useState<AdminLessonWithTopic | null>(null);

    const fileInputRef = useRef<HTMLInputElement>(null);
    const [showImport, setShowImport] = useState(false);
    const [importResult, setImportResult] = useState<LessonsImportResult | null>(null);

    const updateLesson = useUpdateLesson(editState?.lessonId ?? "");
    const deleteLesson = useDeleteLesson(deleteConfirm?.topicId ?? "");

    function toggleSort(key: SortKey) {
        if (sortKey === key) setSortDir(d => d === "asc" ? "desc" : "asc");
        else { setSortKey(key); setSortDir("asc"); }
    }

    // Get skill ID from topic
    const topicToSkillMap = useMemo(() => {
        const map: Record<string, string> = {};
        for (const topic of topics) {
            map[topic.id] = topic.skillId;
        }
        return map;
    }, [topics]);

    // Filter topics by selected skill
    const filteredTopicsBySkill = useMemo(() => {
        if (!filterSkillId) return topics;
        return topics.filter(t => t.skillId === filterSkillId);
    }, [topics, filterSkillId]);

    const filtered = useMemo(() => {
        let items = lessons;
        if (filterTopicId) items = items.filter(l => l.topicId === filterTopicId);
        if (filterSkillId && !filterTopicId) {
            const topicIdsInSkill = new Set(filteredTopicsBySkill.map(t => t.id));
            items = items.filter(l => topicIdsInSkill.has(l.topicId));
        }
        if (search.trim()) {
            const q = search.trim().toLowerCase();
            items = items.filter(l =>
                l.title.toLowerCase().includes(q) ||
                l.topicTitle.toLowerCase().includes(q)
            );
        }
        return [...items].sort((a, b) => {
            const av = a[sortKey];
            const bv = b[sortKey];
            const cmp = typeof av === "string"
                ? av.localeCompare(bv as string)
                : (av as number) - (bv as number);
            return sortDir === "asc" ? cmp : -cmp;
        });
    }, [lessons, filterTopicId, filterSkillId, filteredTopicsBySkill, search, sortKey, sortDir]);

    // Group by topic for better display
    const groupedByTopic = useMemo(() => {
        const groups: Record<string, { topicTitle: string; lessons: AdminLessonWithTopic[] }> = {};
        for (const lesson of filtered) {
            if (!groups[lesson.topicId]) {
                groups[lesson.topicId] = { topicTitle: lesson.topicTitle, lessons: [] };
            }
            groups[lesson.topicId].lessons.push(lesson);
        }
        // Sort lessons within each group
        for (const group of Object.values(groups)) {
            group.lessons.sort((a, b) => a.orderInTopic - b.orderInTopic);
        }
        return Object.entries(groups);
    }, [filtered]);

    function startEdit(l: AdminLessonWithTopic) {
        setEditState({
            lessonId: l.id,
            topicId: l.topicId,
            title: l.title,
            orderInTopic: String(l.orderInTopic),
        });
        setEditError("");
    }

    async function handleUpdate() {
        if (!editState) return;
        setEditError("");
        if (!editState.title.trim()) { setEditError("Title is required."); return; }
        try {
            await updateLesson.mutateAsync({
                title: editState.title.trim(),
                orderInTopic: parseInt(editState.orderInTopic) || 1,
            });
            setEditState(null);
        } catch (e) {
            setEditError((e as Error).message);
        }
    }

    async function handleDelete() {
        if (!deleteConfirm) return;
        await deleteLesson.mutateAsync(deleteConfirm.id);
        setDeleteConfirm(null);
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

    function SortIcon({ col }: { col: SortKey }) {
        if (sortKey !== col) return <span className="ml-1 text-outline-variant">↕</span>;
        return <span className="ml-1 text-on-surface-variant">{sortDir === "asc" ? "↑" : "↓"}</span>;
    }

    return (
        <div className="max-w-5xl">
            <div className="flex items-center justify-between mb-6">
                <div>
                    <h1 className="text-xl font-semibold text-on-surface">Lessons</h1>
                    <p className="text-sm text-on-surface-variant mt-0.5">{lessons.length} total</p>
                </div>
                <div className="flex gap-2">
                    <button
                        onClick={() => { setShowImport(v => !v); setImportResult(null); }}
                        className="px-4 py-2 text-sm border border-outline-variant text-on-surface-variant rounded-md hover:bg-surface-container transition-colors"
                    >
                        {showImport ? "Close Import" : "Import JSON"}
                    </button>
                </div>
            </div>

            {/* Import Section */}
            {showImport && (
                <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-5 mb-5">
                    <div className="flex items-center justify-between mb-3">
                        <h2 className="text-sm font-medium text-on-surface">Import Lessons from JSON</h2>
                        <button
                            onClick={downloadLessonsTemplate}
                            className="text-xs text-on-surface-variant hover:text-on-surface transition-colors underline"
                        >
                            Download template
                        </button>
                    </div>
                    <p className="text-xs text-on-surface-variant mb-3">
                        JSON format: <code className="bg-surface-container px-1 rounded">{"{ topicTitle, title, orderInTopic, exercises[] }"}</code>
                    </p>
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept=".json"
                        onChange={handleImport}
                        className="block w-full text-sm text-on-surface-variant file:mr-4 file:py-2 file:px-4 file:rounded-md file:border file:border-outline-variant file:text-sm file:bg-surface-container file:text-on-surface hover:file:bg-surface-container-high cursor-pointer"
                    />
                    {importLessons.isPending && (
                        <p className="mt-3 text-xs text-on-surface-variant">Importing...</p>
                    )}
                    {importLessons.isError && (
                        <p className="mt-3 text-xs text-error">{(importLessons.error as Error).message}</p>
                    )}
                    {importResult && (
                        <div className="mt-3 p-3 bg-surface-container rounded-md">
                            <p className="text-xs text-on-surface">
                                Lessons — Created: <span className="font-medium">{importResult.lessonsCreated}</span> | Updated: <span className="font-medium">{importResult.lessonsUpdated}</span>
                            </p>
                            <p className="text-xs text-on-surface mt-1">
                                Exercises — Created: <span className="font-medium">{importResult.exercisesCreated}</span> | Updated: <span className="font-medium">{importResult.exercisesUpdated}</span>
                            </p>
                            {importResult.errors.length > 0 && (
                                <div className="mt-2">
                                    <p className="text-xs text-error font-medium">{importResult.errors.length} error(s):</p>
                                    <ul className="mt-1 text-xs text-error font-mono max-h-32 overflow-y-auto">
                                        {importResult.errors.map((err, i) => <li key={i}>{err}</li>)}
                                    </ul>
                                </div>
                            )}
                        </div>
                    )}
                </div>
            )}

            {/* Filters */}
            <div className="bg-surface-container-lowest rounded-2xl p-4 mb-4 flex flex-wrap gap-3 items-end">
                <div>
                    <label className="block text-xs text-on-surface-variant mb-1">Search</label>
                    <input
                        className="border-outline-variant rounded px-3 py-1.5 text-sm w-52 focus:outline-none focus:ring-1 focus:ring-primary"
                        placeholder="Title or topic…"
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                    />
                </div>
                <div>
                    <label className="block text-xs text-on-surface-variant mb-1">Skill</label>
                    <select
                        className="border-outline-variant rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                        value={filterSkillId}
                        onChange={e => { setFilterSkillId(e.target.value); setFilterTopicId(""); }}
                    >
                        <option value="">All skills</option>
                        {skills.map(s => (
                            <option key={s.id} value={s.id}>{s.title}</option>
                        ))}
                    </select>
                </div>
                <div>
                    <label className="block text-xs text-on-surface-variant mb-1">Topic</label>
                    <select
                        className="border-outline-variant rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                        value={filterTopicId}
                        onChange={e => setFilterTopicId(e.target.value)}
                    >
                        <option value="">All topics</option>
                        {filteredTopicsBySkill.map(t => (
                            <option key={t.id} value={t.id}>{t.title}</option>
                        ))}
                    </select>
                </div>
                {(filterTopicId || filterSkillId || search) && (
                    <button
                        onClick={() => { setFilterTopicId(""); setFilterSkillId(""); setSearch(""); }}
                        className="text-xs text-on-surface-variant hover:text-on-surface transition-colors pb-1.5"
                    >
                        Clear filters
                    </button>
                )}
                <span className="text-xs text-on-surface-variant ml-auto pb-1.5">{filtered.length} shown</span>
            </div>

            {/* Table */}
            {isLoading ? (
                <p className="text-sm text-on-surface-variant py-8 text-center">Loading…</p>
            ) : filtered.length === 0 ? (
                <p className="text-sm text-on-surface-variant py-8 text-center">No lessons found.</p>
            ) : (
                <div className="space-y-6">
                    {groupedByTopic.map(([topicId, group]) => (
                        <div key={topicId}>
                            <h3 className="text-sm font-medium text-on-surface-variant mb-2 flex items-center gap-2">
                                <span className="px-2 py-0.5 bg-primary-container text-primary rounded text-xs">
                                    {group.topicTitle}
                                </span>
                                <span className="text-xs text-on-surface-variant">
                                    {group.lessons.length} lesson{group.lessons.length !== 1 ? 's' : ''}
                                </span>
                            </h3>
                            <div className="bg-surface-container-lowest rounded-2xl overflow-hidden">
                                <table className="w-full text-sm border-collapse">
                                    <thead>
                                        <tr className="border-b border-outline-variant bg-surface-container-low">
                                            <th
                                                className="text-left py-2.5 px-4 text-xs font-medium text-on-surface-variant cursor-pointer hover:text-on-surface select-none"
                                                onClick={() => toggleSort("title")}
                                            >
                                                Title<SortIcon col="title" />
                                            </th>
                                            <th
                                                className="text-left py-2.5 px-4 text-xs font-medium text-on-surface-variant cursor-pointer hover:text-on-surface select-none w-24"
                                                onClick={() => toggleSort("orderInTopic")}
                                            >
                                                Order<SortIcon col="orderInTopic" />
                                            </th>
                                            <th className="py-2.5 px-4 text-xs font-medium text-on-surface-variant text-right w-48">Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {group.lessons.map(lesson => (
                                            <tr key={lesson.id} className="border-b border-surface-container hover:bg-surface-container-low">
                                                {editState?.lessonId === lesson.id ? (
                                                    <>
                                                        <td className="py-2 px-4">
                                                            <input
                                                                className="border-outline-variant rounded px-2 py-1 text-sm w-full focus:outline-none focus:ring-1 focus:ring-primary"
                                                                value={editState.title}
                                                                onChange={e => setEditState(s => s && ({ ...s, title: e.target.value }))}
                                                            />
                                                        </td>
                                                        <td className="py-2 px-4">
                                                            <input
                                                                type="number" min={1}
                                                                className="border-outline-variant rounded px-2 py-1 text-sm w-16 focus:outline-none focus:ring-1 focus:ring-primary"
                                                                value={editState.orderInTopic}
                                                                onChange={e => setEditState(s => s && ({ ...s, orderInTopic: e.target.value }))}
                                                            />
                                                        </td>
                                                        <td className="py-2 px-4 text-right">
                                                            <div className="flex items-center justify-end gap-2">
                                                                {editError && <span className="text-xs text-error">{editError}</span>}
                                                                <button
                                                                    onClick={handleUpdate}
                                                                    disabled={updateLesson.isPending}
                                                                    className="text-xs px-3 py-1 bg-primary text-on-primary rounded hover:bg-primary-dim disabled:opacity-40 transition-colors"
                                                                >
                                                                    {updateLesson.isPending ? "Saving…" : "Save"}
                                                                </button>
                                                                <button
                                                                    onClick={() => setEditState(null)}
                                                                    className="text-xs text-on-surface-variant hover:text-on-surface transition-colors"
                                                                >
                                                                    Cancel
                                                                </button>
                                                            </div>
                                                        </td>
                                                    </>
                                                ) : (
                                                    <>
                                                        <td className="py-2.5 px-4 text-on-surface">
                                                            <Link
                                                                href={`/admin/lessons/${lesson.id}/exercises`}
                                                                className="hover:underline"
                                                            >
                                                                {lesson.title}
                                                            </Link>
                                                        </td>
                                                        <td className="py-2.5 px-4 text-on-surface-variant">{lesson.orderInTopic}</td>
                                                        <td className="py-2.5 px-4 text-right">
                                                            <div className="flex items-center justify-end gap-3">
                                                                <Link
                                                                    href={`/admin/lessons/${lesson.id}/exercises`}
                                                                    className="text-xs text-on-surface-variant hover:text-on-surface transition-colors"
                                                                >
                                                                    Exercises →
                                                                </Link>
                                                                <button
                                                                    onClick={() => startEdit(lesson)}
                                                                    className="text-xs text-on-surface-variant hover:text-on-surface transition-colors"
                                                                >
                                                                    Edit
                                                                </button>
                                                                <button
                                                                    onClick={() => setDeleteConfirm(lesson)}
                                                                    className="text-xs text-error hover:text-error transition-colors"
                                                                >
                                                                    Delete
                                                                </button>
                                                            </div>
                                                        </td>
                                                    </>
                                                )}
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {/* Delete confirm modal */}
            {deleteConfirm && (
                <div className="fixed inset-0 bg-black/30 flex items-center justify-center z-50">
                    <div className="bg-surface-container-lowest rounded-2xl p-6 w-96 shadow-lg">
                        <h3 className="text-sm font-semibold text-on-surface mb-2">Delete lesson?</h3>
                        <p className="text-sm text-on-surface-variant mb-1">
                            <span className="font-medium text-on-surface">{deleteConfirm.title}</span>
                        </p>
                        <p className="text-xs text-on-surface-variant mb-5">
                            Topic: {deleteConfirm.topicTitle}. All exercises in this lesson will also be deleted.
                        </p>
                        <div className="flex gap-3 justify-end">
                            <button
                                onClick={() => setDeleteConfirm(null)}
                                className="px-4 py-2 text-sm border border-outline-variant rounded-md text-on-surface-variant hover:bg-surface-container-low transition-colors"
                            >
                                Cancel
                            </button>
                            <button
                                onClick={handleDelete}
                                disabled={deleteLesson.isPending}
                                className="px-4 py-2 text-sm bg-error text-on-error rounded-md hover:bg-error/90 disabled:opacity-40 transition-colors"
                            >
                                {deleteLesson.isPending ? "Deleting…" : "Delete"}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
