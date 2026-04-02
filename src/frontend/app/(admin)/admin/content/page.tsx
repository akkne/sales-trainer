"use client";

import { useRef, useState } from "react";
import {
    useAdminSkills,
    useImportLessons,
    type LessonsImportResult,
} from "@/lib/hooks/useAdmin";

const CSV_TEMPLATE = [
    "lesson_title,lesson_sort_order,lesson_difficulty,lesson_xp,exercise_type,exercise_sort_order,exercise_content_json",
    'Opening the Call,1,1,50,multiple_choice,1,"{""situation"":""You just dialed a prospect."",""question"":""Best opener?"",""options"":[""Hi boss"",""Hi I\'m Alex from Acme"",""Buy something?""],""correctOptionIndex"":1}"',
    'Opening the Call,1,1,50,fill_blank,2,"{""characterName"":""Prospect"",""characterLine"":""Who is this?"",""options"":[""Nobody."",""I\'m Alex from Acme."",""Please don\'t hang up!""],""correctOptionIndex"":1}"',
].join("\n");

const JSON_TEMPLATE = JSON.stringify(
    [
        {
            title: "Opening the Call",
            sortOrder: 1,
            difficultyLevel: 1,
            xpReward: 50,
            exercises: [
                {
                    type: "multiple_choice",
                    sortOrder: 1,
                    content: {
                        situation: "You just dialed a prospect.",
                        question: "Best opener?",
                        options: ["Hi boss", "Hi I'm Alex from Acme", "Buy something?"],
                        correctOptionIndex: 1,
                        explanation: "A clear, friendly opener sets the tone.",
                    },
                },
                {
                    type: "fill_blank",
                    sortOrder: 2,
                    content: {
                        characterName: "Prospect",
                        characterLine: "Who is this?",
                        options: ["Nobody.", "I'm Alex from Acme.", "Please don't hang up!"],
                        correctOptionIndex: 1,
                    },
                },
            ],
        },
    ],
    null,
    2
);

