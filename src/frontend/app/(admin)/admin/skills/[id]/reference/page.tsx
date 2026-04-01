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
    const deleteMaterial = useDeleteReference(skillId);

    const [showForm, setShowForm] = useState(false);
    const [form, setForm] = useState({ title: "", markdownContent: "", sortOrder: 0 });

    const [editId, setEditId] = useState<string | null>(null);
    const [editForm, setEditForm] = useState({ title: "", markdownContent: "", sortOrder: 0 });
    const updateMaterial = useUpdateReference(skillId, editId ?? "");

    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    async function handleCreate() {
        await createMaterial.mutateAsync(form);
        setForm({ title: "", markdownContent: "", sortOrder: 0 });
        setShowForm(false);
    }

    function startEdit(m: AdminReferenceMaterial) {
        setEditId(m.id);
        setEditForm({
            title: m.title,
            markdownContent: m.markdownContent,
            sortOrder: m.sortOrder,
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
                    className="text-xs text-gray-400 hover:text-gray-600 transition-colors"
                >
                    ← Back to skill
                </Link>
            </div>

            <div className="flex items-center justify-between mb-6">
                <h1 className="text-xl font-semibold text-gray-900">Reference materials</h1>
                <button
                    onClick={() => setShowForm((v) => !v)}
                    className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 transition-colors"
                >
                    {showForm ? "Cancel" : "+ New material"}
                </button>
            </div>

            {showForm && (
                <div className="bg-white border border-gray-200 rounded-lg p-5 mb-6">
                    <div className="space-y-4">
                        <label className="block">
                            <span className="text-xs text-gray-500">Title</span>
                            <input
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                value={form.title}
                                onChange={(e) => setForm({ ...form, title: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-gray-500">Sort order</span>
                            <input
                                type="number"
                                className="mt-1 w-32 border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                value={form.sortOrder}
                                onChange={(e) =>
                                    setForm({ ...form, sortOrder: Number(e.target.value) })
                                }
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-gray-500">Content (Markdown)</span>
                            <textarea
                                rows={12}
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-gray-400"
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
                        className="mt-4 px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                    >
                        {createMaterial.isPending ? "Saving..." : "Create"}
                    </button>
                </div>
            )}

            {isLoading ? (
                <p className="text-sm text-gray-400">Loading...</p>
            ) : materials.length === 0 ? (
                <p className="text-sm text-gray-400">No reference materials yet.</p>
            ) : (
                <div className="space-y-4">
                    {materials.map((m) => (
                        <div
                            key={m.id}
                            className="bg-white border border-gray-200 rounded-lg p-5"
                        >
                            {editId === m.id ? (
                                <div className="space-y-4">
                                    <label className="block">
                                        <span className="text-xs text-gray-500">Title</span>
                                        <input
                                            className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                            value={editForm.title}
                                            onChange={(e) =>
                                                setEditForm({ ...editForm, title: e.target.value })
                                            }
                                        />
                                    </label>
                                    <label className="block">
                                        <span className="text-xs text-gray-500">Sort order</span>
                                        <input
                                            type="number"
                                            className="mt-1 w-32 border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                            value={editForm.sortOrder}
                                            onChange={(e) =>
                                                setEditForm({
                                                    ...editForm,
                                                    sortOrder: Number(e.target.value),
                                                })
                                            }
                                        />
                                    </label>
                                    <label className="block">
                                        <span className="text-xs text-gray-500">
                                            Content (Markdown)
                                        </span>
                                        <textarea
                                            rows={12}
                                            className="mt-1 w-full border border-gray-300 rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-gray-400"
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
                                            className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                                        >
                                            {updateMaterial.isPending ? "Saving..." : "Save"}
                                        </button>
                                        <button
                                            onClick={() => setEditId(null)}
                                            className="px-4 py-2 text-sm text-gray-500 hover:text-gray-700 transition-colors"
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div>
                                    <div className="flex items-center justify-between mb-3">
                                        <div>
                                            <h3 className="font-medium text-gray-800">
                                                {m.title}
                                            </h3>
                                            <span className="text-xs text-gray-400">
                                                order: {m.sortOrder}
                                            </span>
                                        </div>
                                        <div className="flex gap-3">
                                            <button
                                                onClick={() => startEdit(m)}
                                                className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
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
                                                    onClick={() => setConfirmDeleteId(m.id)}
                                                    className="text-sm text-gray-400 hover:text-red-500 transition-colors"
                                                >
                                                    Delete
                                                </button>
                                            )}
                                        </div>
                                    </div>
                                    <pre className="text-xs text-gray-500 font-mono whitespace-pre-wrap line-clamp-4 bg-gray-50 rounded p-3">
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
