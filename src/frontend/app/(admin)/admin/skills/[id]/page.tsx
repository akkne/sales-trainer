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
        return <p className="text-sm text-on-surface-variant">Loading skill...</p>;
    }

    return (
        <div>
            <div className="mb-6">
                <Link
                    href="/admin/skills"
                    className="text-xs text-on-surface-variant hover:text-on-surface transition-colors"
                >
                    ← All skills
                </Link>
            </div>

            {/* Skill card */}
            <div className="bg-surface-container-lowest rounded-2xl p-5 mb-8">
                <div className="flex items-center justify-between mb-4">
                    <h1 className="text-lg font-semibold text-on-surface">{skill.title}</h1>
                    {!editMode && (
                        <button
                            onClick={startEdit}
                            className="text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                        >
                            Edit
                        </button>
                    )}
                </div>

                {editMode && form ? (
                    <div>
                        <div className="grid grid-cols-2 gap-4">
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">Title</span>
                                <input
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={form.title}
                                    onChange={(e) => setForm({ ...form, title: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">Slug</span>
                                <input
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={form.slug}
                                    onChange={(e) => setForm({ ...form, slug: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">Icon name</span>
                                <input
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={form.iconName}
                                    onChange={(e) => setForm({ ...form, iconName: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">Sort order</span>
                                <input
                                    type="number"
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={form.sortOrder}
                                    onChange={(e) =>
                                        setForm({ ...form, sortOrder: Number(e.target.value) })
                                    }
                                />
                            </label>
                        </div>
                        <div className="mt-4">
                            <span className="text-xs text-on-surface-variant block mb-2">
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
                                                ? "bg-primary text-on-primary border-primary"
                                                : "bg-surface-container-lowest text-on-surface-variant border-outline-variant hover:border-outline"
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
                                className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                            >
                                {updateSkill.isPending ? "Saving..." : "Save"}
                            </button>
                            <button
                                onClick={() => setEditMode(false)}
                                className="px-4 py-2 text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                            >
                                Cancel
                            </button>
                        </div>
                    </div>
                ) : (
                    <dl className="grid grid-cols-3 gap-3 text-sm">
                        <div>
                            <dt className="text-xs text-on-surface-variant">Slug</dt>
                            <dd className="text-on-surface">{skill.slug}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-on-surface-variant">Icon</dt>
                            <dd className="text-on-surface">{skill.iconName}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-on-surface-variant">Order</dt>
                            <dd className="text-on-surface">{skill.sortOrder}</dd>
                        </div>
                        <div className="col-span-3">
                            <dt className="text-xs text-on-surface-variant">Sales types</dt>
                            <dd className="text-on-surface">
                                {skill.applicableSalesTypes.join(", ") || "—"}
                            </dd>
                        </div>
                    </dl>
                )}
            </div>

            {/* Lessons */}
            <div className="flex items-center justify-between mb-4">
                <h2 className="text-base font-semibold text-on-surface">Lessons</h2>
                <div className="flex items-center gap-3">
                    <Link
                        href={`/admin/skills/${id}/reference`}
                        className="text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                    >
                        Reference materials →
                    </Link>
                    <button
                        onClick={() => setShowLessonForm((v) => !v)}
                        className="px-3 py-1.5 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim transition-colors"
                    >
                        {showLessonForm ? "Cancel" : "+ New lesson"}
                    </button>
                </div>
            </div>

            {showLessonForm && (
                <div className="bg-surface-container-lowest rounded-2xl p-5 mb-4">
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
                            <span className="text-xs text-on-surface-variant">Difficulty (1–3)</span>
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
                    <button
                        onClick={handleCreateLesson}
                        disabled={createLesson.isPending || !lessonForm.title}
                        className="mt-4 px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                    >
                        {createLesson.isPending ? "Saving..." : "Create lesson"}
                    </button>
                </div>
            )}

            {lessonsLoading ? (
                <p className="text-sm text-on-surface-variant">Loading...</p>
            ) : lessons.length === 0 ? (
                <p className="text-sm text-on-surface-variant">No lessons yet.</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-outline-variant">
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Title
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Order
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Difficulty
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                XP
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {lessons.map((lesson) => (
                            <tr
                                key={lesson.id}
                                className="border-b border-surface-container hover:bg-surface-container-low"
                            >
                                <td className="py-2.5 px-3 font-medium text-on-surface">
                                    <Link
                                        href={`/admin/skills/${id}/lessons/${lesson.id}`}
                                        className="hover:underline"
                                    >
                                        {lesson.title}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 text-on-surface-variant">
                                    {lesson.sortOrder}
                                </td>
                                <td className="py-2.5 px-3 text-on-surface-variant">
                                    {lesson.difficultyLevel}
                                </td>
                                <td className="py-2.5 px-3 text-on-surface-variant">{lesson.xpReward}</td>
                                <td className="py-2.5 px-3 text-right">
                                    {confirmDeleteLessonId === lesson.id ? (
                                        <span className="inline-flex gap-2">
                                            <button
                                                onClick={() => {
                                                    deleteLesson.mutate(lesson.id);
                                                    setConfirmDeleteLessonId(null);
                                                }}
                                                className="text-xs text-error hover:underline"
                                            >
                                                Confirm
                                            </button>
                                            <button
                                                onClick={() => setConfirmDeleteLessonId(null)}
                                                className="text-xs text-on-surface-variant hover:underline"
                                            >
                                                Cancel
                                            </button>
                                        </span>
                                    ) : (
                                        <button
                                            onClick={() => setConfirmDeleteLessonId(lesson.id)}
                                            className="text-xs text-on-surface-variant hover:text-error transition-colors"
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
