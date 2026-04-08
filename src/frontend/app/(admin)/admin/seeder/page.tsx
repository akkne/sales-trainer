"use client";

import { useRef, useState } from "react";
import { useImportSkills, type SkillsImportResult } from "@/lib/hooks/useAdmin";

const CSV_TEMPLATE = [
    "slug,title,icon_name,sort_order,sales_types",
    "cold-calling,Cold Calling,phone,1,b2b_saas|b2c",
    "objection-handling,Objection Handling,shield,2,b2b_saas|retail|b2c",
].join("\n");

const JSON_TEMPLATE = JSON.stringify(
    [
        {
            slug: "cold-calling",
            title: "Cold Calling",
            iconName: "phone",
            sortOrder: 1,
            salesTypes: ["b2b_saas", "b2c"],
        },
        {
            slug: "objection-handling",
            title: "Objection Handling",
            iconName: "shield",
            sortOrder: 2,
            salesTypes: ["b2b_saas", "retail", "b2c"],
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
    a.download = `skills_template.${format}`;
    a.click();
    URL.revokeObjectURL(url);
}

function StatBox({ label, value, accent }: { label: string; value: number; accent?: boolean }) {
    return (
        <div
            className={`rounded-2xl border px-4 py-3 text-center ${
                accent && value > 0 ? "border-primary-container bg-primary-container" : "border-outline-variant bg-surface-container-lowest"
            }`}
        >
            <div
                className={`text-2xl font-semibold ${
                    accent && value > 0 ? "text-primary" : "text-on-surface"
                }`}
            >
                {value}
            </div>
            <div className="text-xs text-on-surface-variant mt-0.5">{label}</div>
        </div>
    );
}

export default function SeederPage() {
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [selectedFile, setSelectedFile] = useState<File | null>(null);
    const [result, setResult] = useState<SkillsImportResult | null>(null);
    const importSkills = useImportSkills();

    function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
        const f = e.target.files?.[0] ?? null;
        setSelectedFile(f);
        setResult(null);
    }

    async function handleImport() {
        if (!selectedFile) return;
        const data = await importSkills.mutateAsync(selectedFile);
        setResult(data);
        setSelectedFile(null);
        if (fileInputRef.current) fileInputRef.current.value = "";
    }

    return (
        <div className="max-w-2xl">
            <h1 className="font-headline text-xl font-bold text-on-surface mb-1">Skills Seeder</h1>
            <p className="text-sm text-on-surface-variant mb-6">
                Bulk-import skills from a CSV or JSON file. Existing skills are updated by slug;
                new ones are created.
            </p>

            {/* Upload card */}
            <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-6 mb-6">
                <h2 className="text-sm font-medium text-on-surface mb-4">Upload file</h2>

                <div
                    className="border-2 border-dashed border-outline-variant rounded-md px-6 py-8 text-center cursor-pointer hover:border-outline transition-colors"
                    onClick={() => fileInputRef.current?.click()}
                >
                    <p className="text-sm text-on-surface-variant">
                        {selectedFile ? (
                            <span className="text-on-surface font-medium">{selectedFile.name}</span>
                        ) : (
                            <>
                                Click to select a{" "}
                                <span className="font-medium">.csv</span> or{" "}
                                <span className="font-medium">.json</span> file
                            </>
                        )}
                    </p>
                    {selectedFile && (
                        <p className="text-xs text-on-surface-variant mt-1">
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
                        disabled={!selectedFile || importSkills.isPending}
                        className="px-4 py-2 text-sm bg-primary text-on-primary rounded-xl hover:bg-primary-dim disabled:opacity-40 transition-colors"
                    >
                        {importSkills.isPending ? "Importing…" : "Import"}
                    </button>
                    <button
                        onClick={() => downloadTemplate("csv")}
                        className="px-4 py-2 text-sm border border-outline-variant rounded-xl text-on-surface-variant hover:bg-surface-container-low transition-colors"
                    >
                        Download CSV template
                    </button>
                    <button
                        onClick={() => downloadTemplate("json")}
                        className="px-4 py-2 text-sm border border-outline-variant rounded-xl text-on-surface-variant hover:bg-surface-container-low transition-colors"
                    >
                        Download JSON template
                    </button>
                </div>

                {importSkills.isError && (
                    <p className="mt-3 text-xs text-error">
                        {(importSkills.error as Error).message}
                    </p>
                )}
            </div>

            {/* Result */}
            {result && (
                <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-6 mb-6">
                    <h2 className="text-sm font-medium text-on-surface mb-4">Import result</h2>
                    <div className="grid grid-cols-2 gap-3 mb-4">
                        <StatBox label="Skills created" value={result.skillsCreated} accent />
                        <StatBox label="Skills updated" value={result.skillsUpdated} />
                    </div>
                    {result.errors.length > 0 ? (
                        <div className="mt-2">
                            <p className="text-xs font-medium text-error mb-1">
                                {result.errors.length} error{result.errors.length > 1 ? "s" : ""}:
                            </p>
                            <ul className="space-y-0.5">
                                {result.errors.map((e, i) => (
                                    <li key={i} className="text-xs text-error font-mono">
                                        {e}
                                    </li>
                                ))}
                            </ul>
                        </div>
                    ) : (
                        <p className="text-xs text-primary">Import completed without errors.</p>
                    )}
                </div>
            )}

            {/* Format reference */}
            <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-6">
                <h2 className="text-sm font-medium text-on-surface mb-3">File format</h2>

                <p className="text-xs text-on-surface-variant font-medium mb-1">CSV columns</p>
                <table className="w-full text-xs border-collapse mb-5">
                    <thead>
                        <tr className="border-b border-outline-variant">
                            <th className="text-left py-1.5 px-2 text-on-surface-variant font-medium">Column</th>
                            <th className="text-left py-1.5 px-2 text-on-surface-variant font-medium">Type</th>
                            <th className="text-left py-1.5 px-2 text-on-surface-variant font-medium">Notes</th>
                        </tr>
                    </thead>
                    <tbody className="text-on-surface">
                        {[
                            ["slug", "string", "Unique identifier — upsert key"],
                            ["title", "string", "Display title"],
                            ["icon_name", "string", "Icon name (e.g. phone, handshake)"],
                            ["sort_order", "integer", "Display order in skill tree"],
                            ["sales_types", "string", "Pipe-separated: b2b_saas|retail|b2c"],
                        ].map(([col, type, notes]) => (
                            <tr key={col} className="border-b border-surface-container">
                                <td className="py-1.5 px-2 font-mono text-on-surface">{col}</td>
                                <td className="py-1.5 px-2 text-on-surface-variant">{type}</td>
                                <td className="py-1.5 px-2 text-on-surface-variant">{notes}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>

                <p className="text-xs text-on-surface-variant font-medium mb-1">JSON format</p>
                <pre className="text-xs bg-surface-container-low border border-outline-variant rounded p-3 overflow-x-auto text-on-surface">
{`[
  {
    "slug": "cold-calling",
    "title": "Cold Calling",
    "iconName": "phone",
    "sortOrder": 1,
    "salesTypes": ["b2b_saas", "b2c"]
  }
]`}
                </pre>
            </div>
        </div>
    );
}