function downloadTemplate(format: "csv" | "json") {
    const content = format === "csv" ? CSV_TEMPLATE : JSON_TEMPLATE;
    const mimeType = format === "csv" ? "text/csv;charset=utf-8;" : "application/json";
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `lessons_template.${format}`;
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

export default function ContentPage() {
    const { data: skills = [], isLoading: skillsLoading } = useAdminSkills();
    const [selectedSkillId, setSelectedSkillId] = useState("");
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [result, setResult] = useState<LessonsImportResult | null>(null);

    const importLessons = useImportLessons(selectedSkillId);

    function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
        const f = e.target.files?.[0] ?? null;
        setSelectedFile(f);
        setResult(null);
    }

    async function handleImport() {
        if (!selectedFile || !selectedSkillId) return;
        const data = await importLessons.mutateAsync(selectedFile);
        setResult(data);
        setSelectedFile(null);
        if (fileInputRef.current) fileInputRef.current.value = "";
    }

    return (
        <div className="max-w-2xl">
            <h1 className="text-xl font-semibold text-gray-900 mb-1">Content Import</h1>
            <p className="text-sm text-gray-500 mb-6">
                Bulk-import lessons and exercises for a specific skill from a CSV or JSON file.
                Existing lessons are matched by title; exercises by sort order within the lesson.
            </p>

            {/* Skill selector */}
            <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
                <h2 className="text-sm font-medium text-gray-700 mb-3">Select skill</h2>
                {skillsLoading ? (
                    <p className="text-sm text-gray-400">Loading skills…</p>
                ) : skills.length === 0 ? (
                    <p className="text-sm text-gray-400">
                        No skills found. Add skills first via the Skills Seeder.
                    </p>
                ) : (
                    <select
                        className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                        value={selectedSkillId}
                        onChange={(e) => {
                            setSelectedSkillId(e.target.value);
                            setResult(null);
                        }}
                    >
                        <option value="">— choose a skill —</option>
                        {[...skills]
                            .sort((a, b) => a.sortOrder - b.sortOrder)
                            .map((s) => (
                                <option key={s.id} value={s.id}>
                                    {s.iconName} · {s.title}
                                </option>
                            ))}
                    </select>
                )}
            </div>

            {/* Upload card */}
            <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
                <h2 className="text-sm font-medium text-gray-700 mb-4">Upload file</h2>

                <div
                    className={`border-2 border-dashed rounded-md px-6 py-8 text-center transition-colors ${
                        selectedSkillId
                            ? "border-gray-300 cursor-pointer hover:border-gray-400"
                            : "border-gray-200 cursor-not-allowed opacity-50"
                    }`}
                    onClick={() => selectedSkillId && fileInputRef.current?.click()}
                >
                    <p className="text-sm text-gray-500">
                        {selectedFile ? (
                            <span className="text-gray-800 font-medium">{selectedFile.name}</span>
                        ) : (
                            <>
                                Click to select a{" "}
                                <span className="font-medium">.csv</span> or{" "}
                                <span className="font-medium">.json</span> file
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
                    accept=".csv,.json"
                    className="hidden"
                    onChange={handleFileChange}
                />

                <div className="mt-4 flex items-center gap-3 flex-wrap">
                    <button
                        onClick={handleImport}
                        disabled={!selectedFile || !selectedSkillId || importLessons.isPending}
                        className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-40 transition-colors"
                    >
                        {importLessons.isPending ? "Importing…" : "Import"}
                    </button>
                    <button
                        onClick={() => downloadTemplate("csv")}
                        className="px-4 py-2 text-sm border border-gray-300 rounded-md text-gray-600 hover:bg-gray-50 transition-colors"
                    >
                        Download CSV template
                    </button>
                    <button
                        onClick={() => downloadTemplate("json")}
                        className="px-4 py-2 text-sm border border-gray-300 rounded-md text-gray-600 hover:bg-gray-50 transition-colors"
                    >
                        Download JSON template
                    </button>
                </div>

                {importLessons.isError && (
                    <p className="mt-3 text-xs text-red-500">
                        {(importLessons.error as Error).message}
                    </p>
                )}
            </div>

            {/* Result */}
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

            {/* Format reference */}
            <div className="bg-white border border-gray-200 rounded-lg p-6">
                <h2 className="text-sm font-medium text-gray-700 mb-3">File format</h2>

                <p className="text-xs text-gray-500 font-medium mb-1">CSV columns</p>
                <table className="w-full text-xs border-collapse mb-5">
                    <thead>
                        <tr className="border-b border-gray-200">
                            <th className="text-left py-1.5 px-2 text-gray-500 font-medium">
                                Column
                            </th>
                            <th className="text-left py-1.5 px-2 text-gray-500 font-medium">
                                Type
                            </th>
                            <th className="text-left py-1.5 px-2 text-gray-500 font-medium">
                                Notes
                            </th>
                        </tr>
                    </thead>
                    <tbody className="text-gray-700">
                        {[
                            ["lesson_title", "string", "Upsert key within the skill"],
                            ["lesson_sort_order", "integer", "Order within the skill"],
                            ["lesson_difficulty", "integer", "1 = easy, 3 = hard"],
                            ["lesson_xp", "integer", "XP awarded on completion"],
                            ["exercise_type", "string", "multiple_choice | fill_blank | free_text"],
                            ["exercise_sort_order", "integer", "Order within the lesson (upsert key)"],
                            ["exercise_content_json", "JSON", "Must be valid JSON"],
                        ].map(([col, type, notes]) => (
                            <tr key={col} className="border-b border-gray-100">
                                <td className="py-1.5 px-2 font-mono text-gray-800">{col}</td>
                                <td className="py-1.5 px-2 text-gray-500">{type}</td>
                                <td className="py-1.5 px-2 text-gray-500">{notes}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>

                <p className="text-xs text-gray-500 font-medium mb-1">JSON format</p>
                <pre className="text-xs bg-gray-50 border border-gray-200 rounded p-3 overflow-x-auto text-gray-700">
{`[
  {
    "title": "Lesson title",
    "sortOrder": 1,
    "difficultyLevel": 1,
    "xpReward": 50,
    "exercises": [
      {
        "type": "multiple_choice",
        "sortOrder": 1,
        "content": { ... }
      }
    ]
  }
]`}
                </pre>
            </div>
        </div>
    );
}
