"use client";

import { useState } from "react";
import Link from "next/link";
import {
    useAdminLeagueTiers,
    useCreateLeagueTier,
    useUpdateLeagueTier,
    useDeleteLeagueTier,
    type AdminLeagueTier,
} from "@/features/admin/hooks/use-admin";

interface TierDraft {
    name: string;
    color: string;
    order: number;
}

const emptyNewTier = { key: "", name: "", color: "#888888", order: 1 };

export default function AdminLeagueTiersPage() {
    const { data: tiers = [], isLoading } = useAdminLeagueTiers();
    const createTier = useCreateLeagueTier();
    const updateTier = useUpdateLeagueTier();
    const deleteTier = useDeleteLeagueTier();

    const [drafts, setDrafts] = useState<Record<string, TierDraft>>({});
    const [newTier, setNewTier] = useState({ ...emptyNewTier });
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    const draftFor = (tier: AdminLeagueTier): TierDraft =>
        drafts[tier.id] ?? { name: tier.name, color: tier.color, order: tier.order };

    const setDraft = (tier: AdminLeagueTier, patch: Partial<TierDraft>) =>
        setDrafts((d) => ({ ...d, [tier.id]: { ...draftFor(tier), ...patch } }));

    const isDirty = (tier: AdminLeagueTier) => {
        const d = drafts[tier.id];
        return !!d && (d.name !== tier.name || d.color !== tier.color || d.order !== tier.order);
    };

    const handleSave = (tier: AdminLeagueTier) => {
        const d = draftFor(tier);
        updateTier.mutate(
            { id: tier.id, name: d.name, color: d.color, order: d.order },
            { onSuccess: () => setDrafts((all) => { const { [tier.id]: _, ...rest } = all; return rest; }) }
        );
    };

    const handleDelete = (tier: AdminLeagueTier) => {
        if (confirmDeleteId !== tier.id) {
            setConfirmDeleteId(tier.id);
            return;
        }
        deleteTier.mutate(tier.id, { onSettled: () => setConfirmDeleteId(null) });
    };

    const handleCreate = () => {
        const key = newTier.key.trim().toLowerCase();
        if (!key || !newTier.name.trim()) return;
        createTier.mutate(
            { key, name: newTier.name.trim(), color: newTier.color, order: newTier.order },
            { onSuccess: () => setNewTier({ ...emptyNewTier, order: newTier.order + 1 }) }
        );
    };

    const mutationError =
        (createTier.error as Error | null)?.message ??
        (updateTier.error as Error | null)?.message ??
        (deleteTier.error as Error | null)?.message;

    return (
        <div>
            <div className="mb-2">
                <Link
                    href="/admin/leagues"
                    className="text-xs text-ink-3 hover:text-ink transition-colors"
                >
                    ← All leagues
                </Link>
            </div>
            <div className="mb-6">
                <h1 className="text-xl font-bold text-ink">League tiers</h1>
                <p className="text-xs text-ink-3 mt-0.5">
                    The promotion ladder runs from the lowest order (entry tier) to the highest (top tier).
                    The key is permanent; name, color, and order are editable.
                </p>
            </div>

            {mutationError && (
                <p className="text-xs text-red-500 mb-4">{mutationError}</p>
            )}

            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium w-20">Order</th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Key</th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Name</th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">Color</th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {[...tiers]
                            .sort((a, b) => a.order - b.order)
                            .map((tier) => {
                                const d = draftFor(tier);
                                return (
                                    <tr key={tier.id} className="border-b border-line hover:bg-bg-2">
                                        <td className="py-2 px-3">
                                            <input
                                                type="number"
                                                value={d.order}
                                                onChange={(e) => setDraft(tier, { order: Number(e.target.value) })}
                                                className="w-16 text-sm border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                            />
                                        </td>
                                        <td className="py-2 px-3">
                                            <code className="text-xs text-ink-3">{tier.key}</code>
                                        </td>
                                        <td className="py-2 px-3">
                                            <input
                                                value={d.name}
                                                onChange={(e) => setDraft(tier, { name: e.target.value })}
                                                className="w-40 text-sm border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                            />
                                        </td>
                                        <td className="py-2 px-3">
                                            <div className="flex items-center gap-2">
                                                <input
                                                    type="color"
                                                    value={d.color}
                                                    onChange={(e) => setDraft(tier, { color: e.target.value })}
                                                    className="w-8 h-8 rounded border border-line p-0.5"
                                                />
                                                <input
                                                    value={d.color}
                                                    onChange={(e) => setDraft(tier, { color: e.target.value })}
                                                    className="w-24 text-xs border border-line rounded px-2 py-1 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                                />
                                            </div>
                                        </td>
                                        <td className="py-2 px-3 text-right whitespace-nowrap">
                                            <button
                                                onClick={() => handleSave(tier)}
                                                disabled={!isDirty(tier) || updateTier.isPending}
                                                className="text-xs px-2 py-1 rounded border border-line text-ink-3 hover:text-ink hover:bg-bg-2 transition-colors disabled:opacity-40"
                                            >
                                                Save
                                            </button>
                                            <button
                                                onClick={() => handleDelete(tier)}
                                                onBlur={() => setConfirmDeleteId(null)}
                                                disabled={deleteTier.isPending}
                                                className={`text-xs px-2 py-1 rounded ml-1 transition-colors disabled:opacity-50 ${
                                                    confirmDeleteId === tier.id
                                                        ? "bg-red-500 text-white"
                                                        : "text-ink-3 hover:text-red-500"
                                                }`}
                                            >
                                                {confirmDeleteId === tier.id ? "Confirm?" : "Delete"}
                                            </button>
                                        </td>
                                    </tr>
                                );
                            })}
                    </tbody>
                </table>
            )}

            <div className="mt-6 p-4 border border-line rounded-xl bg-bg-2/50">
                <h2 className="text-sm font-semibold text-ink mb-3">Add tier</h2>
                <div className="flex flex-wrap items-end gap-3">
                    <label className="text-xs text-ink-3">
                        Order
                        <input
                            type="number"
                            value={newTier.order}
                            onChange={(e) => setNewTier({ ...newTier, order: Number(e.target.value) })}
                            className="block mt-1 w-16 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        />
                    </label>
                    <label className="text-xs text-ink-3">
                        Key (slug)
                        <input
                            value={newTier.key}
                            placeholder="platinum"
                            onChange={(e) => setNewTier({ ...newTier, key: e.target.value })}
                            className="block mt-1 w-32 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        />
                    </label>
                    <label className="text-xs text-ink-3">
                        Name
                        <input
                            value={newTier.name}
                            placeholder="Платина"
                            onChange={(e) => setNewTier({ ...newTier, name: e.target.value })}
                            className="block mt-1 w-40 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        />
                    </label>
                    <label className="text-xs text-ink-3">
                        Color
                        <span className="flex items-center gap-2 mt-1">
                            <input
                                type="color"
                                value={newTier.color}
                                onChange={(e) => setNewTier({ ...newTier, color: e.target.value })}
                                className="w-8 h-8 rounded border border-line p-0.5"
                            />
                            <input
                                value={newTier.color}
                                onChange={(e) => setNewTier({ ...newTier, color: e.target.value })}
                                className="w-24 text-xs border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                            />
                        </span>
                    </label>
                    <button
                        onClick={handleCreate}
                        disabled={createTier.isPending || !newTier.key.trim() || !newTier.name.trim()}
                        className="text-sm px-3 py-1.5 rounded-lg bg-indigo text-white hover:opacity-90 transition-opacity disabled:opacity-50"
                    >
                        {createTier.isPending ? "Adding..." : "Add tier"}
                    </button>
                </div>
            </div>
        </div>
    );
}
