"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
    useAdminDialogBundles,
    useAdminDialogModes,
    useCreateMode,
    useUpdateMode,
    useDeleteMode,
    AdminDialogMode,
    CreateModeRequest,
} from "@/lib/hooks/useAdminDialog";

export default function AdminBundleModesPage() {
    const params = useParams();
    const bundleId = params.bundleId as string;

    const { data: bundles } = useAdminDialogBundles();
    const { data: modes, isLoading, error } = useAdminDialogModes(bundleId);
    const createModeMutation = useCreateMode();
    const updateModeMutation = useUpdateMode();
    const deleteModeMutation = useDeleteMode();

    const currentBundle = bundles?.find((bundle) => bundle.id === bundleId);

    const [isCreating, setIsCreating] = useState(false);
    const [editingModeId, setEditingModeId] = useState<string | null>(null);
    const [deletingModeId, setDeletingModeId] = useState<string | null>(null);

    const [formData, setFormData] = useState<CreateModeRequest>({
        key: "",
        title: "",
        description: "",
        chatSystemPrompt: "",
        feedbackSystemPrompt: "",
        sortOrder: 0,
        isActive: true,
    });

    const resetForm = () => {
        setFormData({
            key: "",
            title: "",
            description: "",
            chatSystemPrompt: "",
            feedbackSystemPrompt: "",
            sortOrder: modes?.length ?? 0,
            isActive: true,
        });
        setIsCreating(false);
        setEditingModeId(null);
    };

    const handleCreate = async () => {
        await createModeMutation.mutateAsync({ bundleId, request: formData });
        resetForm();
    };

    const handleUpdate = async () => {
        if (!editingModeId) return;
        await updateModeMutation.mutateAsync({ modeId: editingModeId, request: formData });
        resetForm();
    };

    const handleDelete = async (modeId: string) => {
        await deleteModeMutation.mutateAsync(modeId);
        setDeletingModeId(null);
    };

    const startEditing = (mode: AdminDialogMode) => {
        setFormData({
            key: mode.key,
            title: mode.title,
            description: mode.description,
            chatSystemPrompt: mode.chatSystemPrompt,
            feedbackSystemPrompt: mode.feedbackSystemPrompt,
            sortOrder: mode.sortOrder,
            isActive: mode.isActive,
        });
        setEditingModeId(mode.id);
        setIsCreating(false);
    };

    const startCreating = () => {
        setFormData({
            key: "",
            title: "",
            description: "",
            chatSystemPrompt: "",
            feedbackSystemPrompt: "",
            sortOrder: modes?.length ?? 0,
            isActive: true,
        });
        setIsCreating(true);
        setEditingModeId(null);
    };

    if (isLoading) {
        return (
            <div className="p-6">
                <Link href="/admin/dialog" className="text-blue-600 hover:underline mb-4 inline-block">
                    ← Back to Bundles
                </Link>
                <h1 className="text-2xl font-bold mb-6">Modes</h1>
                <p className="text-gray-500">Loading...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-6">
                <Link href="/admin/dialog" className="text-blue-600 hover:underline mb-4 inline-block">
                    ← Back to Bundles
                </Link>
                <h1 className="text-2xl font-bold mb-6">Modes</h1>
                <p className="text-red-500">Error: {error.message}</p>
            </div>
        );
    }

    return (
        <div className="p-6">
            <Link href="/admin/dialog" className="text-blue-600 hover:underline mb-4 inline-block">
                ← Back to Bundles
            </Link>

            <div className="flex justify-between items-center mb-6">
                <div>
                    <h1 className="text-2xl font-bold flex items-center gap-2">
                        {currentBundle?.iconEmoji} {currentBundle?.title || "Bundle"} — Modes
                    </h1>
                    <p className="text-gray-500 text-sm mt-1">
                        Skill: {currentBundle?.skillTitle} ({currentBundle?.skillSlug})
                    </p>
                </div>
                <button
                    onClick={startCreating}
                    className="px-4 py-2 bg-green-500 text-white rounded-lg hover:bg-green-600"
                >
                    + New Mode
                </button>
            </div>

            {(isCreating || editingModeId) && (
                <div className="mb-6 p-4 border rounded-lg bg-white">
                    <h2 className="text-lg font-semibold mb-4">
                        {isCreating ? "Create Mode" : "Edit Mode"}
                    </h2>
                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                Key
                            </label>
                            <input
                                type="text"
                                value={formData.key}
                                onChange={(changeEvent) => setFormData({ ...formData, key: changeEvent.target.value })}
                                className="w-full px-3 py-2 border rounded-lg"
                                placeholder="secretary-bypass"
                            />
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                Title
                            </label>
                            <input
                                type="text"
                                value={formData.title}
                                onChange={(changeEvent) => setFormData({ ...formData, title: changeEvent.target.value })}
                                className="w-full px-3 py-2 border rounded-lg"
                                placeholder="Обход секретаря"
                            />
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                Sort Order
                            </label>
                            <input
                                type="number"
                                value={formData.sortOrder}
                                onChange={(changeEvent) => setFormData({ ...formData, sortOrder: parseInt(changeEvent.target.value) || 0 })}
                                className="w-full px-3 py-2 border rounded-lg"
                            />
                        </div>
                        <div className="flex items-end">
                            <label className="flex items-center gap-2">
                                <input
                                    type="checkbox"
                                    checked={formData.isActive}
                                    onChange={(changeEvent) => setFormData({ ...formData, isActive: changeEvent.target.checked })}
                                />
                                <span className="text-sm text-gray-700">Active</span>
                            </label>
                        </div>
                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                Description
                            </label>
                            <textarea
                                value={formData.description}
                                onChange={(changeEvent) => setFormData({ ...formData, description: changeEvent.target.value })}
                                className="w-full px-3 py-2 border rounded-lg"
                                rows={2}
                                placeholder="Описание режима..."
                            />
                        </div>
                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                Chat System Prompt (AI role for conversation)
                            </label>
                            <textarea
                                value={formData.chatSystemPrompt}
                                onChange={(changeEvent) => setFormData({ ...formData, chatSystemPrompt: changeEvent.target.value })}
                                className="w-full px-3 py-2 border rounded-lg font-mono text-sm"
                                rows={10}
                                placeholder="Ты — секретарь крупной компании..."
                            />
                            <p className="text-xs text-gray-400 mt-1">
                                AI will add [DIALOG_END] when conversation should end.
                            </p>
                        </div>
                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-gray-700 mb-1">
                                Feedback System Prompt (AI evaluation instructions)
                            </label>
                            <textarea
                                value={formData.feedbackSystemPrompt}
                                onChange={(changeEvent) => setFormData({ ...formData, feedbackSystemPrompt: changeEvent.target.value })}
                                className="w-full px-3 py-2 border rounded-lg font-mono text-sm"
                                rows={8}
                                placeholder="Проанализируй диалог менеджера..."
                            />
                            <p className="text-xs text-gray-400 mt-1">
                                AI will add [XP:number] at the end (0-100 based on performance).
                            </p>
                        </div>
                    </div>
                    <div className="flex gap-2 mt-4">
                        <button
                            onClick={isCreating ? handleCreate : handleUpdate}
                            disabled={createModeMutation.isPending || updateModeMutation.isPending}
                            className="px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 disabled:bg-gray-300"
                        >
                            {isCreating ? "Create" : "Save"}
                        </button>
                        <button
                            onClick={resetForm}
                            className="px-4 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300"
                        >
                            Cancel
                        </button>
                    </div>
                </div>
            )}

            <div className="bg-white rounded-lg border overflow-hidden">
                <table className="w-full">
                    <thead className="bg-gray-50">
                        <tr>
                            <th className="px-4 py-3 text-left text-sm font-medium text-gray-500">Title</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-gray-500">Key</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-gray-500">Order</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-gray-500">Status</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-gray-500">Actions</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200">
                        {modes?.map((mode) => (
                            <tr key={mode.id} className="hover:bg-gray-50">
                                <td className="px-4 py-3 font-medium">{mode.title}</td>
                                <td className="px-4 py-3 text-gray-500 text-sm">{mode.key}</td>
                                <td className="px-4 py-3 text-gray-500">{mode.sortOrder}</td>
                                <td className="px-4 py-3">
                                    <span
                                        className={`px-2 py-1 text-xs rounded-full ${
                                            mode.isActive
                                                ? "bg-green-100 text-green-800"
                                                : "bg-gray-100 text-gray-800"
                                        }`}
                                    >
                                        {mode.isActive ? "Active" : "Inactive"}
                                    </span>
                                </td>
                                <td className="px-4 py-3">
                                    <div className="flex gap-2">
                                        <button
                                            onClick={() => startEditing(mode)}
                                            className="text-blue-600 hover:underline text-sm"
                                        >
                                            Edit
                                        </button>
                                        <button
                                            onClick={() => setDeletingModeId(mode.id)}
                                            className="text-red-600 hover:underline text-sm"
                                        >
                                            Delete
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                        {(!modes || modes.length === 0) && (
                            <tr>
                                <td colSpan={5} className="px-4 py-8 text-center text-gray-500">
                                    No modes yet. Create one to get started.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>

            {deletingModeId && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
                    <div className="bg-white rounded-lg p-6 max-w-sm">
                        <h3 className="text-lg font-semibold mb-2">Delete Mode?</h3>
                        <p className="text-gray-500 mb-4">
                            This action cannot be undone.
                        </p>
                        <div className="flex gap-2">
                            <button
                                onClick={() => handleDelete(deletingModeId)}
                                disabled={deleteModeMutation.isPending}
                                className="px-4 py-2 bg-red-500 text-white rounded-lg hover:bg-red-600 disabled:bg-gray-300"
                            >
                                Delete
                            </button>
                            <button
                                onClick={() => setDeletingModeId(null)}
                                className="px-4 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300"
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
