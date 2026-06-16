"use client";

import { useState } from "react";
import {
    useExerciseTypePrompts,
    useUpdateExerciseTypePrompt,
    useExerciseTypeRewards,
    useUpdateExerciseTypeReward,
} from "@/features/admin/hooks/use-admin";
import { AI_EXERCISE_TYPES, TYPE_LABELS } from "@/features/admin/components/exercise-editors";

export default function AdminPromptsPage() {
    const { data: prompts = [], isLoading } = useExerciseTypePrompts();
    const updatePrompt = useUpdateExerciseTypePrompt();
    const { data: rewards = [] } = useExerciseTypeRewards();
    const updateReward = useUpdateExerciseTypeReward();

    // Local edits for base XP: map of exerciseType -> current input value
    const [xpDrafts, setXpDrafts] = useState<Record<string, number>>({});

    function getRewardXp(exerciseType: string, stored: number): number {
        return xpDrafts[exerciseType] ?? stored;
    }

    async function handleSaveReward(exerciseType: string) {
        await updateReward.mutateAsync({ exerciseType, baseXpReward: getRewardXp(exerciseType, 0) });
        setXpDrafts((prev) => { const next = { ...prev }; delete next[exerciseType]; return next; });
    }

    // Local edits: map of exerciseType -> current textarea value
    const [drafts, setDrafts] = useState<Record<string, string>>({});
    const [saved, setSaved] = useState<Record<string, boolean>>({});

    function getPromptText(exerciseType: string): string {
        if (drafts[exerciseType] !== undefined) return drafts[exerciseType];
        return prompts.find((p) => p.exerciseType === exerciseType)?.systemPrompt ?? "";
    }

    function setDraft(exerciseType: string, value: string) {
        setDrafts((prev) => ({ ...prev, [exerciseType]: value }));
        setSaved((prev) => ({ ...prev, [exerciseType]: false }));
    }

    async function handleSave(exerciseType: string) {
        await updatePrompt.mutateAsync({
            exerciseType,
            systemPrompt: getPromptText(exerciseType),
        });
        setSaved((prev) => ({ ...prev, [exerciseType]: true }));
        // Remove local draft — the query will now return the updated value
        setDrafts((prev) => { const next = { ...prev }; delete next[exerciseType]; return next; });
    }

    return (
        <div className="max-w-3xl">
            <div className="mb-6">
                <h1 className="text-xl font-semibold text-ink">AI Prompts</h1>
                <p className="text-sm text-ink-3 mt-1">
                    Each AI-evaluated exercise type has a <strong>global system prompt</strong> that is sent to the
                    AI grader for every exercise of that type. If an exercise also has a per-exercise
                    <code className="mx-1 px-1 bg-bg-2 rounded text-xs font-mono">ai_prompt</code>
                    field in its content, that text is appended as an addendum to the global prompt.
                </p>
            </div>

            {rewards.length > 0 && (
                <div className="bg-surface border border-line rounded-2xl p-5 mb-8">
                    <h2 className="text-sm font-semibold text-ink mb-1">Base XP per exercise type</h2>
                    <p className="text-xs text-ink-3 mb-4">
                        XP awarded when a user answers an exercise of this type correctly. Stored in the
                        database — no hardcoded values.
                    </p>
                    <div className="space-y-2">
                        {rewards.map((reward) => {
                            const isSaving = updateReward.isPending && updateReward.variables?.exerciseType === reward.exerciseType;
                            const dirty = xpDrafts[reward.exerciseType] !== undefined
                                && xpDrafts[reward.exerciseType] !== reward.baseXpReward;
                            return (
                                <div key={reward.exerciseType} className="flex items-center gap-3">
                                    <span className="text-sm text-ink w-40">
                                        {TYPE_LABELS[reward.exerciseType as keyof typeof TYPE_LABELS] ?? reward.exerciseType}
                                    </span>
                                    <span className="text-xs font-mono text-ink-3 w-32">{reward.exerciseType}</span>
                                    <input
                                        type="number"
                                        min={0}
                                        value={getRewardXp(reward.exerciseType, reward.baseXpReward)}
                                        onChange={(e) =>
                                            setXpDrafts((prev) => ({ ...prev, [reward.exerciseType]: Number(e.target.value) }))
                                        }
                                        className="w-24 text-sm border border-line rounded px-2 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                                    />
                                    <button
                                        onClick={() => handleSaveReward(reward.exerciseType)}
                                        disabled={isSaving || !dirty}
                                        className="px-3 py-1.5 text-xs bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-40 transition-colors"
                                    >
                                        {isSaving ? "Saving…" : "Save"}
                                    </button>
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}

            {isLoading && <p className="text-sm text-ink-3">Loading...</p>}

            <div className="space-y-6">
                {AI_EXERCISE_TYPES.map((type) => {
                    const isSaving = updatePrompt.isPending && updatePrompt.variables?.exerciseType === type;
                    const wasSaved = saved[type] ?? false;

                    return (
                        <div key={type} className="bg-surface border border-line rounded-2xl p-5">
                            <div className="flex items-center justify-between mb-2">
                                <div>
                                    <span className="text-sm font-medium text-ink">{TYPE_LABELS[type]}</span>
                                    <span className="ml-2 text-xs font-mono text-ink-3 px-1.5 py-0.5 bg-bg-2 rounded">
                                        {type}
                                    </span>
                                </div>
                                <div className="flex items-center gap-3">
                                    {wasSaved && (
                                        <span className="text-xs text-indigo">Saved</span>
                                    )}
                                    <button
                                        onClick={() => handleSave(type)}
                                        disabled={isSaving}
                                        className="px-3 py-1.5 text-xs bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-40 transition-colors"
                                    >
                                        {isSaving ? "Saving…" : "Save"}
                                    </button>
                                </div>
                            </div>

                            <textarea
                                rows={6}
                                className="w-full border border-line rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface resize-y"
                                value={getPromptText(type)}
                                onChange={(e) => setDraft(type, e.target.value)}
                                placeholder={`Global system prompt for all "${TYPE_LABELS[type]}" exercises…`}
                            />

                            <p className="text-[10px] text-ink-3 mt-1">
                                Per-exercise <code className="font-mono">ai_prompt</code> (set inside each exercise&apos;s content editor) is appended after this global prompt.
                            </p>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
