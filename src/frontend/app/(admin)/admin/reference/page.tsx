"use client";

import { useState, useDeferredValue } from "react";
import {
    useAdminReferenceAll,
    useAdminReferenceCategories,
    useAdminSkills,
    useDeleteReferenceMaterial,
    useUpdateReferenceMaterial,
    useCreateReferenceForSkill,
    type AdminReferenceMaterial,
    type CreateReferenceMaterialBody,
} from "@/lib/hooks/useAdmin";

const EMPTY_FORM: CreateReferenceMaterialBody = {
    title: "",
    markdownContent: "",
    sortOrder: 0,
    category: null,
    tags: null,
};

export default function AdminReferenceAllPage() {
    const [selectedSkillId, setSelectedSkillId] = useState("");
    const [selectedCategory, setSelectedCategory] = useState("");
    const [rawSearch, setRawSearch] = useState("");
    const search = useDeferredValue(rawSearch);

    const [createSkillId, setCreateSkillId] = useState("");
    const [showCreateForm, setShowCreateForm] = useState(false);
    const [createForm, setCreateForm] = useState<CreateReferenceMaterialBody>(EMPTY_FORM);

    const [editingId, setEditingId] = useState<string | null>(null);
    const [editForm, setEditForm] = useState<CreateReferenceMaterialBody>(EMPTY_FORM);

    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    const { data: materials = [], isLoading } = useAdminReferenceAll({
        skillId: selectedSkillId || undefined,
        category: selectedCategory || undefined,
        search: search || undefined,
    });
    const { data: categories = [] } = useAdminReferenceCategories();
    const { data: skills = [] } = useAdminSkills();

    const deleteMaterial = useDeleteReferenceMaterial();
    const updateMaterial = useUpdateReferenceMaterial(editingId ?? "");
    const createMaterial = useCreateReferenceForSkill(createSkillId);

    function startEdit(material: AdminReferenceMaterial) {
        setEditingId(material.id);
        setEditForm({
            title: material.title,
            markdownContent: material.markdownContent,
            sortOrder: material.sortOrder,
            category: material.category,
            tags: material.tags.join(", "),
        });
    }

    async function handleSave() {
        await updateMaterial.mutateAsync(editForm);
        setEditingId(null);
    }

    async function handleCreate() {
        if (!createSkillId) return;
        await createMaterial.mutateAsync(createForm);
        setCreateForm(EMPTY_FORM);
        setShowCreateForm(false);
        setCreateSkillId("");
    }

    return (
        <div>
            <div className="flex items-center justify-between mb-6">
                <h1 className="text-xl font-semibold text-gray-900">Reference Materials</h1>
                <button
                    onClick={() => setShowCreateForm((v) => !v)}
                    className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 transition-colors"
                >
                    {showCreateForm ? "Cancel" : "+ New material"}
                </button>
            </div>

            {showCreateForm && (
                <div className="bg-white border border-gray-200 rounded-lg p-5 mb-6 space-y-4">
                    <h2 className="text-sm font-semibold text-gray-700">Create material</h2>
                    <label className="block">
                        <span className="text-xs text-gray-500">Skill *</span>
                        <select
                            className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                            value={createSkillId}
                            onChange={(e) => setCreateSkillId(e.target.value)}
                        >
                            <option value="">— select skill —</option>
                            {skills.map((s) => (
                                <option key={s.id} value={s.id}>{s.title}</option>
                            ))}
                        </select>
                    </label>
                    <ReferenceFormFields form={createForm} onChange={setCreateForm} />
                    <button
                        onClick={handleCreate}
                        disabled={createMaterial.isPending || !createSkillId || !createForm.title}
                        className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                    >
                        {createMaterial.isPending ? "Saving..." : "Create"}
                    </button>
                </div>
            )}

            {/* Filters */}
            <div className="flex flex-wrap gap-3 mb-5">
                <input
                    type="search"
                    placeholder="Search title or content..."
                    value={rawSearch}
                    onChange={(e) => setRawSearch(e.target.value)}
                    className="border border-gray-300 rounded-md px-3 py-1.5 text-sm w-56 focus:outline-none focus:ring-1 focus:ring-gray-400"
                />
                <select
                    value={selectedSkillId}
                    onChange={(e) => setSelectedSkillId(e.target.value)}
                    className="border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                >
                    <option value="">All skills</option>
                    {skills.map((s) => (
                        <option key={s.id} value={s.id}>{s.title}</option>
                    ))}
                </select>
                <select
                    value={selectedCategory}
                    onChange={(e) => setSelectedCategory(e.target.value)}
                    className="border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                >
                    <option value="">All categories</option>
                    {categories.map((c) => (
                        <option key={c} value={c}>{c}</option>
                    ))}
                </select>
            </div>

            {isLoading ? (
                <p className="text-sm text-gray-400">Loading...</p>
            ) : materials.length === 0 ? (
                <p className="text-sm text-gray-400">No reference materials found.</p>
            ) : (
                <div className="space-y-4">
                    {materials.map((material) => (
                        <div key={material.id} className="bg-white border border-gray-200 rounded-lg p-5">
                            {editingId === material.id ? (
                                <div className="space-y-4">
                                    <ReferenceFormFields form={editForm} onChange={setEditForm} />
                                    <div className="flex gap-3">
                                        <button
                                            onClick={handleSave}
                                            disabled={updateMaterial.isPending}
                                            className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                                        >
                                            {updateMaterial.isPending ? "Saving..." : "Save"}
                                        </button>
                                        <button
                                            onClick={() => setEditingId(null)}
                                            className="px-4 py-2 text-sm text-gray-500 hover:text-gray-700 transition-colors"
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div>
                                    <div className="flex items-start justify-between mb-2">
                                        <div>
                                            <h3 className="font-medium text-gray-800">{material.title}</h3>
                                            <div className="flex flex-wrap gap-2 mt-1">
                                                <span className="text-xs bg-gray-100 text-gray-500 rounded px-2 py-0.5">
                                                    {material.skillTitle}
                                                </span>
                                                {material.category && (
                                                    <span className="text-xs bg-blue-50 text-blue-600 rounded px-2 py-0.5">
                                                        {material.category}
                                                    </span>
                                                )}
                                                {material.tags.map((tag) => (
                                                    <span key={tag} className="text-xs bg-green-50 text-green-700 rounded px-2 py-0.5">
                                                        {tag}
                                                    </span>
                                                ))}
                                                <span className="text-xs text-gray-400">order: {material.sortOrder}</span>
                                            </div>
                                        </div>
                                        <div className="flex gap-3 shrink-0 ml-4">
                                            <button
                                                onClick={() => startEdit(material)}
                                                className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
                                            >
                                                Edit
                                            </button>
                                            {confirmDeleteId === material.id ? (
                                                <>
                                                    <button
                                                        onClick={() => {
                                                            deleteMaterial.mutate(material.id);
                                                            setConfirmDeleteId(null);
                                                        }}
                                                        className="text-sm text-red-600 hover:underline"
                                                    >
                                                        Confirm
                                                    </button>
                                                    <button
                                                        onClick={() => setConfirmDeleteId(null)}
                                                        className="text-sm text-gray-400 hover:underline"
                                                    >
                                                        Cancel
                                                    </button>
                                                </>
                                            ) : (
                                                <button
                                                    onClick={() => setConfirmDeleteId(material.id)}
                                                    className="text-sm text-gray-400 hover:text-red-500 transition-colors"
                                                >
                                                    Delete
                                                </button>
                                            )}
                                        </div>
                                    </div>
                                    <pre className="text-xs text-gray-500 font-mono whitespace-pre-wrap line-clamp-3 bg-gray-50 rounded p-3 mt-2">
                                        {material.markdownContent}
                                    </pre>
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

function ReferenceFormFields({
    form,
    onChange,
}: {
    form: CreateReferenceMaterialBody;
    onChange: (updated: CreateReferenceMaterialBody) => void;
}) {
    return (
        <>
            <label className="block">
                <span className="text-xs text-gray-500">Title *</span>
                <input
                    className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                    value={form.title}
                    onChange={(e) => onChange({ ...form, title: e.target.value })}
                />
            </label>
            <div className="flex gap-3">
                <label className="block flex-1">
                    <span className="text-xs text-gray-500">Category</span>
                    <input
                        className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                        placeholder="e.g. objections"
                        value={form.category ?? ""}
                        onChange={(e) => onChange({ ...form, category: e.target.value || null })}
                    />
                </label>
                <label className="block flex-1">
                    <span className="text-xs text-gray-500">Tags (comma-separated)</span>
                    <input
                        className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                        placeholder="e.g. rapport,discovery"
                        value={form.tags ?? ""}
                        onChange={(e) => onChange({ ...form, tags: e.target.value || null })}
                    />
                </label>
                <label className="block w-24">
                    <span className="text-xs text-gray-500">Sort order</span>
                    <input
                        type="number"
                        className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                        value={form.sortOrder}
                        onChange={(e) => onChange({ ...form, sortOrder: Number(e.target.value) })}
                    />
                </label>
            </div>
            <label className="block">
                <span className="text-xs text-gray-500">Content (Markdown)</span>
                <textarea
                    rows={10}
                    className="mt-1 w-full border border-gray-300 rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-gray-400"
                    value={form.markdownContent}
                    onChange={(e) => onChange({ ...form, markdownContent: e.target.value })}
                />
            </label>
        </>
    );
}
