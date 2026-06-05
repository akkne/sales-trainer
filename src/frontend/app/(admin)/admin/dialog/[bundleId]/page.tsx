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
} from "@/features/dialog/hooks/use-admin-dialog";

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
        voiceEnabled: false,
        voiceId: null,
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
            voiceEnabled: false,
            voiceId: null,
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
            voiceEnabled: mode.voiceEnabled,
            voiceId: mode.voiceId,
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
            voiceEnabled: false,
            voiceId: null,
        });
        setIsCreating(true);
        setEditingModeId(null);
    };

    if (isLoading) {
        return (
            <div className="p-6">
                <Link href="/admin/dialog" className="text-primary hover:underline mb-4 inline-block">
                    ← Back to Bundles
                </Link>
                <h1 className="font-headline text-xl font-bold text-on-surface mb-6">Modes</h1>
                <p className="text-on-surface-variant">Loading...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-6">
                <Link href="/admin/dialog" className="text-primary hover:underline mb-4 inline-block">
                    ← Back to Bundles
                </Link>
                <h1 className="font-headline text-xl font-bold text-on-surface mb-6">Modes</h1>
                <p className="text-error">Error: {error.message}</p>
            </div>
        );
    }

    return (
        <div className="p-6">
            <Link href="/admin/dialog" className="text-primary hover:underline mb-4 inline-block">
                ← Back to Bundles
            </Link>

            <div className="flex justify-between items-center mb-6">
                <div>
                    <h1 className="font-headline text-xl font-bold text-on-surface flex items-center gap-2">
                        {currentBundle?.iconEmoji} {currentBundle?.title || "Bundle"} — Modes
                    </h1>
                    <p className="text-on-surface-variant text-sm mt-1">
                        Skill: {currentBundle?.skillTitle} ({currentBundle?.skillSlug})
                    </p>
                </div>
                <button
                    onClick={startCreating}
                    className="px-4 py-2 bg-primary text-on-primary rounded-lg hover:bg-primary-dim"
                >
                    + New Mode
                </button>
            </div>

            {(isCreating || editingModeId) && (
                <div className="mb-6 p-4 border border-outline-variant rounded-2xl bg-surface-container-lowest">
                    <h2 className="font-headline text-lg font-semibold text-on-surface mb-4">
                        {isCreating ? "Create Mode" : "Edit Mode"}
                    </h2>
                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm font-medium text-on-surface mb-1">
                                Key
                            </label>
                            <input
                                type="text"
                                value={formData.key}
                                onChange={(changeEvent) => setFormData({ ...formData, key: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-outline-variant rounded-xl bg-surface-container-low text-on-surface focus:outline-none focus:ring-1 focus:ring-primary"
                                placeholder="secretary-bypass"
                            />
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-on-surface mb-1">
                                Title
                            </label>
                            <input
                                type="text"
                                value={formData.title}
                                onChange={(changeEvent) => setFormData({ ...formData, title: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-outline-variant rounded-xl bg-surface-container-low text-on-surface focus:outline-none focus:ring-1 focus:ring-primary"
                                placeholder="Обход секретаря"
                            />
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-on-surface mb-1">
                                Sort Order
                            </label>
                            <input
                                type="number"
                                value={formData.sortOrder}
                                onChange={(changeEvent) => setFormData({ ...formData, sortOrder: parseInt(changeEvent.target.value) || 0 })}
                                className="w-full px-3 py-2 border border-outline-variant rounded-xl bg-surface-container-low text-on-surface focus:outline-none focus:ring-1 focus:ring-primary"
                            />
                        </div>
                        <div className="flex items-end">
                            <label className="flex items-center gap-2">
                                <input
                                    type="checkbox"
                                    checked={formData.isActive}
                                    onChange={(changeEvent) => setFormData({ ...formData, isActive: changeEvent.target.checked })}
                                />
                                <span className="text-sm text-on-surface">Active</span>
                            </label>
                        </div>
                        <div className="flex items-end">
                            <label className="flex items-center gap-2">
                                <input
                                    type="checkbox"
                                    checked={formData.voiceEnabled}
                                    onChange={(changeEvent) => setFormData({ ...formData, voiceEnabled: changeEvent.target.checked })}
                                />
                                <span className="text-sm text-on-surface">Voice Enabled</span>
                            </label>
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-on-surface mb-1">
                                Voice ID (ElevenLabs)
                            </label>
                            <input
                                type="text"
                                value={formData.voiceId || ""}
                                onChange={(changeEvent) => setFormData({ ...formData, voiceId: changeEvent.target.value || null })}
                                className="w-full px-3 py-2 border border-outline-variant rounded-xl bg-surface-container-low text-on-surface focus:outline-none focus:ring-1 focus:ring-primary"
                                placeholder="Leave empty for default"
                                disabled={!formData.voiceEnabled}
                            />
                        </div>
                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-on-surface mb-1">
                                Description
                            </label>
                            <textarea
                                value={formData.description}
                                onChange={(changeEvent) => setFormData({ ...formData, description: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-outline-variant rounded-xl bg-surface-container-low text-on-surface focus:outline-none focus:ring-1 focus:ring-primary"
                                rows={2}
                                placeholder="Описание режима..."
                            />
                        </div>
                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-on-surface mb-1">
                                Chat System Prompt (AI role for conversation)
                            </label>
                            <textarea
                                value={formData.chatSystemPrompt}
                                onChange={(changeEvent) => setFormData({ ...formData, chatSystemPrompt: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-outline-variant rounded-xl bg-surface-container-low text-on-surface focus:outline-none focus:ring-1 focus:ring-primary font-mono text-sm"
                                rows={10}
                                placeholder="Ты — секретарь крупной компании..."
                            />
                            <p className="text-xs text-on-surface-variant mt-1">
                                AI will add [DIALOG_END] when conversation should end.
                            </p>
                        </div>
                        <div className="col-span-2">
                            <label className="block text-sm font-medium text-on-surface mb-1">
                                Feedback System Prompt (AI evaluation instructions)
                            </label>
                            <textarea
                                value={formData.feedbackSystemPrompt}
                                onChange={(changeEvent) => setFormData({ ...formData, feedbackSystemPrompt: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-outline-variant rounded-xl bg-surface-container-low text-on-surface focus:outline-none focus:ring-1 focus:ring-primary font-mono text-sm"
                                rows={8}
                                placeholder="Проанализируй диалог менеджера..."
                            />
                            <p className="text-xs text-on-surface-variant mt-1">
                                AI will add [XP:number] at the end (0-100 based on performance).
                            </p>
                        </div>
                    </div>
                    <div className="flex gap-2 mt-4">
                        <button
                            onClick={isCreating ? handleCreate : handleUpdate}
                            disabled={createModeMutation.isPending || updateModeMutation.isPending}
                            className="px-4 py-2 bg-primary text-on-primary rounded-lg hover:bg-primary-dim disabled:opacity-40"
                        >
                            {isCreating ? "Create" : "Save"}
                        </button>
                        <button
                            onClick={resetForm}
                            className="px-4 py-2 bg-surface-container text-on-surface-variant rounded-lg hover:bg-surface-container-high"
                        >
                            Cancel
                        </button>
                    </div>
                </div>
            )}

            <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant overflow-hidden">
                <table className="w-full">
                    <thead className="bg-surface-container-low">
                        <tr>
                            <th className="px-4 py-3 text-left text-sm font-medium text-on-surface-variant">Title</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-on-surface-variant">Key</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-on-surface-variant">Order</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-on-surface-variant">Status</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-on-surface-variant">Voice</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-on-surface-variant">Actions</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-outline-variant">
                        {modes?.map((mode) => (
                            <tr key={mode.id} className="hover:bg-surface-container-low">
                                <td className="px-4 py-3 font-medium">{mode.title}</td>
                                <td className="px-4 py-3 text-on-surface-variant text-sm">{mode.key}</td>
                                <td className="px-4 py-3 text-on-surface-variant">{mode.sortOrder}</td>
                                <td className="px-4 py-3">
                                    <span
                                        className={`px-2 py-1 text-xs rounded-full ${
                                            mode.isActive
                                                ? "bg-primary-container text-primary"
                                                : "bg-surface-container text-on-surface-variant"
                                        }`}
                                    >
                                        {mode.isActive ? "Active" : "Inactive"}
                                    </span>
                                </td>
                                <td className="px-4 py-3">
                                    {mode.voiceEnabled ? (
                                        <span className="px-2 py-1 text-xs rounded-full bg-tertiary-container text-tertiary">
                                            🎤 Voice
                                        </span>
                                    ) : (
                                        <span className="text-on-surface-variant text-xs">—</span>
                                    )}
                                </td>
                                <td className="px-4 py-3">
                                    <div className="flex gap-2">
                                        <button
                                            onClick={() => startEditing(mode)}
                                            className="text-primary hover:underline text-sm"
                                        >
                                            Edit
                                        </button>
                                        <button
                                            onClick={() => setDeletingModeId(mode.id)}
                                            className="text-error hover:underline text-sm"
                                        >
                                            Delete
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                        {(!modes || modes.length === 0) && (
                            <tr>
                                <td colSpan={6} className="px-4 py-8 text-center text-on-surface-variant">
                                    No modes yet. Create one to get started.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>

            {deletingModeId && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
                    <div className="bg-surface-container-lowest rounded-2xl p-6 max-w-sm">
                        <h3 className="font-headline text-lg font-semibold text-on-surface mb-2">Delete Mode?</h3>
                        <p className="text-on-surface-variant mb-4">
                            This action cannot be undone.
                        </p>
                        <div className="flex gap-2">
                            <button
                                onClick={() => handleDelete(deletingModeId)}
                                disabled={deleteModeMutation.isPending}
                                className="px-4 py-2 bg-error text-on-error rounded-lg hover:bg-error/90 disabled:opacity-40"
                            >
                                Delete
                            </button>
                            <button
                                onClick={() => setDeletingModeId(null)}
                                className="px-4 py-2 bg-surface-container text-on-surface-variant rounded-lg hover:bg-surface-container-high"
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
