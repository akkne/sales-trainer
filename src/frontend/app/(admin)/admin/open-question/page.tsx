"use client";

import { use, useState } from "react";
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
    const [hasChanges, setHasChanges] = useState(false);

    if (isLoading) {
        return (
            <div className="p-6">
                <h1 className="text-2xl font-bold mb-6">Open Question — Global AI Context</h1>
                <p className="text-gray-500">Loading...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="p-6">
                <h1 className="text-2xl font-bold mb-6">Open Question — Global AI Context</h1>
                <p className="text-red-500">Error: {(error as Error).message}</p>
            </div>
        );
    }

    const initialText = data?.contextText ?? "";

    return (
        <div className="p-6">
            <h1 className="text-2xl font-bold mb-2">Open Question — Global AI Context</h1>
            <p className="text-sm text-gray-500 mb-6">
                This is the shared AI prompt that applies to ALL open question evaluations.
                Describe the AI role, response guidelines, and general evaluation criteria here.
            </p>

            <div className="bg-white border border-gray-200 rounded-lg p-5">
                <label className="block">
                    <span className="text-sm font-medium text-gray-700">
                        Global AI Context (applies to every open question)
                    </span>
                    <textarea
                        value={hasChanges ? text : initialText}
                        onChange={(e) => {
                            setText(e.target.value);
                            setHasChanges(true);
                        }}
                        rows={12}
                        className="mt-2 w-full border border-gray-300 rounded-md px-4 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                        placeholder="You are a strict sales expert evaluator..."
                    />
                </label>

                <div className="flex gap-3 mt-4">
                    <button
                        onClick={async () => {
                            await updateMutation.mutateAsync(hasChanges ? text : initialText);
                            setHasChanges(false);
                        }}
                        disabled={updateMutation.isPending}
                        className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                    >
                        {updateMutation.isPending ? "Saving..." : "Save"}
                    </button>
                </div>
            </div>
        </div>
    );
}
