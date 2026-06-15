"use client";

import { useState, useMemo } from "react";
import Link from "next/link";
import {
    useAdminAllLessons,
    useAdminAllTopics,
    useAdminSkills,
    useUpdateLesson,
    useDeleteLesson,
    useImportLessons,
    type AdminLessonWithTopic,
} from "@/features/admin/hooks/use-admin";
import { ImportPanel } from "@/features/admin/components/import-panel";
import { buildExerciseImportTemplate } from "@/features/admin/components/exercise-editors";

type SortKey = "topicTitle" | "title" | "orderInTopic";
type SortDir = "asc" | "desc";

const LESSONS_TEMPLATE = [
    {
        topicIconicName: "intro-cold-call",
        title: "First Steps",
        orderInTopic: 1,
        exercises: [buildExerciseImportTemplate("choose_option", 1)],
    },
];

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

    const updateLesson = useUpdateLesson(editState?.lessonId ?? "");
    const deleteLesson = useDeleteLesson(deleteConfirm?.topicId ?? "");

    function toggleSort(key: SortKey) {
        if (sortKey === key) setSortDir(d => d === "asc" ? "desc" : "asc");
        else { setSortKey(key); setSortDir("asc"); }
    }


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
            const searchQuery = search.trim().toLowerCase();
            items = items.filter(l =>
                l.title.toLowerCase().includes(searchQuery) ||
                l.topicTitle.toLowerCase().includes(searchQuery)
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

    const groupedByTopic = useMemo(() => {
        const groups: Record<string, { topicTitle: string; lessons: AdminLessonWithTopic[] }> = {};
        for (const lesson of filtered) {
            if (!groups[lesson.topicId]) {
                groups[lesson.topicId] = { topicTitle: lesson.topicTitle, lessons: [] };
            }
            groups[lesson.topicId].lessons.push(lesson);
        }
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

    function SortIcon({ col }: { col: SortKey }) {
        if (sortKey !== col) return <span className="ml-1 text-ink-4">↕</span>;
        return <span className="ml-1 text-ink-3">{sortDir === "asc" ? "↑" : "↓"}</span>;
    }

    return (
        <div className="max-w-7xl">
            <div className="flex items-center justify-between mb-6">
                <div>
                    <h1 className="text-xl font-semibold text-ink">Lessons</h1>
                    <p className="text-sm text-ink-3 mt-0.5">{lessons.length} total</p>
                </div>
            </div>

            <ImportPanel
                title="Import Lessons"
                description='JSON array: [{ topicIconicName, title, orderInTopic, exercises[] }]. Download template for the full exercise schema.'
                templateData={LESSONS_TEMPLATE}
                templateFilename="lessons_template.json"
                onImport={async ({ text }) => {
                    const file = new File([text], "import.json", { type: "application/json" });
                    const result = await importLessons.mutateAsync(file);
                    return {
                        created: result.lessonsCreated,
                        updated: result.lessonsUpdated,
                        errors: result.errors,
                    };
                }}
            />

            <div className="bg-surface rounded-2xl p-4 mb-4 flex flex-wrap gap-3 items-end">
                <div>
                    <label className="block text-xs text-ink-3 mb-1">Search</label>
                    <input
                        className="border-line rounded px-3 py-1.5 text-sm w-52 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        placeholder="Title or topic…"
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                    />
                </div>
                <div>
                    <label className="block text-xs text-ink-3 mb-1">Skill</label>
                    <select
                        className="border-line rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
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
                    <label className="block text-xs text-ink-3 mb-1">Topic</label>
                    <select
                        className="border-line rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
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
                        className="text-xs text-ink-3 hover:text-ink transition-colors pb-1.5"
                    >
                        Clear filters
                    </button>
                )}
                <span className="text-xs text-ink-3 ml-auto pb-1.5">{filtered.length} shown</span>
            </div>

            {isLoading ? (
                <p className="text-sm text-ink-3 py-8 text-center">Loading…</p>
            ) : filtered.length === 0 ? (
                <p className="text-sm text-ink-3 py-8 text-center">No lessons found.</p>
            ) : (
                <div className="space-y-6">
                    {groupedByTopic.map(([topicId, group]) => (
                        <div key={topicId}>
                            <h3 className="text-sm font-medium text-ink-3 mb-2 flex items-center gap-2">
                                <span className="px-2 py-0.5 bg-indigo-soft text-indigo rounded text-xs">
                                    {group.topicTitle}
                                </span>
                                <span className="text-xs text-ink-3">
                                    {group.lessons.length} lesson{group.lessons.length !== 1 ? 's' : ''}
                                </span>
                            </h3>
                            <div className="bg-surface rounded-2xl overflow-hidden">
                                <table className="w-full text-sm border-collapse">
                                    <thead>
                                        <tr className="border-b border-line bg-surface">
                                            <th
                                                className="text-left py-2.5 px-4 text-xs font-medium text-ink-3 cursor-pointer hover:text-ink select-none"
                                                onClick={() => toggleSort("title")}
                                            >
                                                Title<SortIcon col="title" />
                                            </th>
                                            <th
                                                className="text-left py-2.5 px-4 text-xs font-medium text-ink-3 cursor-pointer hover:text-ink select-none w-24"
                                                onClick={() => toggleSort("orderInTopic")}
                                            >
                                                Order<SortIcon col="orderInTopic" />
                                            </th>
                                            <th className="py-2.5 px-4 text-xs font-medium text-ink-3 text-right w-48">Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {group.lessons.map(lesson => (
                                            <tr key={lesson.id} className="border-b border-line hover:bg-bg-2">
                                                {editState?.lessonId === lesson.id ? (
                                                    <>
                                                        <td className="py-2 px-4">
                                                            <input
                                                                className="border-line rounded px-2 py-1 text-sm w-full focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                                value={editState.title}
                                                                onChange={e => setEditState(s => s && ({ ...s, title: e.target.value }))}
                                                            />
                                                        </td>
                                                        <td className="py-2 px-4">
                                                            <input
                                                                type="number" min={1}
                                                                className="border-line rounded px-2 py-1 text-sm w-16 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                                value={editState.orderInTopic}
                                                                onChange={e => setEditState(s => s && ({ ...s, orderInTopic: e.target.value }))}
                                                            />
                                                        </td>
                                                        <td className="py-2 px-4 text-right">
                                                            <div className="flex items-center justify-end gap-2">
                                                                {editError && <span className="text-xs text-bad">{editError}</span>}
                                                                <button
                                                                    onClick={handleUpdate}
                                                                    disabled={updateLesson.isPending}
                                                                    className="text-xs px-3 py-1 bg-ink text-bg rounded hover:opacity-90 disabled:opacity-40 transition-colors"
                                                                >
                                                                    {updateLesson.isPending ? "Saving…" : "Save"}
                                                                </button>
                                                                <button
                                                                    onClick={() => setEditState(null)}
                                                                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                                                                >
                                                                    Cancel
                                                                </button>
                                                            </div>
                                                        </td>
                                                    </>
                                                ) : (
                                                    <>
                                                        <td className="py-2.5 px-4 text-ink">
                                                            <Link
                                                                href={`/admin/lessons/${lesson.id}/exercises`}
                                                                className="hover:underline"
                                                            >
                                                                {lesson.title}
                                                            </Link>
                                                        </td>
                                                        <td className="py-2.5 px-4 text-ink-3">{lesson.orderInTopic}</td>
                                                        <td className="py-2.5 px-4 text-right">
                                                            <div className="flex items-center justify-end gap-3">
                                                                <Link
                                                                    href={`/admin/lessons/${lesson.id}/exercises`}
                                                                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                                                                >
                                                                    Exercises →
                                                                </Link>
                                                                <button
                                                                    onClick={() => startEdit(lesson)}
                                                                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                                                                >
                                                                    Edit
                                                                </button>
                                                                <button
                                                                    onClick={() => setDeleteConfirm(lesson)}
                                                                    className="text-xs text-bad hover:text-bad transition-colors"
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

            {deleteConfirm && (
                <div className="fixed inset-0 bg-black/30 flex items-center justify-center z-50">
                    <div className="bg-surface rounded-2xl p-6 w-96 shadow-lg">
                        <h3 className="text-sm font-semibold text-ink mb-2">Delete lesson?</h3>
                        <p className="text-sm text-ink-3 mb-1">
                            <span className="font-medium text-ink">{deleteConfirm.title}</span>
                        </p>
                        <p className="text-xs text-ink-3 mb-5">
                            Topic: {deleteConfirm.topicTitle}. All exercises in this lesson will also be deleted.
                        </p>
                        <div className="flex gap-3 justify-end">
                            <button
                                onClick={() => setDeleteConfirm(null)}
                                className="px-4 py-2 text-sm border border-line rounded-md text-ink-3 hover:bg-bg-2 transition-colors"
                            >
                                Cancel
                            </button>
                            <button
                                onClick={handleDelete}
                                disabled={deleteLesson.isPending}
                                className="px-4 py-2 text-sm bg-bad text-white rounded-md hover:bg-bad/90 disabled:opacity-40 transition-colors"
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
