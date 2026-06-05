"use client";

import { useState, useDeferredValue } from "react";
import {
    useAdminReferenceAll,
    useAdminReferenceCategories,
    useAdminSkills,
    useDeleteReference,
    useUpdateReference,
    useCreateReference,
    type AdminReferenceMaterial,
    type CreateReferenceMaterialBody,
} from "@/features/admin/hooks/use-admin";

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

    const deleteMaterial = useDeleteReference();
    const updateMaterial = useUpdateReference(editingId ?? "");
    const createMaterial = useCreateReference(createSkillId);

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
                <h1 className="text-xl font-semibold text-on-surface">Reference Materials</h1>
                <button
                    onClick={() => setShowCreateForm((v) => !v)}
                    className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim transition-colors"
                >
                    {showCreateForm ? "Cancel" : "+ New material"}
                </button>
            </div>

            {showCreateForm && (
                <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5 mb-6 space-y-4">
                    <h2 className="text-sm font-semibold text-on-surface">Create material</h2>
                    <label className="block">
                        <span className="text-xs text-on-surface-variant">Skill *</span>
                        <select
                            className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
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
                        className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
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
                    className="border border-outline-variant rounded-md px-3 py-1.5 text-sm w-56 focus:outline-none focus:ring-1 focus:ring-primary"
                />
                <select
                    value={selectedSkillId}
                    onChange={(e) => setSelectedSkillId(e.target.value)}
                    className="border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                >
                    <option value="">All skills</option>
                    {skills.map((s) => (
                        <option key={s.id} value={s.id}>{s.title}</option>
                    ))}
                </select>
                <select
                    value={selectedCategory}
                    onChange={(e) => setSelectedCategory(e.target.value)}
                    className="border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                >
                    <option value="">All categories</option>
                    {categories.map((c) => (
                        <option key={c} value={c}>{c}</option>
                    ))}
                </select>
            </div>

            {isLoading ? (
                <p className="text-sm text-on-surface-variant">Loading...</p>
            ) : materials.length === 0 ? (
                <p className="text-sm text-on-surface-variant">No reference materials found.</p>
            ) : (
                <div className="space-y-4">
                    {materials.map((material) => (
                        <div key={material.id} className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
                            {editingId === material.id ? (
                                <div className="space-y-4">
                                    <ReferenceFormFields form={editForm} onChange={setEditForm} />
                                    <div className="flex gap-3">
                                        <button
                                            onClick={handleSave}
                                            disabled={updateMaterial.isPending}
                                            className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                                        >
                                            {updateMaterial.isPending ? "Saving..." : "Save"}
                                        </button>
                                        <button
                                            onClick={() => setEditingId(null)}
                                            className="px-4 py-2 text-sm text-on-surface-variant hover:text-on-surface transition-colors"
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div>
                                    <div className="flex items-start justify-between mb-2">
                                        <div>
                                            <h3 className="font-medium text-on-surface">{material.title}</h3>
                                            <div className="flex flex-wrap gap-2 mt-1">
                                                <span className="text-xs bg-surface-container text-on-surface-variant rounded px-2 py-0.5">
                                                    {material.skillTitle}
                                                </span>
                                                {material.category && (
                                                    <span className="text-xs bg-tertiary-container text-tertiary rounded px-2 py-0.5">
                                                        {material.category}
                                                    </span>
                                                )}
                                                {material.tags.map((tag) => (
                                                    <span key={tag} className="text-xs bg-primary-container text-primary rounded px-2 py-0.5">
                                                        {tag}
                                                    </span>
                                                ))}
                                                <span className="text-xs text-on-surface-variant">order: {material.sortOrder}</span>
                                            </div>
                                        </div>
                                        <div className="flex gap-3 shrink-0 ml-4">
                                            <button
                                                onClick={() => startEdit(material)}
                                                className="text-sm text-on-surface-variant hover:text-on-surface transition-colors"
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
                                                        className="text-sm text-error hover:underline"
                                                    >
                                                        Confirm
                                                    </button>
                                                    <button
                                                        onClick={() => setConfirmDeleteId(null)}
                                                        className="text-sm text-on-surface-variant hover:underline"
                                                    >
                                                        Cancel
                                                    </button>
                                                </>
                                            ) : (
                                                <button
                                                    onClick={() => setConfirmDeleteId(material.id)}
                                                    className="text-sm text-on-surface-variant hover:text-error transition-colors"
                                                >
                                                    Delete
                                                </button>
                                            )}
                                        </div>
                                    </div>
                                    <pre className="text-xs text-on-surface-variant font-mono whitespace-pre-wrap line-clamp-3 bg-surface-container-low rounded p-3 mt-2">
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
                <span className="text-xs text-on-surface-variant">Title *</span>
                <input
                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                    value={form.title}
                    onChange={(e) => onChange({ ...form, title: e.target.value })}
                />
            </label>
            <div className="flex gap-3">
                <label className="block flex-1">
                    <span className="text-xs text-on-surface-variant">Category</span>
                    <input
                        className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                        placeholder="e.g. objections"
                        value={form.category ?? ""}
                        onChange={(e) => onChange({ ...form, category: e.target.value || null })}
                    />
                </label>
                <label className="block flex-1">
                    <span className="text-xs text-on-surface-variant">Tags (comma-separated)</span>
                    <input
                        className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                        placeholder="e.g. rapport,discovery"
                        value={form.tags ?? ""}
                        onChange={(e) => onChange({ ...form, tags: e.target.value || null })}
                    />
                </label>
                <label className="block w-24">
                    <span className="text-xs text-on-surface-variant">Sort order</span>
                    <input
                        type="number"
                        className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                        value={form.sortOrder}
                        onChange={(e) => onChange({ ...form, sortOrder: Number(e.target.value) })}
                    />
                </label>
            </div>
            <label className="block">
                <span className="text-xs text-on-surface-variant">Content (Markdown)</span>
                <textarea
                    rows={10}
                    className="mt-1 w-full border border-outline-variant rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-primary"
                    value={form.markdownContent}
                    onChange={(e) => onChange({ ...form, markdownContent: e.target.value })}
                />
            </label>
        </>
    );
}
