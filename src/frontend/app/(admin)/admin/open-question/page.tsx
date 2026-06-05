"use client";

import { useEffect, useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import {
    useExerciseTypePrompts,
    useUpdateExerciseTypePrompt,
    type ExerciseTypePrompt,
} from "@/features/admin/hooks/use-admin";

interface GlobalContextData {
    contextText: string;
}

async function fetchGlobalContext(): Promise<GlobalContextData> {
    return apiClient.get<GlobalContextData>("/admin/open-question/global-context");
}

async function updateGlobalContext(text: string): Promise<GlobalContextData> {
    return apiClient.post<GlobalContextData>("/admin/open-question/global-context", { contextText: text });
}

// Exercise types that use AI evaluation
const AI_EXERCISE_TYPES = [
    { key: "free_text", label: "Free Text (legacy)" },
    { key: "open_question", label: "Open Question" },
    { key: "find_error", label: "Find Error" },
    { key: "rewrite_better", label: "Rewrite Better" },
    { key: "ai_dialog", label: "AI Dialog" },
    { key: "rate_call", label: "Rate Call" },
    { key: "written_answer", label: "Written Answer" },
];

export default function AdminOpenQuestionPage() {
    // Global context for legacy open_question
    const { data, isLoading, error } = useQuery({
        queryKey: ["open-question", "global-context"],
        queryFn: fetchGlobalContext,
    });

    const updateMutation = useMutation({
        mutationFn: updateGlobalContext,
    });

    const [text, setText] = useState("");

    useEffect(() => {
        if (data) {
            setText(data.contextText ?? "");
        }
    }, [data]);

    // Exercise type prompts
    const { data: typePrompts = [], isLoading: promptsLoading } = useExerciseTypePrompts();
    const updatePromptMutation = useUpdateExerciseTypePrompt();

    const [selectedType, setSelectedType] = useState<string>("");
    const [promptText, setPromptText] = useState("");

    // When selected type changes, load the prompt
    useEffect(() => {
        if (selectedType && typePrompts.length > 0) {
            const found = typePrompts.find((p) => p.exerciseType === selectedType);
            setPromptText(found?.systemPrompt ?? "");
        }
    }, [selectedType, typePrompts]);

    async function handleSaveTypePrompt() {
        if (!selectedType) return;
        await updatePromptMutation.mutateAsync({
            exerciseType: selectedType,
            systemPrompt: promptText,
        });
    }

    if (isLoading) {
        return (
            <div className="p-6">
                <h1 className="font-headline text-xl font-bold text-on-surface mb-6">AI Prompts Management</h1>
                <p className="text-on-surface-variant">Loading...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-6">
                <h1 className="font-headline text-xl font-bold text-on-surface mb-6">AI Prompts Management</h1>
                <p className="text-error">Error: {(error as Error).message}</p>
            </div>
        );
    }

    return (
        <div className="p-6 space-y-8">
            <div>
                <h1 className="font-headline text-xl font-bold text-on-surface mb-2">AI Prompts Management</h1>
                <p className="text-sm text-on-surface-variant">
                    Manage global AI prompts for exercise evaluation and open question context.
                </p>
            </div>

            {/* Exercise Type Prompts Section */}
            <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-5">
                <h2 className="text-lg font-semibold text-on-surface mb-2">Exercise Type Prompts</h2>
                <p className="text-sm text-on-surface-variant mb-4">
                    These are the global system prompts for AI-powered exercise types. Each exercise type uses this base prompt plus any per-exercise custom prompt.
                </p>

                <div className="grid grid-cols-2 gap-4 mb-4">
                    <label className="block">
                        <span className="text-sm font-medium text-on-surface">Exercise Type</span>
                        <select
                            className="mt-1 w-full border border-outline-variant rounded-xl bg-surface-container-low text-on-surface px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                            value={selectedType}
                            onChange={(e) => setSelectedType(e.target.value)}
                        >
                            <option value="">Select exercise type...</option>
                            {AI_EXERCISE_TYPES.map((type) => (
                                <option key={type.key} value={type.key}>
                                    {type.label}
                                </option>
                            ))}
                        </select>
                    </label>
                    <div className="flex items-end">
                        {selectedType && (
                            <span className="text-xs text-on-surface-variant">
                                {typePrompts.find((p) => p.exerciseType === selectedType)
                                    ? `Last updated: ${new Date(typePrompts.find((p) => p.exerciseType === selectedType)!.updatedAt).toLocaleDateString()}`
                                    : "No prompt saved yet"}
                            </span>
                        )}
                    </div>
                </div>

                {selectedType && (
                    <>
                        <label className="block">
                            <span className="text-sm font-medium text-on-surface">
                                System Prompt for {AI_EXERCISE_TYPES.find((t) => t.key === selectedType)?.label}
                            </span>
                            <textarea
                                value={promptText}
                                onChange={(e) => setPromptText(e.target.value)}
                                rows={10}
                                wrap="soft"
                                className="mt-2 w-full border border-outline-variant rounded-xl bg-surface-container-low text-on-surface px-4 py-2 text-sm font-mono whitespace-pre-wrap focus:outline-none focus:ring-2 focus:ring-primary resize-y"
                                placeholder="Enter the system prompt for this exercise type..."
                            />
                        </label>

                        <div className="flex gap-3 mt-4">
                            <button
                                onClick={handleSaveTypePrompt}
                                disabled={updatePromptMutation.isPending}
                                className="px-4 py-2 text-sm bg-primary text-on-primary rounded-xl hover:bg-primary-dim disabled:opacity-50 transition-colors"
                            >
                                {updatePromptMutation.isPending ? "Saving..." : "Save Prompt"}
                            </button>
                            {updatePromptMutation.isSuccess && (
                                <span className="text-sm text-primary flex items-center">Saved!</span>
                            )}
                        </div>
                    </>
                )}

                {promptsLoading && (
                    <p className="text-sm text-on-surface-variant mt-4">Loading prompts...</p>
                )}
            </div>

            {/* Open Question Global Context Section */}
            <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-5">
                <h2 className="text-lg font-semibold text-on-surface mb-2">Open Question — Global AI Context</h2>
                <p className="text-sm text-on-surface-variant mb-4">
                    This is the shared AI prompt that applies to ALL open question evaluations (legacy).
                    Describe the AI role, response guidelines, and general evaluation criteria here.
                </p>

                <label className="block">
                    <span className="text-sm font-medium text-on-surface">
                        Global AI Context (applies to every open question)
                    </span>
                    <textarea
                        value={text}
                        onChange={(e) => setText(e.target.value)}
                        rows={10}
                        wrap="soft"
                        className="mt-2 w-full border border-outline-variant rounded-xl bg-surface-container-low text-on-surface px-4 py-2 text-sm font-mono whitespace-pre-wrap focus:outline-none focus:ring-2 focus:ring-primary resize-y"
                        placeholder="You are a strict sales expert evaluator..."
                    />
                </label>

                <div className="flex gap-3 mt-4">
                    <button
                        onClick={async () => {
                            await updateMutation.mutateAsync(text);
                        }}
                        disabled={updateMutation.isPending}
                        className="px-4 py-2 text-sm bg-primary text-on-primary rounded-xl hover:bg-primary-dim disabled:opacity-50 transition-colors"
                    >
                        {updateMutation.isPending ? "Saving..." : "Save"}
                    </button>
                </div>
            </div>
        </div>
    );
}
