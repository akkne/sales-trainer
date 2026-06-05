"use client";

import { useState } from "react";
import Link from "next/link";
import {
    useAdminDialogBundles,
    useAdminSkills,
    useCreateBundle,
    useUpdateBundle,
    useDeleteBundle,
    AdminDialogBundle,
    CreateBundleRequest,
} from "@/features/dialog/hooks/use-admin-dialog";

export default function AdminDialogPage() {
    const { data: bundles, isLoading, error } = useAdminDialogBundles();
    const { data: skills } = useAdminSkills();
    const createBundleMutation = useCreateBundle();
    const updateBundleMutation = useUpdateBundle();
    const deleteBundleMutation = useDeleteBundle();

    const [isCreating, setIsCreating] = useState(false);
    const [editingBundleId, setEditingBundleId] = useState<string | null>(null);
    const [deletingBundleId, setDeletingBundleId] = useState<string | null>(null);

    const [formData, setFormData] = useState<CreateBundleRequest>({
        skillId: "",
        title: "",
        description: "",
        iconEmoji: "📞",
        sortOrder: 0,
        isActive: true,
    });

    const resetForm = () => {
        setFormData({
            skillId: skills?.[0]?.id ?? "",
            title: "",
            description: "",
            iconEmoji: "📞",
            sortOrder: 0,
            isActive: true,
        });
        setIsCreating(false);
        setEditingBundleId(null);
    };

    const handleCreate = async () => {
        await createBundleMutation.mutateAsync(formData);
        resetForm();
    };

    const handleUpdate = async () => {
        if (!editingBundleId) return;
        await updateBundleMutation.mutateAsync({ bundleId: editingBundleId, request: formData });
        resetForm();
    };

    const handleDelete = async (bundleId: string) => {
        await deleteBundleMutation.mutateAsync(bundleId);
        setDeletingBundleId(null);
    };

    const startEditing = (bundle: AdminDialogBundle) => {
        setFormData({
            skillId: bundle.skillId,
            title: bundle.title,
            description: bundle.description,
            iconEmoji: bundle.iconEmoji,
            sortOrder: bundle.sortOrder,
            isActive: bundle.isActive,
        });
        setEditingBundleId(bundle.id);
        setIsCreating(false);
    };

    const startCreating = () => {
        setFormData({
            skillId: skills?.[0]?.id ?? "",
            title: "",
            description: "",
            iconEmoji: "📞",
            sortOrder: bundles?.length ?? 0,
            isActive: true,
        });
        setIsCreating(true);
        setEditingBundleId(null);
    };

    if (isLoading) {
        return (
            <div className="p-6">
                <h1 className="text-xl font-bold text-ink mb-6">Dialog Bundles</h1>
                <p className="text-ink-3">Loading...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-6">
                <h1 className="text-xl font-bold text-ink mb-6">Dialog Bundles</h1>
                <p className="text-bad">Error: {error.message}</p>
            </div>
        );
    }

    return (
        <div className="p-6">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-xl font-bold text-ink">Dialog Bundles</h1>
                <button
                    onClick={startCreating}
                    className="px-4 py-2 bg-ink text-bg rounded-lg hover:opacity-90"
                >
                    + New Bundle
                </button>
            </div>

            {(isCreating || editingBundleId) && (
                <div className="mb-6 p-4 bg-surface rounded-2xl">
                    <h2 className="text-lg font-semibold mb-4">
                        {isCreating ? "Create Bundle" : "Edit Bundle"}
                    </h2>
                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm font-medium text-ink mb-1">
                                Skill
                            </label>
                            <select
                                value={formData.skillId}
                                onChange={(changeEvent) => setFormData({ ...formData, skillId: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30"
                            >
                                <option value="">Select skill...</option>
                                {skills?.map((skill) => (
                                    <option key={skill.id} value={skill.id}>
                                        {skill.title} ({skill.slug})
                                    </option>
                                ))}
                            </select>
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-ink mb-1">
                                Title
                            </label>
                            <input
                                type="text"
                                value={formData.title}
                                onChange={(changeEvent) => setFormData({ ...formData, title: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                placeholder="Холодные звонки"
                            />
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-ink mb-1">
                                Icon Emoji
                            </label>
                            <input
                                type="text"
                                value={formData.iconEmoji}
                                onChange={(changeEvent) => setFormData({ ...formData, iconEmoji: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                placeholder="📞"
                            />
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-ink mb-1">
                                Sort Order
                            </label>
                            <input
                                type="number"
                                value={formData.sortOrder}
                                onChange={(changeEvent) => setFormData({ ...formData, sortOrder: parseInt(changeEvent.target.value) || 0 })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30"
                            />
                        </div>
                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-ink mb-1">
                                Description
                            </label>
                            <textarea
                                value={formData.description}
                                onChange={(changeEvent) => setFormData({ ...formData, description: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                rows={2}
                                placeholder="Описание бандла..."
                            />
                        </div>
                        <div className="col-span-2">
                            <label className="flex items-center gap-2">
                                <input
                                    type="checkbox"
                                    checked={formData.isActive}
                                    onChange={(changeEvent) => setFormData({ ...formData, isActive: changeEvent.target.checked })}
                                />
                                <span className="text-sm text-ink">Active</span>
                            </label>
                        </div>
                    </div>
                    <div className="flex gap-2 mt-4">
                        <button
                            onClick={isCreating ? handleCreate : handleUpdate}
                            disabled={createBundleMutation.isPending || updateBundleMutation.isPending || !formData.skillId}
                            className="px-4 py-2 bg-ink text-bg rounded-lg hover:opacity-90 disabled:opacity-40"
                        >
                            {isCreating ? "Create" : "Save"}
                        </button>
                        <button
                            onClick={resetForm}
                            className="px-4 py-2 bg-bg-2 text-ink-3 rounded-lg hover:bg-surface-2"
                        >
                            Cancel
                        </button>
                    </div>
                </div>
            )}

            <div className="bg-surface rounded-2xl overflow-hidden">
                <table className="w-full">
                    <thead className="bg-surface">
                        <tr>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Icon</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Title</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Skill</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Order</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Status</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Actions</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-line">
                        {bundles?.map((bundle) => (
                            <tr key={bundle.id} className="hover:bg-bg-2">
                                <td className="px-4 py-3 text-2xl">{bundle.iconEmoji}</td>
                                <td className="px-4 py-3">
                                    <Link
                                        href={`/admin/dialog/${bundle.id}`}
                                        className="text-indigo hover:underline font-medium"
                                    >
                                        {bundle.title}
                                    </Link>
                                </td>
                                <td className="px-4 py-3 text-ink-3 text-sm">
                                    {bundle.skillTitle}
                                    <span className="text-ink-3 ml-1">({bundle.skillSlug})</span>
                                </td>
                                <td className="px-4 py-3 text-ink-3">{bundle.sortOrder}</td>
                                <td className="px-4 py-3">
                                    <span
                                        className={`px-2 py-1 text-xs rounded-full ${
                                            bundle.isActive
                                                ? "bg-indigo-soft text-indigo"
                                                : "bg-bg-2 text-ink-3"
                                        }`}
                                    >
                                        {bundle.isActive ? "Active" : "Inactive"}
                                    </span>
                                </td>
                                <td className="px-4 py-3">
                                    <div className="flex gap-2">
                                        <button
                                            onClick={() => startEditing(bundle)}
                                            className="text-indigo hover:underline text-sm"
                                        >
                                            Edit
                                        </button>
                                        <button
                                            onClick={() => setDeletingBundleId(bundle.id)}
                                            className="text-bad hover:underline text-sm"
                                        >
                                            Delete
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                        {(!bundles || bundles.length === 0) && (
                            <tr>
                                <td colSpan={6} className="px-4 py-8 text-center text-ink-3">
                                    No bundles yet. Create one to get started.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>

            {deletingBundleId && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
                    <div className="bg-surface rounded-2xl p-6 max-w-sm">
                        <h3 className="text-lg font-semibold mb-2">Delete Bundle?</h3>
                        <p className="text-ink-3 mb-4">
                            This will also delete all modes in this bundle. This action cannot be undone.
                        </p>
                        <div className="flex gap-2">
                            <button
                                onClick={() => handleDelete(deletingBundleId)}
                                disabled={deleteBundleMutation.isPending}
                                className="px-4 py-2 bg-bad text-white rounded-lg hover:bg-bad disabled:opacity-40"
                            >
                                Delete
                            </button>
                            <button
                                onClick={() => setDeletingBundleId(null)}
                                className="px-4 py-2 bg-bg-2 text-ink-3 rounded-lg hover:bg-surface-2"
                            >
                                Cancel
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
