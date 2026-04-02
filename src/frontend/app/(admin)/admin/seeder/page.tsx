"use client";

import { useRef, useState } from "react";
import { useImportCsv, type SeederImportResult } from "@/lib/hooks/useAdmin";

const CSV_TEMPLATE = [
    "skill_slug,skill_title,skill_icon,skill_sort_order,skill_sales_types,lesson_title,lesson_sort_order,lesson_difficulty,lesson_xp,exercise_type,exercise_sort_order,exercise_content_json",
    'cold-calling,Cold Calling,phone,1,b2b_saas|b2c,Opening the Call,1,1,50,multiple_choice,1,"{""situation"":""You just dialed a prospect."",""question"":""What is the best opener?"",""options"":[""Hi, can I speak to the boss?"",""Hi, my name is Alex from Acme — is now a good time?"",""Do you want to buy something?""],""correctOptionIndex"":1,""explanation"":""A friendly, clear opener sets the tone.""}"',
    'cold-calling,Cold Calling,phone,1,b2b_saas|b2c,Opening the Call,1,1,50,fill_blank,2,"{""characterName"":""Prospect"",""characterLine"":""Who is this and why are you calling?"",""options"":[""I\'m nobody important."",""I\'m Alex from Acme — I help teams like yours cut churn by 30%."",""Please don\'t hang up!""],""correctOptionIndex"":1}"',
].join("\n");

