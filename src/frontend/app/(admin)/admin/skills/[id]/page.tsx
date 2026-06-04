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
import { SKILL_STAGES, getStageMeta } from "@/lib/skillStages";

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
            stage: skill.stage,
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
        return <p className="text-sm text-ink-3">Loading skill...</p>;
    }

    return (
        <div>
            <div className="mb-6">
                <Link
                    href="/admin/skills"
                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    ← All skills
                </Link>
            </div>

            {/* Skill card */}
            <div className="bg-surface rounded-2xl p-5 mb-8">
                <div className="flex items-center justify-between mb-4">
                    <h1 className="text-lg font-semibold text-ink">{skill.title}</h1>
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
                        <div className="grid grid-cols-2 gap-4">
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
                                <span className="text-xs text-ink-3">Order in tree</span>
                                <input
                                    type="number"
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={form.orderInTree}
                                    onChange={(e) =>
                                        setForm({ ...form, orderInTree: Number(e.target.value) })
                                    }
                                />
                            </label>
                            <label className="block">
                                <span className="text-xs text-ink-3">Stage</span>
                                <select
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                                    value={form.stage}
                                    onChange={(e) => setForm({ ...form, stage: e.target.value })}
                                >
                                    {SKILL_STAGES.map((s) => (
                                        <option key={s.key} value={s.key}>
                                            {s.label}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            <label className="block col-span-2">
                                <span className="text-xs text-ink-3">Description</span>
                                <textarea
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
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
                                className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                            >
                                {updateSkill.isPending ? "Saving..." : "Save"}
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
                    <dl className="grid grid-cols-2 gap-3 text-sm">
                        <div>
                            <dt className="text-xs text-ink-3">Iconic Name</dt>
                            <dd className="text-ink font-mono text-xs">{skill.iconicName}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-ink-3">Order</dt>
                            <dd className="text-ink">{skill.orderInTree}</dd>
                        </div>
                        <div>
                            <dt className="text-xs text-ink-3">Stage</dt>
                            <dd className="text-ink">{getStageMeta(skill.stage).label}</dd>
                        </div>
                        <div className="col-span-2">
                            <dt className="text-xs text-ink-3">Description</dt>
                            <dd className="text-ink">{skill.description || "—"}</dd>
                        </div>
                    </dl>
                )}
            </div>

            {/* Topics */}
            <div className="flex items-center justify-between mb-4">
                <h2 className="text-base font-semibold text-ink">Topics</h2>
                <div className="flex items-center gap-3">
                    <Link
                        href={`/admin/skills/${id}/reference`}
                        className="text-sm text-ink-3 hover:text-ink transition-colors"
                    >
                        Reference materials →
                    </Link>
                    <button
                        onClick={() => setShowTopicForm((v) => !v)}
                        className="px-3 py-1.5 text-sm bg-ink text-bg rounded-md hover:opacity-90 transition-colors"
                    >
                        {showTopicForm ? "Cancel" : "+ New topic"}
                    </button>
                </div>
            </div>

            {showTopicForm && (
                <div className="bg-surface rounded-2xl p-5 mb-4">
                    <div className="grid grid-cols-3 gap-4">
                        <label className="block">
                            <span className="text-xs text-ink-3">Iconic Name (English ID)</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={topicForm.iconicName}
                                onChange={(e) =>
                                    setTopicForm({ ...topicForm, iconicName: e.target.value })
                                }
                                placeholder="e.g. introduction-to-sales"
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Title</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={topicForm.title}
                                onChange={(e) =>
                                    setTopicForm({ ...topicForm, title: e.target.value })
                                }
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Order in skill</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
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
                        className="mt-4 px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                    >
                        {createTopic.isPending ? "Saving..." : "Create topic"}
                    </button>
                </div>
            )}

            {topicsLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : topics.length === 0 ? (
                <p className="text-sm text-ink-3">No topics yet.</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Iconic Name
                            </th>
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
                        {topics.map((topic) => (
                            <tr
                                key={topic.id}
                                className="border-b border-line hover:bg-bg-2"
                            >
                                <td className="py-2.5 px-3 font-mono text-xs text-ink">
                                    <Link
                                        href={`/admin/skills/${id}/topics/${topic.id}`}
                                        className="hover:underline"
                                    >
                                        {topic.iconicName}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 font-medium text-ink">
                                    {topic.title}
                                </td>
                                <td className="py-2.5 px-3 text-ink-3">
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
                                                className="text-xs text-bad hover:underline"
                                            >
                                                Confirm
                                            </button>
                                            <button
                                                onClick={() => setConfirmDeleteTopicId(null)}
                                                className="text-xs text-ink-3 hover:underline"
                                            >
                                                Cancel
                                            </button>
                                        </span>
                                    ) : (
                                        <button
                                            onClick={() => setConfirmDeleteTopicId(topic.id)}
                                            className="text-xs text-ink-3 hover:text-bad transition-colors"
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
