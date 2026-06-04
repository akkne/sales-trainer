"use client";

import { useState } from "react";
import Link from "next/link";
import { use } from "react";
import {
    useAdminReference,
    useCreateReference,
    useUpdateReference,
    useDeleteReference,
    type AdminReferenceMaterial,
    type CreateReferenceMaterialBody,
} from "@/lib/hooks/useAdmin";

export default function AdminReferencePageWrapper({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id: skillId } = use(params);
    return <AdminReferencePage skillId={skillId} />;
}

function AdminReferencePage({ skillId }: { skillId: string }) {
    const { data: materials = [], isLoading } = useAdminReference(skillId);
    const createMaterial = useCreateReference(skillId);
    const deleteMaterial = useDeleteReference();

    const emptyForm: CreateReferenceMaterialBody = { title: "", markdownContent: "", sortOrder: 0, category: null, tags: null };
    const [showForm, setShowForm] = useState(false);
    const [form, setForm] = useState<CreateReferenceMaterialBody>(emptyForm);

    const [editId, setEditId] = useState<string | null>(null);
    const [editForm, setEditForm] = useState<CreateReferenceMaterialBody>(emptyForm);
    const updateMaterial = useUpdateReference(editId ?? "");

    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    async function handleCreate() {
        await createMaterial.mutateAsync(form);
        setForm(emptyForm);
        setShowForm(false);
    }

    function startEdit(m: AdminReferenceMaterial) {
        setEditId(m.id);
        setEditForm({
            title: m.title,
            markdownContent: m.markdownContent,
            sortOrder: m.sortOrder,
            category: m.category,
            tags: m.tags.join(", "),
        });
    }

    async function handleSave() {
        await updateMaterial.mutateAsync(editForm);
        setEditId(null);
    }

    return (
        <div>
            <div className="mb-6">
                <Link
                    href={`/admin/skills/${skillId}`}
                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    ← Back to skill
                </Link>
            </div>

            <div className="flex items-center justify-between mb-6">
                <h1 className="text-xl font-semibold text-ink">Reference materials</h1>
                <button
                    onClick={() => setShowForm((v) => !v)}
                    className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 transition-colors"
                >
                    {showForm ? "Cancel" : "+ New material"}
                </button>
            </div>

            {showForm && (
                <div className="bg-surface rounded-2xl border border-line p-5 mb-6">
                    <div className="space-y-4">
                        <label className="block">
                            <span className="text-xs text-ink-3">Title</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={form.title}
                                onChange={(e) => setForm({ ...form, title: e.target.value })}
                            />
                        </label>
                        <div className="flex gap-3">
                            <label className="block flex-1">
                                <span className="text-xs text-ink-3">Category</span>
                                <input
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    placeholder="e.g. objections"
                                    value={form.category ?? ""}
                                    onChange={(e) => setForm({ ...form, category: e.target.value || null })}
                                />
                            </label>
                            <label className="block flex-1">
                                <span className="text-xs text-ink-3">Tags (comma-separated)</span>
                                <input
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    placeholder="e.g. rapport,discovery"
                                    value={form.tags ?? ""}
                                    onChange={(e) => setForm({ ...form, tags: e.target.value || null })}
                                />
                            </label>
                            <label className="block w-28">
                                <span className="text-xs text-ink-3">Sort order</span>
                                <input
                                    type="number"
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={form.sortOrder}
                                    onChange={(e) =>
                                        setForm({ ...form, sortOrder: Number(e.target.value) })
                                    }
                                />
                            </label>
                        </div>
                        <label className="block">
                            <span className="text-xs text-ink-3">Content (Markdown)</span>
                            <textarea
                                rows={12}
                                className="mt-1 w-full border border-line rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={form.markdownContent}
                                onChange={(e) =>
                                    setForm({ ...form, markdownContent: e.target.value })
                                }
                            />
                        </label>
                    </div>
                    <button
                        onClick={handleCreate}
                        disabled={createMaterial.isPending || !form.title}
                        className="mt-4 px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                    >
                        {createMaterial.isPending ? "Saving..." : "Create"}
                    </button>
                </div>
            )}

            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : materials.length === 0 ? (
                <p className="text-sm text-ink-3">No reference materials yet.</p>
            ) : (
                <div className="space-y-4">
                    {materials.map((m) => (
                        <div
                            key={m.id}
                            className="bg-surface rounded-2xl border border-line p-5"
                        >
                            {editId === m.id ? (
                                <div className="space-y-4">
                                    <label className="block">
                                        <span className="text-xs text-ink-3">Title</span>
                                        <input
                                            className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                            value={editForm.title}
                                            onChange={(e) =>
                                                setEditForm({ ...editForm, title: e.target.value })
                                            }
                                        />
                                    </label>
                                    <div className="flex gap-3">
                                        <label className="block flex-1">
                                            <span className="text-xs text-ink-3">Category</span>
                                            <input
                                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                value={editForm.category ?? ""}
                                                onChange={(e) => setEditForm({ ...editForm, category: e.target.value || null })}
                                            />
                                        </label>
                                        <label className="block flex-1">
                                            <span className="text-xs text-ink-3">Tags (comma-separated)</span>
                                            <input
                                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                value={editForm.tags ?? ""}
                                                onChange={(e) => setEditForm({ ...editForm, tags: e.target.value || null })}
                                            />
                                        </label>
                                        <label className="block w-28">
                                            <span className="text-xs text-ink-3">Sort order</span>
                                            <input
                                                type="number"
                                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                value={editForm.sortOrder}
                                                onChange={(e) =>
                                                    setEditForm({
                                                        ...editForm,
                                                        sortOrder: Number(e.target.value),
                                                    })
                                                }
                                            />
                                        </label>
                                    </div>
                                    <label className="block">
                                        <span className="text-xs text-ink-3">
                                            Content (Markdown)
                                        </span>
                                        <textarea
                                            rows={12}
                                            className="mt-1 w-full border border-line rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                            value={editForm.markdownContent}
                                            onChange={(e) =>
                                                setEditForm({
                                                    ...editForm,
                                                    markdownContent: e.target.value,
                                                })
                                            }
                                        />
                                    </label>
                                    <div className="flex gap-3">
                                        <button
                                            onClick={handleSave}
                                            disabled={updateMaterial.isPending}
                                            className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                                        >
                                            {updateMaterial.isPending ? "Saving..." : "Save"}
                                        </button>
                                        <button
                                            onClick={() => setEditId(null)}
                                            className="px-4 py-2 text-sm text-ink-3 hover:text-ink transition-colors"
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div>
                                    <div className="flex items-center justify-between mb-3">
                                        <div>
                                            <h3 className="font-medium text-ink">
                                                {m.title}
                                            </h3>
                                            <span className="text-xs text-ink-3">
                                                order: {m.sortOrder}
                                            </span>
                                        </div>
                                        <div className="flex gap-3">
                                            <button
                                                onClick={() => startEdit(m)}
                                                className="text-sm text-ink-3 hover:text-ink transition-colors"
                                            >
                                                Edit
                                            </button>
                                            {confirmDeleteId === m.id ? (
                                                <>
                                                    <button
                                                        onClick={() => {
                                                            deleteMaterial.mutate(m.id);
                                                            setConfirmDeleteId(null);
                                                        }}
                                                        className="text-sm text-bad hover:underline"
                                                    >
                                                        Confirm
                                                    </button>
                                                    <button
                                                        onClick={() => setConfirmDeleteId(null)}
                                                        className="text-sm text-ink-3 hover:underline"
                                                    >
                                                        Cancel
                                                    </button>
                                                </>
                                            ) : (
                                                <button
                                                    onClick={() => setConfirmDeleteId(m.id)}
                                                    className="text-sm text-ink-3 hover:text-bad transition-colors"
                                                >
                                                    Delete
                                                </button>
                                            )}
                                        </div>
                                    </div>
                                    <pre className="text-xs text-ink-3 font-mono whitespace-pre-wrap line-clamp-4 bg-surface rounded p-3">
                                        {m.markdownContent}
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
