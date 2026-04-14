"use client";

import { useState } from "react";
import Link from "next/link";
import { use } from "react";
import {
    useAdminSkills,
    useUpdateSkill,
    useAdminTopics,
    useCreateTopic,
    useDeleteTopic,
    type AdminSkill,
} from "@/lib/hooks/useAdmin";

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

    const { data: topics = [], isLoading: topicsLoading } = useAdminTopics(skill?.iconicName || "");
    const createTopic = useCreateTopic(skill?.iconicName || "");
    const deleteTopic = useDeleteTopic(id);

    const [showTopicForm, setShowTopicForm] = useState(false);
    const [topicForm, setTopicForm] = useState({
        iconicName: "",
        title: "",
        orderInSkill: 0,
    });
    const [confirmDeleteTopicId, setConfirmDeleteTopicId] = useState<string | null>(null);

    function startEdit() {
        if (!skill) return;
        setForm({
            iconicName: skill.iconicName,
            title: skill.title,
            description: skill.description,
            orderInTree: skill.orderInTree,
        });
        setEditMode(true);
    }

    async function handleSaveSkill() {
        if (!form) return;
        await updateSkill.mutateAsync(form);
        setEditMode(false);
    }

    async function handleCreateTopic() {
        await createTopic.mutateAsync(topicForm);
        setTopicForm({ iconicName: "", title: "", orderInSkill: 0 });
        setShowTopicForm(false);
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
                                <span className="text-xs text-on-surface-variant">Iconic Name (English ID)</span>
                                <input
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={form.iconicName}
                                    onChange={(e) => setForm({ ...form, iconicName: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">Title</span>
                                <input
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={form.title}
                                    onChange={(e) => setForm({ ...form, title: e.target.value })}
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-on-surface-variant">Order in tree</span>
                                <input
                                    type="number"
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    value={form.orderInTree}
                                    onChange={(e) =>
                                        setForm({ ...form, orderInTree: Number(e.target.value) })
                                    }
                                />
                            </label>
                            <div />
                            <label className="block col-span-2">
                                <span className="text-xs text-on-surface-variant">Description</span>
                                <textarea
                                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                    rows={2}
                                    value={form.description || ""}
                                    onChange={(e) => setForm({ ...form, description: e.target.value || null })}
                                />
                            </label>
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
                    <dl className="grid grid-cols-2 gap-3 text-sm">
                        <div>
                            <dt className="text-xs text-on-surface-variant">Iconic Name</dt>
                            <dd className="text-on-surface font-mono text-xs">{skill.iconicName}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-on-surface-variant">Order</dt>
                            <dd className="text-on-surface">{skill.orderInTree}</dd>
                        </div>
                        <div className="col-span-2">
                            <dt className="text-xs text-on-surface-variant">Description</dt>
                            <dd className="text-on-surface">{skill.description || "—"}</dd>
                        </div>
                    </dl>
                )}
            </div>

            {/* Topics */}
            <div className="flex items-center justify-between mb-4">
                <h2 className="text-base font-semibold text-on-surface">Topics</h2>
                <div className="flex items-center gap-3">
                    <Link
                        href={`/admin/skills/${id}/reference`}
                        className="text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                    >
                        Reference materials →
                    </Link>
                    <button
                        onClick={() => setShowTopicForm((v) => !v)}
                        className="px-3 py-1.5 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim transition-colors"
                    >
                        {showTopicForm ? "Cancel" : "+ New topic"}
                    </button>
                </div>
            </div>

            {showTopicForm && (
                <div className="bg-surface-container-lowest rounded-2xl p-5 mb-4">
                    <div className="grid grid-cols-3 gap-4">
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Iconic Name (English ID)</span>
                            <input
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={topicForm.iconicName}
                                onChange={(e) =>
                                    setTopicForm({ ...topicForm, iconicName: e.target.value })
                                }
                                placeholder="e.g. introduction-to-sales"
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Title</span>
                            <input
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={topicForm.title}
                                onChange={(e) =>
                                    setTopicForm({ ...topicForm, title: e.target.value })
                                }
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Order in skill</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={topicForm.orderInSkill}
                                onChange={(e) =>
                                    setTopicForm({
                                        ...topicForm,
                                        orderInSkill: Number(e.target.value),
                                    })
                                }
                            />
                        </label>
                    </div>
                    <button
                        onClick={handleCreateTopic}
                        disabled={createTopic.isPending || !topicForm.iconicName || !topicForm.title}
                        className="mt-4 px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                    >
                        {createTopic.isPending ? "Saving..." : "Create topic"}
                    </button>
                </div>
            )}

            {topicsLoading ? (
                <p className="text-sm text-on-surface-variant">Loading...</p>
            ) : topics.length === 0 ? (
                <p className="text-sm text-on-surface-variant">No topics yet.</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-outline-variant">
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Iconic Name
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Title
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Order
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {topics.map((topic) => (
                            <tr
                                key={topic.id}
                                className="border-b border-surface-container hover:bg-surface-container-low"
                            >
                                <td className="py-2.5 px-3 font-mono text-xs text-on-surface">
                                    <Link
                                        href={`/admin/skills/${id}/topics/${topic.id}`}
                                        className="hover:underline"
                                    >
                                        {topic.iconicName}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 font-medium text-on-surface">
                                    {topic.title}
                                </td>
                                <td className="py-2.5 px-3 text-on-surface-variant">
                                    {topic.orderInSkill}
                                </td>
                                <td className="py-2.5 px-3 text-right">
                                    {confirmDeleteTopicId === topic.id ? (
                                        <span className="inline-flex gap-2">
                                            <button
                                                onClick={() => {
                                                    deleteTopic.mutate(topic.id);
                                                    setConfirmDeleteTopicId(null);
                                                }}
                                                className="text-xs text-error hover:underline"
                                            >
                                                Confirm
                                            </button>
                                            <button
                                                onClick={() => setConfirmDeleteTopicId(null)}
                                                className="text-xs text-on-surface-variant hover:underline"
                                            >
                                                Cancel
                                            </button>
                                        </span>
                                    ) : (
                                        <button
                                            onClick={() => setConfirmDeleteTopicId(topic.id)}
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
