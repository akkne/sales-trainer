"use client";

import { useState, useMemo } from "react";
import Link from "next/link";
import {
    useAdminSkills,
    useAdminAllTopics,
    useCreateTopic,
    useUpdateTopic,
    useDeleteTopic,
    useImportTopics,
    type AdminTopicWithSkill,
} from "@/features/admin/hooks/use-admin";
import { ImportPanel } from "@/features/admin/components/import-panel";
import { TOPICS_TEMPLATE } from "@/features/admin/lib/import-templates";

export default function AdminTopicsPage() {
    const { data: skills = [] } = useAdminSkills();
    const { data: topics = [], isLoading } = useAdminAllTopics();
    const importTopics = useImportTopics();

    const [filterSkillId, setFilterSkillId] = useState<string>("");
    const [searchQuery, setSearchQuery] = useState("");
    const [showForm, setShowForm] = useState(false);
    const [editingId, setEditingId] = useState<string | null>(null);
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    const [formSkillIconicName, setFormSkillIconicName] = useState<string>("");
    const [formIconicName, setFormIconicName] = useState("");
    const [formTitle, setFormTitle] = useState("");
    const [formOrder, setFormOrder] = useState(1);

    // For creating topics under any skill
    const createTopicForSkill = useCreateTopic(formSkillIconicName || skills[0]?.iconicName || "");

    // For updating topics - we need the skill context but update by topicId
    const updateTopic = useUpdateTopic(editingId || "");

    // For deleting - need to get the skill ID from the topic
    const topicBeingDeleted = topics.find(t => t.id === confirmDeleteId);
    const deleteTopic = useDeleteTopic(topicBeingDeleted?.skillId || "");

    // Filtered topics
    const filteredTopics = useMemo(() => {
        return topics.filter(topic => {
            const matchesSkill = !filterSkillId || topic.skillId === filterSkillId;
            const matchesSearch = !searchQuery ||
                topic.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
                topic.skillTitle.toLowerCase().includes(searchQuery.toLowerCase());
            return matchesSkill && matchesSearch;
        });
    }, [topics, filterSkillId, searchQuery]);

    // Group topics by skill for display
    const groupedTopics = useMemo(() => {
        const groups: Record<string, { skill: { id: string; iconicName: string; title: string }; topics: AdminTopicWithSkill[] }> = {};

        for (const topic of filteredTopics) {
            if (!groups[topic.skillId]) {
                groups[topic.skillId] = {
                    skill: { id: topic.skillId, iconicName: topic.skillIconicName, title: topic.skillTitle },
                    topics: []
                };
            }
            groups[topic.skillId].topics.push(topic);
        }

        // Sort topics within each group by orderInSkill
        for (const group of Object.values(groups)) {
            group.topics.sort((a, b) => a.orderInSkill - b.orderInSkill);
        }

        return Object.values(groups);
    }, [filteredTopics]);

    function resetForm() {
        setFormSkillIconicName(skills[0]?.iconicName || "");
        setFormIconicName("");
        setFormTitle("");
        setFormOrder(1);
    }

    async function handleCreate() {
        if (!formSkillIconicName) return;
        await createTopicForSkill.mutateAsync({
            iconicName: formIconicName,
            title: formTitle,
            orderInSkill: formOrder,
        });
        resetForm();
        setShowForm(false);
    }

    function startEdit(topic: AdminTopicWithSkill) {
        setEditingId(topic.id);
        setFormIconicName(topic.iconicName);
        setFormTitle(topic.title);
        setFormOrder(topic.orderInSkill);
    }

    async function handleUpdate() {
        if (!editingId) return;
        await updateTopic.mutateAsync({
            iconicName: formIconicName,
            title: formTitle,
            orderInSkill: formOrder,
        });
        setEditingId(null);
        setFormIconicName("");
        setFormTitle("");
        setFormOrder(1);
    }

    async function handleDelete(topicId: string) {
        await deleteTopic.mutateAsync(topicId);
        setConfirmDeleteId(null);
    }

    return (
        <div>
            <div className="flex flex-wrap gap-3 items-center justify-between mb-6">
                <h1 className="text-xl font-semibold text-ink">Topics</h1>
                <button
                    onClick={() => { setShowForm(v => !v); if (!showForm) resetForm(); }}
                    className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 transition-colors"
                >
                    {showForm ? "Cancel" : "+ New topic"}
                </button>
            </div>

            <ImportPanel
                title="Import Topics"
                description='JSON array: [{ skillIconicName, iconicName, title, orderInSkill }]'
                templateData={TOPICS_TEMPLATE}
                templateFilename="topics_template.json"
                onImport={async ({ text }) => {
                    const file = new File([text], "import.json", { type: "application/json" });
                    const result = await importTopics.mutateAsync(file);
                    return {
                        created: result.topicsCreated,
                        updated: result.topicsUpdated,
                        errors: result.errors,
                    };
                }}
            />

            {/* Create Form */}
            {showForm && (
                <div className="bg-surface rounded-2xl p-5 mb-6">
                    <h2 className="text-sm font-medium text-ink mb-4">New topic</h2>
                    <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
                        <label className="block">
                            <span className="text-xs text-ink-3">Skill</span>
                            <select
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={formSkillIconicName}
                                onChange={(e) => setFormSkillIconicName(e.target.value)}
                            >
                                <option value="">Select skill...</option>
                                {skills.map(skill => (
                                    <option key={skill.id} value={skill.iconicName}>{skill.title} ({skill.iconicName})</option>
                                ))}
                            </select>
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Iconic Name (English ID)</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={formIconicName}
                                onChange={(e) => setFormIconicName(e.target.value)}
                                placeholder="e.g. introduction-to-sales"
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Title (Display Name)</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={formTitle}
                                onChange={(e) => setFormTitle(e.target.value)}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Order in skill</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={formOrder}
                                onChange={(e) => setFormOrder(Number(e.target.value))}
                            />
                        </label>
                    </div>
                    <button
                        onClick={handleCreate}
                        disabled={createTopicForSkill.isPending || !formIconicName || !formTitle || !formSkillIconicName}
                        className="mt-4 px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                    >
                        {createTopicForSkill.isPending ? "Saving..." : "Create"}
                    </button>
                    {createTopicForSkill.isError && (
                        <p className="mt-2 text-xs text-bad">
                            {(createTopicForSkill.error as Error).message}
                        </p>
                    )}
                </div>
            )}

            {/* Filters */}
            <div className="flex flex-wrap gap-4 mb-4">
                <label className="block flex-1 min-w-[160px] max-w-xs">
                    <span className="text-xs text-ink-3">Filter by skill</span>
                    <select
                        className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        value={filterSkillId}
                        onChange={(e) => setFilterSkillId(e.target.value)}
                    >
                        <option value="">All skills</option>
                        {skills.map(skill => (
                            <option key={skill.id} value={skill.id}>{skill.title}</option>
                        ))}
                    </select>
                </label>
                <label className="block flex-1 min-w-[160px] max-w-xs">
                    <span className="text-xs text-ink-3">Search</span>
                    <input
                        className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        placeholder="Search topics..."
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                    />
                </label>
            </div>

            {/* Topics List */}
            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : filteredTopics.length === 0 ? (
                <p className="text-sm text-ink-3">No topics found.</p>
            ) : (
                <div className="space-y-6">
                    {groupedTopics.map(group => (
                        <div key={group.skill.id}>
                            <h3 className="text-sm font-medium text-ink-3 mb-2 flex items-center gap-2">
                                <span className="px-2 py-0.5 bg-indigo-soft text-indigo rounded text-xs">
                                    {group.skill.title}
                                </span>
                                <span className="text-xs text-ink-3">
                                    {group.topics.length} topic{group.topics.length !== 1 ? 's' : ''}
                                </span>
                            </h3>
                            <div className="overflow-x-auto -mx-4 px-4">
                            <table className="w-full text-sm border-collapse min-w-[400px]">
                                <thead>
                                    <tr className="border-b border-line">
                                        <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                            Iconic Name
                                        </th>
                                        <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                            Title
                                        </th>
                                        <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium w-24">
                                            Order
                                        </th>
                                        <th className="py-2 px-3 w-40" />
                                    </tr>
                                </thead>
                                <tbody>
                                    {group.topics.map(topic => (
                                        <tr
                                            key={topic.id}
                                            className="border-b border-line hover:bg-bg-2"
                                        >
                                            {editingId === topic.id ? (
                                                <>
                                                    <td className="py-2.5 px-3">
                                                        <input
                                                            className="w-full border border-line rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                            value={formIconicName}
                                                            onChange={(e) => setFormIconicName(e.target.value)}
                                                        />
                                                    </td>
                                                    <td className="py-2.5 px-3">
                                                        <input
                                                            className="w-full border border-line rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                            value={formTitle}
                                                            onChange={(e) => setFormTitle(e.target.value)}
                                                        />
                                                    </td>
                                                    <td className="py-2.5 px-3">
                                                        <input
                                                            type="number"
                                                            className="w-full border border-line rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                            value={formOrder}
                                                            onChange={(e) => setFormOrder(Number(e.target.value))}
                                                        />
                                                    </td>
                                                    <td className="py-2.5 px-3 text-right">
                                                        <span className="inline-flex gap-2">
                                                            <button
                                                                onClick={handleUpdate}
                                                                disabled={updateTopic.isPending}
                                                                className="text-xs text-indigo hover:underline"
                                                            >
                                                                {updateTopic.isPending ? "Saving..." : "Save"}
                                                            </button>
                                                            <button
                                                                onClick={() => setEditingId(null)}
                                                                className="text-xs text-ink-3 hover:underline"
                                                            >
                                                                Cancel
                                                            </button>
                                                        </span>
                                                    </td>
                                                </>
                                            ) : (
                                                <>
                                                    <td className="py-2.5 px-3 font-mono text-xs text-ink">
                                                        <Link
                                                            href={`/admin/skills/${topic.skillId}/topics/${topic.id}`}
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
                                                        {confirmDeleteId === topic.id ? (
                                                            <span className="inline-flex gap-2">
                                                                <button
                                                                    onClick={() => handleDelete(topic.id)}
                                                                    className="text-xs text-bad hover:underline"
                                                                >
                                                                    Confirm
                                                                </button>
                                                                <button
                                                                    onClick={() => setConfirmDeleteId(null)}
                                                                    className="text-xs text-ink-3 hover:underline"
                                                                >
                                                                    Cancel
                                                                </button>
                                                            </span>
                                                        ) : (
                                                            <span className="inline-flex gap-3">
                                                                <button
                                                                    onClick={() => startEdit(topic)}
                                                                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                                                                >
                                                                    Edit
                                                                </button>
                                                                <button
                                                                    onClick={() => setConfirmDeleteId(topic.id)}
                                                                    className="text-xs text-ink-3 hover:text-bad transition-colors"
                                                                >
                                                                    Delete
                                                                </button>
                                                            </span>
                                                        )}
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
        </div>
    );
}
