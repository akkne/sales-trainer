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
                <Link href="/admin/dialog" className="text-indigo hover:underline mb-4 inline-block">
                    ← Back to Bundles
                </Link>
                <h1 className="text-xl font-bold text-ink mb-6">Modes</h1>
                <p className="text-ink-3">Loading...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-6">
                <Link href="/admin/dialog" className="text-indigo hover:underline mb-4 inline-block">
                    ← Back to Bundles
                </Link>
                <h1 className="text-xl font-bold text-ink mb-6">Modes</h1>
                <p className="text-bad">Error: {error.message}</p>
            </div>
        );
    }

    return (
        <div className="p-6">
            <Link href="/admin/dialog" className="text-indigo hover:underline mb-4 inline-block">
                ← Back to Bundles
            </Link>

            <div className="flex flex-wrap gap-3 justify-between items-center mb-6">
                <div>
                    <h1 className="text-xl font-bold text-ink flex items-center gap-2">
                        {currentBundle?.iconEmoji} {currentBundle?.title || "Bundle"} — Modes
                    </h1>
                    <p className="text-ink-3 text-sm mt-1">
                        Skill: {currentBundle?.skillTitle} ({currentBundle?.skillSlug})
                    </p>
                </div>
                <button
                    onClick={startCreating}
                    className="px-4 py-2 bg-ink text-bg rounded-lg hover:opacity-90"
                >
                    + New Mode
                </button>
            </div>

            {(isCreating || editingModeId) && (
                <div className="mb-6 p-4 border border-line rounded-2xl bg-surface">
                    <h2 className="text-lg font-semibold text-ink mb-4">
                        {isCreating ? "Create Mode" : "Edit Mode"}
                    </h2>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm font-medium text-ink mb-1">
                                Key
                            </label>
                            <input
                                type="text"
                                value={formData.key}
                                onChange={(changeEvent) => setFormData({ ...formData, key: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                placeholder="secretary-bypass"
                            />
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
                                placeholder="Обход секретаря"
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
                        <div className="flex items-end">
                            <label className="flex items-center gap-2">
                                <input
                                    type="checkbox"
                                    checked={formData.isActive}
                                    onChange={(changeEvent) => setFormData({ ...formData, isActive: changeEvent.target.checked })}
                                />
                                <span className="text-sm text-ink">Active</span>
                            </label>
                        </div>
                        <div className="flex items-end">
                            <label className="flex items-center gap-2">
                                <input
                                    type="checkbox"
                                    checked={formData.voiceEnabled}
                                    onChange={(changeEvent) => setFormData({ ...formData, voiceEnabled: changeEvent.target.checked })}
                                />
                                <span className="text-sm text-ink">Voice Enabled</span>
                            </label>
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-ink mb-1">
                                Voice ID (ElevenLabs)
                            </label>
                            <input
                                type="text"
                                value={formData.voiceId || ""}
                                onChange={(changeEvent) => setFormData({ ...formData, voiceId: changeEvent.target.value || null })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                placeholder="Leave empty for default"
                                disabled={!formData.voiceEnabled}
                            />
                        </div>
                        <div className="md:col-span-2">
                            <label className="block text-sm font-medium text-ink mb-1">
                                Description
                            </label>
                            <textarea
                                value={formData.description}
                                onChange={(changeEvent) => setFormData({ ...formData, description: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                rows={2}
                                placeholder="Описание режима..."
                            />
                        </div>
                        <div className="md:col-span-2">
                            <label className="block text-sm font-medium text-ink mb-1">
                                Chat System Prompt (AI role for conversation)
                            </label>
                            <textarea
                                value={formData.chatSystemPrompt}
                                onChange={(changeEvent) => setFormData({ ...formData, chatSystemPrompt: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30 font-mono text-sm"
                                rows={10}
                                placeholder="Ты — секретарь крупной компании..."
                            />
                            <p className="text-xs text-ink-3 mt-1">
                                AI will add [DIALOG_END] when conversation should end.
                            </p>
                        </div>
                        <div className="md:col-span-2">
                            <label className="block text-sm font-medium text-ink mb-1">
                                Feedback System Prompt (AI evaluation instructions)
                            </label>
                            <textarea
                                value={formData.feedbackSystemPrompt}
                                onChange={(changeEvent) => setFormData({ ...formData, feedbackSystemPrompt: changeEvent.target.value })}
                                className="w-full px-3 py-2 border border-line rounded-xl bg-surface text-ink focus:outline-none focus:ring-1 focus:ring-indigo/30 font-mono text-sm"
                                rows={8}
                                placeholder="Проанализируй диалог менеджера..."
                            />
                            <p className="text-xs text-ink-3 mt-1">
                                AI will add [XP:number] at the end (0-100 based on performance).
                            </p>
                        </div>
                    </div>
                    <div className="flex gap-2 mt-4">
                        <button
                            onClick={isCreating ? handleCreate : handleUpdate}
                            disabled={createModeMutation.isPending || updateModeMutation.isPending}
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

            <div className="bg-surface rounded-2xl border border-line overflow-hidden">
            <div className="overflow-x-auto -mx-4 px-4">
                <table className="w-full min-w-[520px]">
                    <thead className="bg-surface">
                        <tr>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Title</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Key</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Order</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Status</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Voice</th>
                            <th className="px-4 py-3 text-left text-sm font-medium text-ink-3">Actions</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-line">
                        {modes?.map((mode) => (
                            <tr key={mode.id} className="hover:bg-bg-2">
                                <td className="px-4 py-3 font-medium">{mode.title}</td>
                                <td className="px-4 py-3 text-ink-3 text-sm">{mode.key}</td>
                                <td className="px-4 py-3 text-ink-3">{mode.sortOrder}</td>
                                <td className="px-4 py-3">
                                    <span
                                        className={`px-2 py-1 text-xs rounded-full ${
                                            mode.isActive
                                                ? "bg-indigo-soft text-indigo"
                                                : "bg-bg-2 text-ink-3"
                                        }`}
                                    >
                                        {mode.isActive ? "Active" : "Inactive"}
                                    </span>
                                </td>
                                <td className="px-4 py-3">
                                    {mode.voiceEnabled ? (
                                        <span className="px-2 py-1 text-xs rounded-full bg-accent-soft text-accent">
                                            🎤 Voice
                                        </span>
                                    ) : (
                                        <span className="text-ink-3 text-xs">—</span>
                                    )}
                                </td>
                                <td className="px-4 py-3">
                                    <div className="flex gap-2">
                                        <button
                                            onClick={() => startEditing(mode)}
                                            className="text-indigo hover:underline text-sm"
                                        >
                                            Edit
                                        </button>
                                        <button
                                            onClick={() => setDeletingModeId(mode.id)}
                                            className="text-bad hover:underline text-sm"
                                        >
                                            Delete
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                        {(!modes || modes.length === 0) && (
                            <tr>
                                <td colSpan={6} className="px-4 py-8 text-center text-ink-3">
                                    No modes yet. Create one to get started.
                                </td>
                            </tr>
                        )}
                    </tbody>
                </table>
            </div>
            </div>

            {deletingModeId && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
                    <div className="bg-surface rounded-2xl p-6 max-w-sm">
                        <h3 className="text-lg font-semibold text-ink mb-2">Delete Mode?</h3>
                        <p className="text-ink-3 mb-4">
                            This action cannot be undone.
                        </p>
                        <div className="flex gap-2">
                            <button
                                onClick={() => handleDelete(deletingModeId)}
                                disabled={deleteModeMutation.isPending}
                                className="px-4 py-2 bg-bad text-white rounded-lg hover:bg-bad/90 disabled:opacity-40"
                            >
                                Delete
                            </button>
                            <button
                                onClick={() => setDeletingModeId(null)}
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