function downloadTemplate() {
    const blob = new Blob([CSV_TEMPLATE], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "seeder_template.csv";
    a.click();
    URL.revokeObjectURL(url);
}

function StatBox({ label, value, accent }: { label: string; value: number; accent?: boolean }) {
    return (
        <div className={`rounded-md border px-4 py-3 text-center ${accent && value > 0 ? "border-green-200 bg-green-50" : "border-gray-200 bg-white"}`}>
            <div className={`text-2xl font-semibold ${accent && value > 0 ? "text-green-700" : "text-gray-800"}`}>{value}</div>
            <div className="text-xs text-gray-500 mt-0.5">{label}</div>
        </div>
    );
}

export default function SeederPage() {
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [result, setResult] = useState<SeederImportResult | null>(null);
    const importCsv = useImportCsv();

    function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
        const f = e.target.files?.[0] ?? null;
        setSelectedFile(f);
        setResult(null);
    }

    async function handleImport() {
        if (!selectedFile) return;
        const data = await importCsv.mutateAsync(selectedFile);
        setResult(data);
        setSelectedFile(null);
        if (fileInputRef.current) fileInputRef.current.value = "";
    }

    return (
        <div className="max-w-2xl">
            <h1 className="text-xl font-semibold text-gray-900 mb-1">Content Seeder</h1>
            <p className="text-sm text-gray-500 mb-6">
                Import skills, lessons, and exercises from a CSV file. Existing records are
                updated by slug / title; new ones are created.
            </p>

            {/* Upload card */}
            <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
                <h2 className="text-sm font-medium text-gray-700 mb-4">Upload CSV</h2>

                <div
                    className="border-2 border-dashed border-gray-300 rounded-md px-6 py-8 text-center cursor-pointer hover:border-gray-400 transition-colors"
                    onClick={() => fileInputRef.current?.click()}
                >
                    <p className="text-sm text-gray-500">
                        {selectedFile ? (
                            <span className="text-gray-800 font-medium">{selectedFile.name}</span>
                        ) : (
                            <>Click to select a <span className="font-medium">.csv</span> file</>
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
                    accept=".csv"
                    className="hidden"
                    onChange={handleFileChange}
                />

                <div className="mt-4 flex items-center gap-3">
                    <button
                        onClick={handleImport}
                        disabled={!selectedFile || importCsv.isPending}
                        className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-40 transition-colors"
                    >
                        {importCsv.isPending ? "Importing…" : "Import"}
                    </button>
                    <button
                        onClick={downloadTemplate}
                        className="px-4 py-2 text-sm border border-gray-300 rounded-md text-gray-600 hover:bg-gray-50 transition-colors"
                    >
                        Download template
                    </button>
                </div>

                {importCsv.isError && (
                    <p className="mt-3 text-xs text-red-500">
                        {(importCsv.error as Error).message}
                    </p>
                )}
            </div>

            {/* Result */}
            {result && (
                <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
                    <h2 className="text-sm font-medium text-gray-700 mb-4">Import result</h2>
                    <div className="grid grid-cols-3 gap-3 mb-4">
                        <StatBox label="Skills created" value={result.skillsCreated} accent />
                        <StatBox label="Skills updated" value={result.skillsUpdated} />
                        <StatBox label="Lessons created" value={result.lessonsCreated} accent />
                        <StatBox label="Lessons updated" value={result.lessonsUpdated} />
                        <StatBox label="Exercises created" value={result.exercisesCreated} accent />
                        <StatBox label="Exercises updated" value={result.exercisesUpdated} />
                    </div>
                    {result.errors.length > 0 && (
                        <div className="mt-2">
                            <p className="text-xs font-medium text-red-600 mb-1">
                                {result.errors.length} row error{result.errors.length > 1 ? "s" : ""}:
                            </p>
                            <ul className="space-y-0.5">
                                {result.errors.map((e, i) => (
                                    <li key={i} className="text-xs text-red-500 font-mono">{e}</li>
                                ))}
                            </ul>
                        </div>
                    )}
                    {result.errors.length === 0 && (
                        <p className="text-xs text-green-600">Import completed without errors.</p>
                    )}
                </div>
            )}

            {/* Format reference */}
            <div className="bg-white border border-gray-200 rounded-lg p-6">
                <h2 className="text-sm font-medium text-gray-700 mb-3">CSV format</h2>
                <p className="text-xs text-gray-500 mb-3">
                    One row = one exercise. Skills and lessons are auto-created or updated.
                    Fields with commas or quotes must be wrapped in double-quotes; internal
                    double-quotes are escaped as <code className="bg-gray-100 px-1 rounded">{`""`}</code>.
                </p>
                <table className="w-full text-xs border-collapse">
                    <thead>
                        <tr className="border-b border-gray-200">
                            <th className="text-left py-1.5 px-2 text-gray-500 font-medium">Column</th>
                            <th className="text-left py-1.5 px-2 text-gray-500 font-medium">Type</th>
                            <th className="text-left py-1.5 px-2 text-gray-500 font-medium">Notes</th>
                        </tr>
                    </thead>
                    <tbody className="text-gray-700">
                        {[
                            ["skill_slug", "string", "Unique identifier for the skill (upsert key)"],
                            ["skill_title", "string", "Display title"],
                            ["skill_icon", "string", "Icon name (e.g. phone, handshake)"],
                            ["skill_sort_order", "integer", "Display order in skill tree"],
                            ["skill_sales_types", "string", "Pipe-separated: b2b_saas|retail|b2c …"],
                            ["lesson_title", "string", "Upsert key within the skill"],
                            ["lesson_sort_order", "integer", "Order within the skill"],
                            ["lesson_difficulty", "integer", "1 = easy, 3 = hard"],
                            ["lesson_xp", "integer", "XP awarded on completion"],
                            ["exercise_type", "string", "multiple_choice | fill_blank | free_text"],
                            ["exercise_sort_order", "integer", "Order within the lesson (upsert key)"],
                            ["exercise_content_json", "JSON", "See API_CONTRACTS.md for shape per type"],
                        ].map(([col, type, notes]) => (
                            <tr key={col} className="border-b border-gray-100">
                                <td className="py-1.5 px-2 font-mono text-gray-800">{col}</td>
                                <td className="py-1.5 px-2 text-gray-500">{type}</td>
                                <td className="py-1.5 px-2 text-gray-500">{notes}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
