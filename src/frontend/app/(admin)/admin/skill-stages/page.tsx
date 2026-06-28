"use client";

import { useState } from "react";
import Link from "next/link";
import {
    useAdminSkillStages,
    useCreateSkillStage,
    useUpdateSkillStage,
    useDeleteSkillStage,
    type AdminSkillStage,
} from "@/features/admin/hooks/use-admin";

interface StageDraft {
    label: string;
    accent: string;
    order: number;
}

const emptyNewStage = { key: "", label: "", accent: "#7C3AED", order: 1 };

export default function AdminSkillStagesPage() {
    const { data: stages = [], isLoading } = useAdminSkillStages();
    const createStage = useCreateSkillStage();
    const updateStage = useUpdateSkillStage();
    const deleteStage = useDeleteSkillStage();

    const [drafts, setDrafts] = useState<Record<string, StageDraft>>({});
    const [newStage, setNewStage] = useState({ ...emptyNewStage });
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    const draftFor = (stage: AdminSkillStage): StageDraft =>
        drafts[stage.id] ?? { label: stage.label, accent: stage.accent, order: stage.order };

    const setDraft = (stage: AdminSkillStage, patch: Partial<StageDraft>) =>
        setDrafts((d) => ({ ...d, [stage.id]: { ...draftFor(stage), ...patch } }));

    const isDirty = (stage: AdminSkillStage) => {
        const d = drafts[stage.id];
        return !!d && (d.label !== stage.label || d.accent !== stage.accent || d.order !== stage.order);
    };

    const handleSave = (stage: AdminSkillStage) => {
        const d = draftFor(stage);
        updateStage.mutate(
            { id: stage.id, label: d.label, accent: d.accent, order: d.order },
            {
                onSuccess: () =>
                    setDrafts((all) =>
                        Object.fromEntries(Object.entries(all).filter(([id]) => id !== stage.id))
                    ),
            }
        );
    };

    const handleDelete = (stage: AdminSkillStage) => {
        if (confirmDeleteId !== stage.id) {
            setConfirmDeleteId(stage.id);
            return;
        }
        deleteStage.mutate(stage.id, { onSettled: () => setConfirmDeleteId(null) });
    };

    const handleCreate = () => {
        const key = newStage.key.trim().toLowerCase();
        if (!key || !newStage.label.trim()) return;
        createStage.mutate(
            { key, label: newStage.label.trim(), accent: newStage.accent, order: newStage.order },
            { onSuccess: () => setNewStage({ ...emptyNewStage, order: newStage.order + 1 }) }
        );
    };

    // `accent` may be a CSS variable (e.g. var(--indigo)); only feed plain hex to the color input.
    const asHex = (accent: string) => (/^#[0-9a-fA-F]{3,8}$/.test(accent) ? accent : "#888888");

    const mutationError =
        (createStage.error as Error | null)?.message ??
        (updateStage.error as Error | null)?.message ??
        (deleteStage.error as Error | null)?.message;

    return (
        <div>
            <div className="mb-2">
                <Link
                    href="/admin/skills"
                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    ← All skills
                </Link>
            </div>
            <div className="mb-6">
                <h1 className="text-xl font-bold text-ink">Skill stages</h1>
                <p className="text-xs text-ink-3 mt-0.5">
                    Funnel stages used to group skills on the tree. They run from the lowest order
                    (shown first) to the highest. The key is permanent — it is stored on every skill —
                    while label, accent color, and order are editable. Unassigned skills fall back to a
                    generic “Other” bucket.
                </p>
            </div>

            {mutationError && (
                <p className="text-xs text-red-500 mb-4">{mutationError}</p>
            )}

            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : (
                <div className="overflow-x-auto -mx-4 px-4">
                <table className="w-full text-sm border-collapse min-w-[560px]">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium w-20">Order</th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Key</th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Label</th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Accent</th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {[...stages]
                            .sort((a, b) => a.order - b.order)
                            .map((stage) => {
                                const d = draftFor(stage);
                                return (
                                    <tr key={stage.id} className="border-b border-line hover:bg-bg-2">
                                        <td className="py-2 px-3">
                                            <input
                                                type="number"
                                                value={d.order}
                                                onChange={(e) => setDraft(stage, { order: Number(e.target.value) })}
                                                className="w-16 text-sm border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                            />
                                        </td>
                                        <td className="py-2 px-3">
                                            <code className="text-xs text-ink-3">{stage.key}</code>
                                        </td>
                                        <td className="py-2 px-3">
                                            <input
                                                value={d.label}
                                                onChange={(e) => setDraft(stage, { label: e.target.value })}
                                                className="w-48 text-sm border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                            />
                                        </td>
                                        <td className="py-2 px-3">
                                            <div className="flex items-center gap-2">
                                                <input
                                                    type="color"
                                                    value={asHex(d.accent)}
                                                    onChange={(e) => setDraft(stage, { accent: e.target.value })}
                                                    className="w-8 h-8 rounded border border-line p-0.5"
                                                />
                                                <input
                                                    value={d.accent}
                                                    onChange={(e) => setDraft(stage, { accent: e.target.value })}
                                                    className="w-32 text-xs border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                />
                                            </div>
                                        </td>
                                        <td className="py-2 px-3 text-right whitespace-nowrap">
                                            <button
                                                onClick={() => handleSave(stage)}
                                                disabled={!isDirty(stage) || updateStage.isPending}
                                                className="text-xs px-2 py-1 rounded border border-line text-ink-3 hover:text-ink hover:bg-bg-2 transition-colors disabled:opacity-40"
                                            >
                                                Save
                                            </button>
                                            <button
                                                onClick={() => handleDelete(stage)}
                                                onBlur={() => setConfirmDeleteId(null)}
                                                disabled={deleteStage.isPending}
                                                className={`text-xs px-2 py-1 rounded ml-1 transition-colors disabled:opacity-50 ${
                                                    confirmDeleteId === stage.id
                                                        ? "bg-red-500 text-white"
                                                        : "text-ink-3 hover:text-red-500"
                                                }`}
                                            >
                                                {confirmDeleteId === stage.id ? "Confirm?" : "Delete"}
                                            </button>
                                        </td>
                                    </tr>
                                );
                            })}
                    </tbody>
                </table>
                </div>
            )}

            <div className="mt-6 p-4 border border-line rounded-xl bg-bg-2/50">
                <h2 className="text-sm font-semibold text-ink mb-3">Add stage</h2>
                <div className="flex flex-wrap items-end gap-3">
                    <label className="text-xs text-ink-3">
                        Order
                        <input
                            type="number"
                            value={newStage.order}
                            onChange={(e) => setNewStage({ ...newStage, order: Number(e.target.value) })}
                            className="block mt-1 w-16 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        />
                    </label>
                    <label className="text-xs text-ink-3">
                        Key (slug)
                        <input
                            value={newStage.key}
                            placeholder="negotiation"
                            onChange={(e) => setNewStage({ ...newStage, key: e.target.value })}
                            className="block mt-1 w-32 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        />
                    </label>
                    <label className="text-xs text-ink-3">
                        Label
                        <input
                            value={newStage.label}
                            placeholder="Negotiation"
                            onChange={(e) => setNewStage({ ...newStage, label: e.target.value })}
                            className="block mt-1 w-48 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        />
                    </label>
                    <label className="text-xs text-ink-3">
                        Accent
                        <span className="flex items-center gap-2 mt-1">
                            <input
                                type="color"
                                value={asHex(newStage.accent)}
                                onChange={(e) => setNewStage({ ...newStage, accent: e.target.value })}
                                className="w-8 h-8 rounded border border-line p-0.5"
                            />
                            <input
                                value={newStage.accent}
                                onChange={(e) => setNewStage({ ...newStage, accent: e.target.value })}
                                className="w-32 text-xs border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                            />
                        </span>
                    </label>
                    <button
                        onClick={handleCreate}
                        disabled={createStage.isPending || !newStage.key.trim() || !newStage.label.trim()}
                        className="text-sm px-3 py-1.5 rounded-lg bg-indigo text-white hover:opacity-90 transition-opacity disabled:opacity-50"
                    >
                        {createStage.isPending ? "Adding..." : "Add stage"}
                    </button>
                </div>
            </div>
        </div>
    );
}
