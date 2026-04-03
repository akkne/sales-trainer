"use client";

import { useRef, useState } from "react";
import { useImportLessonsBulk, type LessonsImportResult } from "@/lib/hooks/useAdmin";

const JSON_TEMPLATE = JSON.stringify(
    [
        {
            skillSlug: "cold-calls",
            title: "Подготовка к звонку",
            sortOrder: 1,
            xpReward: 60,
        },
        {
            skillSlug: "cold-calls",
            title: "Первые секунды разговора",
            sortOrder: 2,
            xpReward: 60,
        },
        {
            skillSlug: "objection-handling",
            title: "Почему клиенты возражают",
            sortOrder: 1,
            xpReward: 70,
        },
    ],
    null,
    2
);

function downloadTemplate() {
    const blob = new Blob([JSON_TEMPLATE], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "bulk_lessons_template.json";
    a.click();
    URL.revokeObjectURL(url);
}

function StatBox({ label, value, accent }: { label: string; value: number; accent?: boolean }) {
    return (
        <div
            className={`rounded-md border px-4 py-3 text-center ${
                accent && value > 0 ? "border-green-200 bg-green-50" : "border-gray-200 bg-white"
            }`}
        >
            <div
                className={`text-2xl font-semibold ${
                    accent && value > 0 ? "text-green-700" : "text-gray-800"
                }`}
            >
                {value}
            </div>
            <div className="text-xs text-gray-500 mt-0.5">{label}</div>
        </div>
    );
}

export default function BulkLessonsPage() {
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [result, setResult] = useState<LessonsImportResult | null>(null);
    const importBulk = useImportLessonsBulk();

    function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
        const f = e.target.files?.[0] ?? null;
        setSelectedFile(f);
        setResult(null);
    }

    async function handleImport() {
        if (!selectedFile) return;
        const data = await importBulk.mutateAsync(selectedFile);
        setResult(data);
        setSelectedFile(null);
        if (fileInputRef.current) fileInputRef.current.value = "";
    }

    return (
        <div className="max-w-2xl">
            <h1 className="text-xl font-semibold text-gray-900 mb-1">Bulk Lessons Import</h1>
            <p className="text-sm text-gray-500 mb-6">
                Import lessons for multiple skills at once. Skills are matched by{" "}
                <span className="font-mono">skillSlug</span>. Lessons are upserted by title within each skill.{" "}
                <span className="font-medium">difficultyLevel</span> defaults to 1 if omitted.
            </p>

            <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
                <h2 className="text-sm font-medium text-gray-700 mb-4">Upload file</h2>

                <div
                    className="border-2 border-dashed border-gray-300 rounded-md px-6 py-8 text-center cursor-pointer hover:border-gray-400 transition-colors"
                    onClick={() => fileInputRef.current?.click()}
                >
                    <p className="text-sm text-gray-500">
                        {selectedFile ? (
                            <span className="text-gray-800 font-medium">{selectedFile.name}</span>
                        ) : (
                            <>
                                Click to select a <span className="font-medium">.json</span> file
                            </>
                        )}
                    </p>
                    {selectedFile && (
                        <p className="text-xs text-gray-400 mt-1">
                            {(selectedFile.size / 1024).toFixed(1)} KB
                        </p>
                    )}
                </div>
                <input
                    ref={fileInputRef}
                    type="file"
                    accept=".json"
                    className="hidden"
                    onChange={handleFileChange}
                />

                <div className="mt-4 flex items-center gap-3 flex-wrap">
                    <button
                        onClick={handleImport}
                        disabled={!selectedFile || importBulk.isPending}
                        className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-40 transition-colors"
                    >
                        {importBulk.isPending ? "Importing…" : "Import"}
                    </button>
                    <button
                        onClick={downloadTemplate}
                        className="px-4 py-2 text-sm border border-gray-300 rounded-md text-gray-600 hover:bg-gray-50 transition-colors"
                    >
                        Download JSON template
                    </button>
                </div>

                {importBulk.isError && (
                    <p className="mt-3 text-xs text-red-500">
                        {(importBulk.error as Error).message}
                    </p>
                )}
            </div>

            {result && (
                <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
                    <h2 className="text-sm font-medium text-gray-700 mb-4">Import result</h2>
                    <div className="grid grid-cols-2 gap-3 mb-4">
                        <StatBox label="Lessons created" value={result.lessonsCreated} accent />
                        <StatBox label="Lessons updated" value={result.lessonsUpdated} />
                        <StatBox label="Exercises created" value={result.exercisesCreated} accent />
                        <StatBox label="Exercises updated" value={result.exercisesUpdated} />
                    </div>
                    {result.errors.length > 0 ? (
                        <div className="mt-2">
                            <p className="text-xs font-medium text-red-600 mb-1">
                                {result.errors.length} error{result.errors.length > 1 ? "s" : ""}:
                            </p>
                            <ul className="space-y-0.5">
                                {result.errors.map((e, i) => (
                                    <li key={i} className="text-xs text-red-500 font-mono">
                                        {e}
                                    </li>
                                ))}
                            </ul>
                        </div>
                    ) : (
                        <p className="text-xs text-green-600">Import completed without errors.</p>
                    )}
                </div>
            )}

            <div className="bg-white border border-gray-200 rounded-lg p-6">
                <h2 className="text-sm font-medium text-gray-700 mb-3">JSON format</h2>
                <pre className="text-xs bg-gray-50 border border-gray-200 rounded p-3 overflow-x-auto text-gray-700">
{`[
  {
    "skillSlug": "cold-calls",     // must match an existing skill slug
    "title": "Lesson title",       // upsert key within the skill
    "sortOrder": 1,
    "xpReward": 60,
    "difficultyLevel": 1           // optional, defaults to 1
  }
]`}
                </pre>
            </div>
        </div>
    );
}
