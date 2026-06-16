"use client";

import { useState } from "react";
import {
    useGamificationSettings,
    useUpdateGamificationSettings,
    useStreakMilestones,
    useCreateStreakMilestone,
    useUpdateStreakMilestone,
    useDeleteStreakMilestone,
    GamificationSettings,
} from "@/features/admin/hooks/use-admin";

export default function AdminGamificationPage() {
    return (
        <div className="max-w-2xl">
            <div className="mb-6">
                <h1 className="text-xl font-semibold text-ink">Gamification</h1>
                <p className="text-sm text-ink-3 mt-1">
                    XP goals and streak rewards. All values are stored in the database — there are no
                    hardcoded constants. Per-exercise XP lives on the <strong>AI Prompts</strong> page; the
                    dialog XP multiplier &amp; criterion weights live on the <strong>Dialog</strong> page.
                </p>
            </div>

            <GoalsCard />
            <StreakMilestonesCard />
        </div>
    );
}

function GoalsCard() {
    const { data: settings } = useGamificationSettings();
    const updateSettings = useUpdateGamificationSettings();
    const [form, setForm] = useState<GamificationSettings | null>(null);

    const current = form ?? settings ?? null;
    if (!current) return null;

    const fields: { key: "dailyXpGoal" | "weeklyXpGoal"; label: string }[] = [
        { key: "dailyXpGoal", label: "Daily XP goal" },
        { key: "weeklyXpGoal", label: "Weekly XP goal" },
    ];

    return (
        <div className="bg-surface border border-line rounded-2xl p-5 mb-6">
            <h2 className="text-sm font-semibold text-ink mb-4">XP goals</h2>
            <div className="flex flex-wrap gap-4 items-end">
                {fields.map(({ key, label }) => (
                    <label key={key} className="text-xs text-ink-3">
                        {label}
                        <input
                            type="number"
                            min={1}
                            value={current[key]}
                            onChange={(e) => setForm({ ...current, [key]: Number(e.target.value) })}
                            className="block mt-1 w-32 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                        />
                    </label>
                ))}
            </div>
            <div className="flex gap-2 mt-4 items-center">
                <button
                    onClick={() => updateSettings.mutate(current)}
                    disabled={updateSettings.isPending || current.dailyXpGoal <= 0 || current.weeklyXpGoal <= 0}
                    className="text-sm px-3 py-1.5 rounded-lg bg-indigo text-white hover:opacity-90 transition-opacity disabled:opacity-50"
                >
                    {updateSettings.isPending ? "Saving..." : "Save"}
                </button>
                {form && (
                    <button
                        onClick={() => setForm(null)}
                        className="text-sm px-3 py-1.5 rounded-lg border border-line text-ink-3 hover:text-ink transition-colors"
                    >
                        Reset
                    </button>
                )}
                {updateSettings.isError && (
                    <span className="text-xs text-red-500">{(updateSettings.error as Error).message}</span>
                )}
            </div>
        </div>
    );
}

function StreakMilestonesCard() {
    const { data: milestones = [] } = useStreakMilestones();
    const createMilestone = useCreateStreakMilestone();
    const updateMilestone = useUpdateStreakMilestone();
    const deleteMilestone = useDeleteStreakMilestone();

    const [newDayCount, setNewDayCount] = useState(0);
    const [newXp, setNewXp] = useState(0);
    const [drafts, setDrafts] = useState<Record<string, { dayCount: number; xpReward: number }>>({});

    const createError = createMilestone.error as Error | null;

    function draftFor(id: string, dayCount: number, xpReward: number) {
        return drafts[id] ?? { dayCount, xpReward };
    }

    async function handleAdd() {
        if (newDayCount <= 0) return;
        await createMilestone.mutateAsync({ dayCount: newDayCount, xpReward: newXp });
        setNewDayCount(0);
        setNewXp(0);
    }

    async function handleUpdate(id: string, dayCount: number, xpReward: number) {
        const draft = draftFor(id, dayCount, xpReward);
        await updateMilestone.mutateAsync({ id, dayCount: draft.dayCount, xpReward: draft.xpReward });
        setDrafts((prev) => { const next = { ...prev }; delete next[id]; return next; });
    }

    return (
        <div className="bg-surface border border-line rounded-2xl p-5">
            <h2 className="text-sm font-semibold text-ink mb-1">Streak milestones</h2>
            <p className="text-xs text-ink-3 mb-4">
                A one-off XP bonus awarded the first time a user&apos;s daily streak reaches the given day count.
            </p>

            <div className="space-y-2 mb-5">
                {milestones.map((m) => {
                    const draft = draftFor(m.id, m.dayCount, m.xpReward);
                    const dirty = draft.dayCount !== m.dayCount || draft.xpReward !== m.xpReward;
                    return (
                        <div key={m.id} className="flex items-center gap-3">
                            <label className="text-xs text-ink-3">
                                Days
                                <input
                                    type="number"
                                    min={1}
                                    value={draft.dayCount}
                                    onChange={(e) =>
                                        setDrafts((prev) => ({ ...prev, [m.id]: { ...draft, dayCount: Number(e.target.value) } }))
                                    }
                                    className="block mt-1 w-24 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                                />
                            </label>
                            <label className="text-xs text-ink-3">
                                XP reward
                                <input
                                    type="number"
                                    min={0}
                                    value={draft.xpReward}
                                    onChange={(e) =>
                                        setDrafts((prev) => ({ ...prev, [m.id]: { ...draft, xpReward: Number(e.target.value) } }))
                                    }
                                    className="block mt-1 w-24 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                                />
                            </label>
                            <button
                                onClick={() => handleUpdate(m.id, m.dayCount, m.xpReward)}
                                disabled={!dirty || updateMilestone.isPending}
                                className="self-end px-3 py-1.5 text-xs bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-40 transition-colors"
                            >
                                Save
                            </button>
                            <button
                                onClick={() => deleteMilestone.mutate(m.id)}
                                disabled={deleteMilestone.isPending}
                                className="self-end px-3 py-1.5 text-xs rounded-md border border-line text-red-500 hover:bg-bg-2 disabled:opacity-40 transition-colors"
                            >
                                Delete
                            </button>
                        </div>
                    );
                })}
                {milestones.length === 0 && (
                    <p className="text-xs text-ink-3">No milestones yet — add one below.</p>
                )}
            </div>

            <div className="flex items-end gap-3 border-t border-line pt-4">
                <label className="text-xs text-ink-3">
                    Days
                    <input
                        type="number"
                        min={1}
                        value={newDayCount}
                        onChange={(e) => setNewDayCount(Number(e.target.value))}
                        className="block mt-1 w-24 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                    />
                </label>
                <label className="text-xs text-ink-3">
                    XP reward
                    <input
                        type="number"
                        min={0}
                        value={newXp}
                        onChange={(e) => setNewXp(Number(e.target.value))}
                        className="block mt-1 w-24 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                    />
                </label>
                <button
                    onClick={handleAdd}
                    disabled={createMilestone.isPending || newDayCount <= 0}
                    className="px-3 py-1.5 text-xs bg-indigo text-white rounded-md hover:opacity-90 disabled:opacity-40 transition-colors"
                >
                    + Add milestone
                </button>
            </div>
            {createError && <p className="text-xs text-red-500 mt-2">{createError.message}</p>}
        </div>
    );
}
