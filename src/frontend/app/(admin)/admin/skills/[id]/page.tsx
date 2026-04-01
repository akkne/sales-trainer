"use client";

import { useState } from "react";
import Link from "next/link";
import { use } from "react";
import {
    useAdminSkills,
    useUpdateSkill,
    useAdminLessons,
    useCreateLesson,
    useDeleteLesson,
    type AdminSkill,
} from "@/lib/hooks/useAdmin";

const SALES_TYPES = ["b2b_saas", "retail", "real_estate", "finance", "b2c"];

export default function AdminSkillDetailPage({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = use(params);

    const { data: skills = [] } = useAdminSkills();
    const skill = skills.find((s) => s.id === id);

    const updateSkill = useUpdateSkill(id);
    const [editMode, setEditMode] = useState(false);
    const [form, setForm] = useState<Omit<AdminSkill, "id"> | null>(null);

    const { data: lessons = [], isLoading: lessonsLoading } = useAdminLessons(id);
    const createLesson = useCreateLesson(id);
    const deleteLesson = useDeleteLesson(id);

    const [showLessonForm, setShowLessonForm] = useState(false);
    const [lessonForm, setLessonForm] = useState({
        title: "",
        sortOrder: 0,
        difficultyLevel: 1,
        xpReward: 10,
    });
    const [confirmDeleteLessonId, setConfirmDeleteLessonId] = useState<string | null>(
        null
    );

    function startEdit() {
        if (!skill) return;
        setForm({
            title: skill.title,
            slug: skill.slug,
            iconName: skill.iconName,
            sortOrder: skill.sortOrder,
            prerequisiteSkillId: skill.prerequisiteSkillId,
            applicableSalesTypes: [...skill.applicableSalesTypes],
        });
        setEditMode(true);
    }

    async function handleSaveSkill() {
        if (!form) return;
        await updateSkill.mutateAsync(form);
        setEditMode(false);
    }

    function toggleSalesType(type: string) {
        if (!form) return;
        setForm({
            ...form,
            applicableSalesTypes: form.applicableSalesTypes.includes(type)
                ? form.applicableSalesTypes.filter((t) => t !== type)
                : [...form.applicableSalesTypes, type],
        });
    }

    async function handleCreateLesson() {
        await createLesson.mutateAsync(lessonForm);
        setLessonForm({ title: "", sortOrder: 0, difficultyLevel: 1, xpReward: 10 });
        setShowLessonForm(false);
    }

    if (!skill) {
        return <p className="text-sm text-gray-400">Loading skill...</p>;
    }

    return (
        <div>
            <div className="mb-6">
                <Link
                    href="/admin/skills"
                    className="text-xs text-gray-400 hover:text-gray-600 transition-colors"
                >
                    ← All skills
                </Link>
            </div>

            {/* Skill card */}
            <div className="bg-white border border-gray-200 rounded-lg p-5 mb-8">
                <div className="flex items-center justify-between mb-4">
                    <h1 className="text-lg font-semibold text-gray-900">{skill.title}</h1>
                    {!editMode && (
                        <button
                            onClick={startEdit}
                            className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
                        >
                            Edit
                        </button>
                    )}
                </div>

                {editMode && form ? (
                    <div>
                        <div className="grid grid-cols-2 gap-4">
                            <label className="block">
                                <span className="text-xs text-gray-500">Title</span>
                                <input
                                    className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                    value={form.title}
                                    onChange={(e) => setForm({ ...form, title: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-gray-500">Slug</span>
                                <input
                                    className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                    value={form.slug}
                                    onChange={(e) => setForm({ ...form, slug: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-gray-500">Icon name</span>
                                <input
                                    className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                    value={form.iconName}
                                    onChange={(e) => setForm({ ...form, iconName: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-gray-500">Sort order</span>
                                <input
                                    type="number"
                                    className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                    value={form.sortOrder}
                                    onChange={(e) =>
                                        setForm({ ...form, sortOrder: Number(e.target.value) })
                                    }
                                />
                            </label>
                        </div>
                        <div className="mt-4">
                            <span className="text-xs text-gray-500 block mb-2">
                                Sales types
                            </span>
                            <div className="flex flex-wrap gap-2">
                                {SALES_TYPES.map((type) => (
                                    <button
                                        key={type}
                                        type="button"
                                        onClick={() => toggleSalesType(type)}
                                        className={`px-3 py-1 text-xs rounded-full border transition-colors ${
                                            form.applicableSalesTypes.includes(type)
                                                ? "bg-gray-900 text-white border-gray-900"
                                                : "bg-white text-gray-600 border-gray-300 hover:border-gray-500"
                                        }`}
                                    >
                                        {type}
                                    </button>
                                ))}
                            </div>
                        </div>
                        <div className="flex gap-3 mt-4">
                            <button
                                onClick={handleSaveSkill}
                                disabled={updateSkill.isPending}
                                className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                            >
                                {updateSkill.isPending ? "Saving..." : "Save"}
                            </button>
                            <button
                                onClick={() => setEditMode(false)}
                                className="px-4 py-2 text-sm text-gray-500 hover:text-gray-700 transition-colors"
                            >
                                Cancel
                            </button>
                        </div>
                    </div>
                ) : (
                    <dl className="grid grid-cols-3 gap-3 text-sm">
                        <div>
                            <dt className="text-xs text-gray-400">Slug</dt>
                            <dd className="text-gray-700">{skill.slug}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-gray-400">Icon</dt>
                            <dd className="text-gray-700">{skill.iconName}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-gray-400">Order</dt>
                            <dd className="text-gray-700">{skill.sortOrder}</dd>
                        </div>
                        <div className="col-span-3">
                            <dt className="text-xs text-gray-400">Sales types</dt>
                            <dd className="text-gray-700">
                                {skill.applicableSalesTypes.join(", ") || "—"}
                            </dd>
                        </div>
                    </dl>
                )}
            </div>

            {/* Lessons */}
            <div className="flex items-center justify-between mb-4">
                <h2 className="text-base font-semibold text-gray-800">Lessons</h2>
                <div className="flex items-center gap-3">
                    <Link
                        href={`/admin/skills/${id}/reference`}
                        className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
                    >
                        Reference materials →
                    </Link>
                    <button
                        onClick={() => setShowLessonForm((v) => !v)}
                        className="px-3 py-1.5 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 transition-colors"
                    >
                        {showLessonForm ? "Cancel" : "+ New lesson"}
                    </button>
                </div>
            </div>

            {showLessonForm && (
                <div className="bg-white border border-gray-200 rounded-lg p-5 mb-4">
                    <div className="grid grid-cols-2 gap-4">
                        <label className="block col-span-2">
                            <span className="text-xs text-gray-500">Title</span>
                            <input
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                value={lessonForm.title}
                                onChange={(e) =>
                                    setLessonForm({ ...lessonForm, title: e.target.value })
                                }
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-gray-500">Sort order</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
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
                            <span className="text-xs text-gray-500">Difficulty (1–3)</span>
                            <input
                                type="number"
                                min={1}
                                max={3}
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
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
                            <span className="text-xs text-gray-500">XP reward</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
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
                    <button
                        onClick={handleCreateLesson}
                        disabled={createLesson.isPending || !lessonForm.title}
                        className="mt-4 px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                    >
                        {createLesson.isPending ? "Saving..." : "Create lesson"}
                    </button>
                </div>
            )}

            {lessonsLoading ? (
                <p className="text-sm text-gray-400">Loading...</p>
            ) : lessons.length === 0 ? (
                <p className="text-sm text-gray-400">No lessons yet.</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-gray-200">
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Title
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Order
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Difficulty
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                XP
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {lessons.map((lesson) => (
                            <tr
                                key={lesson.id}
                                className="border-b border-gray-100 hover:bg-gray-50"
                            >
                                <td className="py-2.5 px-3 font-medium text-gray-800">
                                    <Link
                                        href={`/admin/skills/${id}/lessons/${lesson.id}`}
                                        className="hover:underline"
                                    >
                                        {lesson.title}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 text-gray-500">
                                    {lesson.sortOrder}
                                </td>
                                <td className="py-2.5 px-3 text-gray-500">
                                    {lesson.difficultyLevel}
                                </td>
                                <td className="py-2.5 px-3 text-gray-500">{lesson.xpReward}</td>
                                <td className="py-2.5 px-3 text-right">
                                    {confirmDeleteLessonId === lesson.id ? (
                                        <span className="inline-flex gap-2">
                                            <button
                                                onClick={() => {
                                                    deleteLesson.mutate(lesson.id);
                                                    setConfirmDeleteLessonId(null);
                                                }}
                                                className="text-xs text-red-600 hover:underline"
                                            >
                                                Confirm
                                            </button>
                                            <button
                                                onClick={() => setConfirmDeleteLessonId(null)}
                                                className="text-xs text-gray-400 hover:underline"
                                            >
                                                Cancel
                                            </button>
                                        </span>
                                    ) : (
                                        <button
                                            onClick={() => setConfirmDeleteLessonId(lesson.id)}
                                            className="text-xs text-gray-400 hover:text-red-500 transition-colors"
                                        >
                                            Delete
                                        </button>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}
