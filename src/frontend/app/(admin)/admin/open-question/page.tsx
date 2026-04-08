"use client";

import { useEffect, useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

interface GlobalContextData {
    contextText: string;
}

async function fetchGlobalContext(): Promise<GlobalContextData> {
    return apiClient.get<GlobalContextData>("/admin/open-question/global-context");
}

async function updateGlobalContext(text: string): Promise<GlobalContextData> {
    return apiClient.post<GlobalContextData>("/admin/open-question/global-context", { contextText: text });
}

export default function AdminOpenQuestionPage() {
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

    if (isLoading) {
        return (
            <div className="p-6">
                <h1 className="font-headline text-xl font-bold text-on-surface mb-6">Open Question — Global AI Context</h1>
                <p className="text-on-surface-variant">Loading...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-6">
                <h1 className="font-headline text-xl font-bold text-on-surface mb-6">Open Question — Global AI Context</h1>
                <p className="text-error">Error: {(error as Error).message}</p>
            </div>
        );
    }

    return (
        <div className="p-6">
            <h1 className="font-headline text-xl font-bold text-on-surface mb-2">Open Question — Global AI Context</h1>
            <p className="text-sm text-on-surface-variant mb-6">
                This is the shared AI prompt that applies to ALL open question evaluations.
                Describe the AI role, response guidelines, and general evaluation criteria here.
            </p>

            <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-5">
                <label className="block">
                    <span className="text-sm font-medium text-on-surface">
                        Global AI Context (applies to every open question)
                    </span>
                    <textarea
                        value={text}
                        onChange={(e) => setText(e.target.value)}
                        rows={12}
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
