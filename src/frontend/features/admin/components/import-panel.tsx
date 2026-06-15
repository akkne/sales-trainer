"use client";

import { useState } from "react";

export interface ImportSummary {
    created: number;
    updated: number;
    errors: string[];
}

interface ImportPanelProps {
    /** Heading shown at the top of the panel. */
    title: string;
    /** Optional one-line hint under the title. */
    description?: string;
    /** Value serialized into the downloadable template file. */
    templateData: unknown;
    /** File name used for the downloaded template. */
    templateFilename: string;
    /**
     * Performs the import. Receives the raw JSON text and its parsed value
     * (already validated to be syntactically valid JSON). Returns a normalized
     * summary, or throws to surface an error message.
     */
    onImport: (raw: { text: string; parsed: unknown }) => Promise<ImportSummary>;
    /** Optional client-side validation run before calling onImport. */
    validate?: (parsed: unknown) => string[];
}

function downloadJson(data: unknown, filename: string): void {
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
}

/**
 * Reusable JSON import panel: upload a .json file OR paste JSON, download a
 * canonical template, and see created/updated/error counts. Used across all
 * admin content entities so importing feels the same everywhere.
 */
export function ImportPanel({
    title,
    description,
    templateData,
    templateFilename,
    onImport,
    validate,
}: ImportPanelProps) {
    const [pasted, setPasted] = useState("");
    const [fileName, setFileName] = useState<string | null>(null);
    const [isImporting, setIsImporting] = useState(false);
    const [clientErrors, setClientErrors] = useState<string[]>([]);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);
    const [summary, setSummary] = useState<ImportSummary | null>(null);

    function reset() {
        setClientErrors([]);
        setErrorMessage(null);
        setSummary(null);
    }

    async function handleFile(event: React.ChangeEvent<HTMLInputElement>) {
        const file = event.target.files?.[0];
        event.target.value = ""; // allow re-selecting the same file
        if (!file) return;
        const text = await file.text();
        setPasted(text);
        setFileName(file.name);
        reset();
    }

    async function runImport() {
        reset();
        const text = pasted.trim();
        if (!text) {
            setErrorMessage("Paste JSON or choose a file first.");
            return;
        }

        let parsed: unknown;
        try {
            parsed = JSON.parse(text);
        } catch (error) {
            setErrorMessage(`Invalid JSON: ${(error as Error).message}`);
            return;
        }

        if (validate) {
            const problems = validate(parsed);
            if (problems.length > 0) {
                setClientErrors(problems);
                return;
            }
        }

        setIsImporting(true);
        try {
            const result = await onImport({ text, parsed });
            setSummary(result);
        } catch (error) {
            setErrorMessage((error as Error).message || "Import failed.");
        } finally {
            setIsImporting(false);
        }
    }

    return (
        <div className="bg-surface border border-line rounded-2xl p-5 mb-6">
            <div className="flex items-center justify-between mb-1">
                <h2 className="text-sm font-medium text-ink">{title}</h2>
                <button
                    onClick={() => downloadJson(templateData, templateFilename)}
                    className="text-xs text-indigo hover:underline"
                >
                    ↓ Download template
                </button>
            </div>
            {description && <p className="text-xs text-ink-3 mb-3">{description}</p>}

            <div className="flex items-center gap-3 mb-3">
                <label className="px-3 py-1.5 text-xs border border-line text-ink-3 rounded-md hover:bg-bg-2 cursor-pointer transition-colors">
                    Choose file…
                    <input type="file" accept=".json,application/json" onChange={handleFile} className="hidden" />
                </label>
                {fileName && <span className="text-xs text-ink-3 font-mono truncate">{fileName}</span>}
            </div>

            <textarea
                value={pasted}
                onChange={(e) => { setPasted(e.target.value); setFileName(null); }}
                rows={8}
                spellCheck={false}
                placeholder="…or paste JSON here"
                className="w-full border border-line rounded-md px-3 py-2 text-xs font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
            />

            <div className="flex items-center gap-3 mt-3">
                <button
                    onClick={runImport}
                    disabled={isImporting || !pasted.trim()}
                    className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-40 transition-colors"
                >
                    {isImporting ? "Importing…" : "Import"}
                </button>
                {(pasted || summary || clientErrors.length > 0 || errorMessage) && (
                    <button
                        onClick={() => { setPasted(""); setFileName(null); reset(); }}
                        className="text-xs text-ink-3 hover:text-ink transition-colors"
                    >
                        Clear
                    </button>
                )}
            </div>

            {clientErrors.length > 0 && (
                <div className="mt-3 p-3 bg-bad/10 rounded-md">
                    <p className="text-xs text-bad font-medium">{clientErrors.length} problem(s) — nothing was imported:</p>
                    <ul className="mt-1 text-xs text-bad font-mono max-h-40 overflow-y-auto list-disc pl-4">
                        {clientErrors.map((e, i) => <li key={i}>{e}</li>)}
                    </ul>
                </div>
            )}

            {errorMessage && <p className="mt-3 text-xs text-bad">{errorMessage}</p>}

            {summary && (
                <div className="mt-3 p-3 bg-bg-2 rounded-md">
                    <p className="text-xs text-ink">
                        Created: <span className="font-medium">{summary.created}</span>
                        {" · "}Updated: <span className="font-medium">{summary.updated}</span>
                        {summary.errors.length > 0 && <> · <span className="text-bad font-medium">{summary.errors.length} error(s)</span></>}
                    </p>
                    {summary.errors.length > 0 && (
                        <ul className="mt-1 text-xs text-bad font-mono max-h-40 overflow-y-auto list-disc pl-4">
                            {summary.errors.map((e, i) => <li key={i}>{e}</li>)}
                        </ul>
                    )}
                </div>
            )}
        </div>
    );
}
