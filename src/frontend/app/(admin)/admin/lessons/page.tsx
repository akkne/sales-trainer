"use client";

import { useState, useMemo } from "react";
import {
    useAdminAllLessons,
    useAdminSkills,
    useCreateLesson,
    useUpdateLesson,
    useDeleteLesson,
    type AdminLessonWithSkill,
} from "@/lib/hooks/useAdmin";

type SortKey = "skillTitle" | "title" | "sortOrder" | "difficultyLevel" | "xpReward";
type SortDir = "asc" | "desc";

const DIFFICULTY_LABELS: Record<number, string> = { 1: "Easy", 2: "Medium", 3: "Hard" };

function difficultyColor(d: number) {
    if (d === 1) return "text-primary bg-primary-container border-primary-container";
    if (d === 2) return "text-tertiary bg-tertiary-container border-tertiary-container";
    return "text-error bg-error-container border-error-container";
}

interface AddFormState {
    skillId: string;
    title: string;
    sortOrder: string;
    difficultyLevel: string;
    xpReward: string;
}

interface EditState {
    lessonId: string;
    skillId: string;
    title: string;
    sortOrder: string;
    difficultyLevel: string;
    xpReward: string;
}

export default function LessonsPage() {
    const { data: lessons = [], isLoading } = useAdminAllLessons();
    const { data: skills = [] } = useAdminSkills();

    const [filterSkillId, setFilterSkillId] = useState("");
    const [filterDifficulty, setFilterDifficulty] = useState(0);
    const [search, setSearch] = useState("");
    const [sortKey, setSortKey] = useState<SortKey>("skillTitle");
    const [sortDir, setSortDir] = useState<SortDir>("asc");

    const [showAdd, setShowAdd] = useState(false);
    const [addForm, setAddForm] = useState<AddFormState>({
        skillId: "", title: "", sortOrder: "1", difficultyLevel: "1", xpReward: "50",
    });
    const [addError, setAddError] = useState("");

    const [editState, setEditState] = useState<EditState | null>(null);
    const [editError, setEditError] = useState("");

    const [deleteConfirm, setDeleteConfirm] = useState<AdminLessonWithSkill | null>(null);

    const createLesson = useCreateLesson(addForm.skillId);
    const updateLesson = useUpdateLesson(
        editState?.skillId ?? "",
        editState?.lessonId ?? ""
    );
    const deleteLesson = useDeleteLesson(deleteConfirm?.skillId ?? "");

    function toggleSort(key: SortKey) {
        if (sortKey === key) setSortDir(d => d === "asc" ? "desc" : "asc");
        else { setSortKey(key); setSortDir("asc"); }
    }

    const filtered = useMemo(() => {
        let items = lessons;
        if (filterSkillId) items = items.filter(l => l.skillId === filterSkillId);
        if (filterDifficulty) items = items.filter(l => l.difficultyLevel === filterDifficulty);
        if (search.trim()) {
            const q = search.trim().toLowerCase();
            items = items.filter(l =>
                l.title.toLowerCase().includes(q) ||
                l.skillTitle.toLowerCase().includes(q) ||
                l.skillIcon.toLowerCase().includes(q)
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
    }, [lessons, filterSkillId, filterDifficulty, search, sortKey, sortDir]);

    async function handleAdd() {
        setAddError("");
        if (!addForm.skillId) { setAddError("Select a skill."); return; }
        if (!addForm.title.trim()) { setAddError("Title is required."); return; }
        try {
            await createLesson.mutateAsync({
                title: addForm.title.trim(),
                sortOrder: parseInt(addForm.sortOrder) || 1,
                difficultyLevel: parseInt(addForm.difficultyLevel) || 1,
                xpReward: parseInt(addForm.xpReward) || 50,
            });
            setShowAdd(false);
            setAddForm({ skillId: "", title: "", sortOrder: "1", difficultyLevel: "1", xpReward: "50" });
        } catch (e) {
            setAddError((e as Error).message);
        }
    }

    function startEdit(l: AdminLessonWithSkill) {
        setEditState({
            lessonId: l.id,
            skillId: l.skillId,
            title: l.title,
            sortOrder: String(l.sortOrder),
            difficultyLevel: String(l.difficultyLevel),
            xpReward: String(l.xpReward),
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
                sortOrder: parseInt(editState.sortOrder) || 1,
                difficultyLevel: parseInt(editState.difficultyLevel) || 1,
                xpReward: parseInt(editState.xpReward) || 50,
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
        if (sortKey !== col) return <span className="ml-1 text-outline-variant">↕</span>;
        return <span className="ml-1 text-on-surface-variant">{sortDir === "asc" ? "↑" : "↓"}</span>;
    }

    const sortedSkills = [...skills].sort((a, b) => a.sortOrder - b.sortOrder);

    return (
        <div className="max-w-5xl">
            <div className="flex items-center justify-between mb-6">
                <div>
                    <h1 className="text-xl font-semibold text-on-surface">Lessons</h1>
                    <p className="text-sm text-on-surface-variant mt-0.5">{lessons.length} total</p>
                </div>
                <button
                    onClick={() => { setShowAdd(v => !v); setAddError(""); }}
                    className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim transition-colors"
                >
                    {showAdd ? "Cancel" : "+ Add lesson"}
                </button>
            </div>

            {/* Add form */}
            {showAdd && (
                <div className="bg-surface-container-lowest rounded-2xl p-5 mb-5">
                    <h2 className="text-sm font-medium text-on-surface mb-4">New lesson</h2>
                    <div className="grid grid-cols-2 gap-3 mb-3">
                        <div className="col-span-2">
                            <label className="block text-xs text-on-surface-variant mb-1">Skill</label>
                            <select
                                className="w-full border-outline-variant rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={addForm.skillId}
                                onChange={e => setAddForm(f => ({ ...f, skillId: e.target.value }))}
                            >
                                <option value="">— choose skill —</option>
                                {sortedSkills.map(s => (
                                    <option key={s.id} value={s.id}>{s.iconName} · {s.title}</option>
                                ))}
                            </select>
                        </div>
                        <div className="col-span-2">
                            <label className="block text-xs text-on-surface-variant mb-1">Title</label>
                            <input
                                className="w-full border-outline-variant rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={addForm.title}
                                onChange={e => setAddForm(f => ({ ...f, title: e.target.value }))}
                                placeholder="Lesson title"
                            />
                        </div>
                        <div>
                            <label className="block text-xs text-on-surface-variant mb-1">Sort order</label>
                            <input
                                type="number" min={1}
                                className="w-full border-outline-variant rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={addForm.sortOrder}
                                onChange={e => setAddForm(f => ({ ...f, sortOrder: e.target.value }))}
                            />
                        </div>
                        <div>
                            <label className="block text-xs text-on-surface-variant mb-1">XP reward</label>
                            <input
                                type="number" min={0}
                                className="w-full border-outline-variant rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={addForm.xpReward}
                                onChange={e => setAddForm(f => ({ ...f, xpReward: e.target.value }))}
                            />
                        </div>
                        <div>
                            <label className="block text-xs text-on-surface-variant mb-1">Difficulty</label>
                            <select
                                className="w-full border-outline-variant rounded px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={addForm.difficultyLevel}
                                onChange={e => setAddForm(f => ({ ...f, difficultyLevel: e.target.value }))}
                            >
                                <option value="1">1 — Easy</option>
                                <option value="2">2 — Medium</option>
                                <option value="3">3 — Hard</option>
                            </select>
                        </div>
                    </div>
                    {addError && <p className="text-xs text-error mb-3">{addError}</p>}
                    <button
                        onClick={handleAdd}
                        disabled={createLesson.isPending}
                        className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-40 transition-colors"
                    >
                        {createLesson.isPending ? "Saving…" : "Save"}
                    </button>
                </div>
            )}

            {/* Filters */}
            <div className="bg-surface-container-lowest rounded-2xl p-4 mb-4 flex flex-wrap gap-3 items-end">
                <div>
                    <label className="block text-xs text-on-surface-variant mb-1">Search</label>
                    <input
                        className="border-outline-variant rounded px-3 py-1.5 text-sm w-52 focus:outline-none focus:ring-1 focus:ring-primary"
                        placeholder="Title or skill…"
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                    />
                </div>
                <div>
                    <label className="block text-xs text-on-surface-variant mb-1">Skill</label>
                    <select
                        className="border-outline-variant rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                        value={filterSkillId}
                        onChange={e => setFilterSkillId(e.target.value)}
                    >
                        <option value="">All skills</option>
                        {sortedSkills.map(s => (
                            <option key={s.id} value={s.id}>{s.iconName} · {s.title}</option>
                        ))}
                    </select>
                </div>
                <div>
                    <label className="block text-xs text-on-surface-variant mb-1">Difficulty</label>
                    <div className="flex gap-1">
                        {([0, 1, 2, 3] as const).map(d => (
                            <button
                                key={d}
                                onClick={() => setFilterDifficulty(d)}
                                className={`px-3 py-1.5 text-xs rounded border transition-colors ${
                                    filterDifficulty === d
                                        ? "bg-primary text-on-primary border-primary"
                                        : "border-outline-variant text-on-surface-variant hover:bg-surface-container-low"
                                }`}
                            >
                                {d === 0 ? "All" : DIFFICULTY_LABELS[d]}
                            </button>
                        ))}
                    </div>
                </div>
                {(filterSkillId || filterDifficulty || search) && (
                    <button
                        onClick={() => { setFilterSkillId(""); setFilterDifficulty(0); setSearch(""); }}
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
                <div className="bg-surface-container-lowest rounded-2xl overflow-hidden">
                    <table className="w-full text-sm border-collapse">
                        <thead>
                            <tr className="border-b border-outline-variant bg-surface-container-low">
                                {([
                                    ["skillTitle", "Skill"],
                                    ["title", "Title"],
                                    ["sortOrder", "#"],
                                    ["difficultyLevel", "Difficulty"],
                                    ["xpReward", "XP"],
                                ] as [SortKey, string][]).map(([key, label]) => (
                                    <th
                                        key={key}
                                        className="text-left py-2.5 px-4 text-xs font-medium text-on-surface-variant cursor-pointer hover:text-on-surface select-none"
                                        onClick={() => toggleSort(key)}
                                    >
                                        {label}<SortIcon col={key} />
                                    </th>
                                ))}
                                <th className="py-2.5 px-4 text-xs font-medium text-on-surface-variant text-right">Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {filtered.map(lesson => (
                                <tr key={lesson.id} className="border-b border-surface-container hover:bg-surface-container-low">
                                    {editState?.lessonId === lesson.id ? (
                                        <>
                                            <td className="py-2 px-4">
                                                <span className="text-xs text-on-surface-variant font-mono">{lesson.skillIcon}</span>
                                                <span className="text-xs text-on-surface-variant ml-1">· {lesson.skillTitle}</span>
                                            </td>
                                            <td className="py-2 px-4">
                                                <input
                                                    className="border-outline-variant rounded px-2 py-1 text-xs w-full focus:outline-none focus:ring-1 focus:ring-primary"
                                                    value={editState.title}
                                                    onChange={e => setEditState(s => s && ({ ...s, title: e.target.value }))}
                                                />
                                            </td>
                                            <td className="py-2 px-4">
                                                <input
                                                    type="number" min={1}
                                                    className="border-outline-variant rounded px-2 py-1 text-xs w-16 focus:outline-none focus:ring-1 focus:ring-primary"
                                                    value={editState.sortOrder}
                                                    onChange={e => setEditState(s => s && ({ ...s, sortOrder: e.target.value }))}
                                                />
                                            </td>
                                            <td className="py-2 px-4">
                                                <select
                                                    className="border-outline-variant rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary"
                                                    value={editState.difficultyLevel}
                                                    onChange={e => setEditState(s => s && ({ ...s, difficultyLevel: e.target.value }))}
                                                >
                                                    <option value="1">1 — Easy</option>
                                                    <option value="2">2 — Medium</option>
                                                    <option value="3">3 — Hard</option>
                                                </select>
                                            </td>
                                            <td className="py-2 px-4">
                                                <input
                                                    type="number" min={0}
                                                    className="border-outline-variant rounded px-2 py-1 text-xs w-16 focus:outline-none focus:ring-1 focus:ring-primary"
                                                    value={editState.xpReward}
                                                    onChange={e => setEditState(s => s && ({ ...s, xpReward: e.target.value }))}
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
                                            <td className="py-2.5 px-4">
                                                <span className="text-xs font-mono text-on-surface-variant">{lesson.skillIcon}</span>
                                                <span className="text-xs text-on-surface-variant ml-1">· {lesson.skillTitle}</span>
                                            </td>
                                            <td className="py-2.5 px-4 text-on-surface">{lesson.title}</td>
                                            <td className="py-2.5 px-4 text-on-surface-variant">{lesson.sortOrder}</td>
                                            <td className="py-2.5 px-4">
                                                <span className={`text-xs px-2 py-0.5 rounded border font-medium ${difficultyColor(lesson.difficultyLevel)}`}>
                                                    {DIFFICULTY_LABELS[lesson.difficultyLevel] ?? lesson.difficultyLevel}
                                                </span>
                                            </td>
                                            <td className="py-2.5 px-4 text-on-surface-variant">{lesson.xpReward}</td>
                                            <td className="py-2.5 px-4 text-right">
                                                <div className="flex items-center justify-end gap-3">
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
                            Skill: {deleteConfirm.skillIcon} · {deleteConfirm.skillTitle}.
                            All exercises in this lesson will also be deleted.
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
