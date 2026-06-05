"use client";

import { useState, useRef } from "react";
import Link from "next/link";
import {
    useAdminSkills,
    useCreateSkill,
    useDeleteSkill,
    useImportSkills,
    type AdminSkill,
    type SkillsImportResult,
} from "@/features/admin/hooks/use-admin";

const emptyForm = (): Omit<AdminSkill, "id"> => ({
    iconicName: "",
    title: "",
    description: null,
    orderInTree: 0,
});

const SKILLS_TEMPLATE = JSON.stringify([
    {
        iconicName: "example-skill",
        title: "Example Skill",
        description: "Description of the skill",
        orderInTree: 1
    }
], null, 2);

function downloadSkillsTemplate() {
    const blob = new Blob([SKILLS_TEMPLATE], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "skills_template.json";
    a.click();
    URL.revokeObjectURL(url);
}

export default function AdminSkillsPage() {
    const { data: skills = [], isLoading } = useAdminSkills();
    const createSkill = useCreateSkill();
    const deleteSkill = useDeleteSkill();
    const importSkills = useImportSkills();

    const [showForm, setShowForm] = useState(false);
    const [form, setForm] = useState(emptyForm());
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    const fileInputRef = useRef<HTMLInputElement>(null);
    const [showImport, setShowImport] = useState(false);
    const [importResult, setImportResult] = useState<SkillsImportResult | null>(null);

    async function handleCreate() {
        await createSkill.mutateAsync(form);
        setForm(emptyForm());
        setShowForm(false);
    }

    async function handleDelete(id: string) {
        await deleteSkill.mutateAsync(id);
        setConfirmDeleteId(null);
    }

    async function handleImport(e: React.ChangeEvent<HTMLInputElement>) {
        const file = e.target.files?.[0];
        if (!file) return;
        try {
            const result = await importSkills.mutateAsync(file);
            setImportResult(result);
        } catch {
            // Error handled by hook
        }
        if (fileInputRef.current) fileInputRef.current.value = "";
    }

    return (
        <div>
            <div className="flex items-center justify-between mb-6">
                <h1 className="text-xl font-semibold text-on-surface">Skills</h1>
                <div className="flex gap-2">
                    <button
                        onClick={() => { setShowImport((v) => !v); setImportResult(null); }}
                        className="px-4 py-2 text-sm border border-outline-variant text-on-surface-variant rounded-md hover:bg-surface-container transition-colors"
                    >
                        {showImport ? "Close Import" : "Import JSON"}
                    </button>
                    <button
                        onClick={() => setShowForm((v) => !v)}
                        className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim transition-colors"
                    >
                        {showForm ? "Cancel" : "+ New skill"}
                    </button>
                </div>
            </div>

            {showImport && (
                <div className="bg-surface-container-lowest border border-outline-variant rounded-2xl p-5 mb-6">
                    <div className="flex items-center justify-between mb-3">
                        <h2 className="text-sm font-medium text-on-surface">Import Skills from JSON</h2>
                        <button
                            onClick={downloadSkillsTemplate}
                            className="text-xs text-on-surface-variant hover:text-on-surface transition-colors underline"
                        >
                            Download template
                        </button>
                    </div>
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept=".json"
                        onChange={handleImport}
                        className="block w-full text-sm text-on-surface-variant file:mr-4 file:py-2 file:px-4 file:rounded-md file:border file:border-outline-variant file:text-sm file:bg-surface-container file:text-on-surface hover:file:bg-surface-container-high cursor-pointer"
                    />
                    {importSkills.isPending && (
                        <p className="mt-3 text-xs text-on-surface-variant">Importing...</p>
                    )}
                    {importSkills.isError && (
                        <p className="mt-3 text-xs text-error">{(importSkills.error as Error).message}</p>
                    )}
                    {importResult && (
                        <div className="mt-3 p-3 bg-surface-container rounded-md">
                            <p className="text-xs text-on-surface">
                                Created: <span className="font-medium">{importResult.skillsCreated}</span> | Updated: <span className="font-medium">{importResult.skillsUpdated}</span>
                            </p>
                            {importResult.errors.length > 0 && (
                                <div className="mt-2">
                                    <p className="text-xs text-error font-medium">{importResult.errors.length} error(s):</p>
                                    <ul className="mt-1 text-xs text-error font-mono">
                                        {importResult.errors.map((e, i) => <li key={i}>{e}</li>)}
                                    </ul>
                                </div>
                            )}
                        </div>
                    )}
                </div>
            )}

            {showForm && (
                <div className="bg-surface-container-lowest rounded-2xl p-5 mb-6">
                    <h2 className="text-sm font-medium text-on-surface mb-4">New skill</h2>
                    <div className="grid grid-cols-2 gap-4">
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Iconic Name (English ID)</span>
                            <input
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={form.iconicName}
                                onChange={(e) => setForm({ ...form, iconicName: e.target.value })}
                                placeholder="e.g. cold-calling"
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Title (Display Name)</span>
                            <input
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={form.title}
                                onChange={(e) => setForm({ ...form, title: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Order in tree</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={form.orderInTree}
                                onChange={(e) =>
                                    setForm({ ...form, orderInTree: Number(e.target.value) })
                                }
                            />
                        </label>
                        <div />
                        <label className="block col-span-2">
                            <span className="text-xs text-on-surface-variant">Description</span>
                            <textarea
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                rows={2}
                                value={form.description || ""}
                                onChange={(e) => setForm({ ...form, description: e.target.value || null })}
                            />
                        </label>
                    </div>
                    <button
                        onClick={handleCreate}
                        disabled={createSkill.isPending || !form.iconicName || !form.title}
                        className="mt-4 px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim disabled:opacity-50 transition-colors"
                    >
                        {createSkill.isPending ? "Saving..." : "Create"}
                    </button>
                    {createSkill.isError && (
                        <p className="mt-2 text-xs text-error">
                            {(createSkill.error as Error).message}
                        </p>
                    )}
                </div>
            )}

            {isLoading ? (
                <p className="text-sm text-on-surface-variant">Loading...</p>
            ) : skills.length === 0 ? (
                <p className="text-sm text-on-surface-variant">No skills yet.</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-outline-variant">
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Iconic Name
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Title
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Description
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Order
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {skills.map((skill) => (
                            <tr
                                key={skill.id}
                                className="border-b border-surface-container hover:bg-surface-container-low"
                            >
                                <td className="py-2.5 px-3 font-mono text-xs text-on-surface">
                                    <Link
                                        href={`/admin/skills/${skill.id}`}
                                        className="hover:underline"
                                    >
                                        {skill.iconicName}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 font-medium text-on-surface">
                                    {skill.title}
                                </td>
                                <td className="py-2.5 px-3 text-on-surface-variant">
                                    {skill.description || "—"}
                                </td>
                                <td className="py-2.5 px-3 text-on-surface-variant">{skill.orderInTree}</td>
                                <td className="py-2.5 px-3 text-right">
                                    {confirmDeleteId === skill.id ? (
                                        <span className="inline-flex gap-2">
                                            <button
                                                onClick={() => handleDelete(skill.id)}
                                                className="text-xs text-error hover:underline"
                                            >
                                                Confirm
                                            </button>
                                            <button
                                                onClick={() => setConfirmDeleteId(null)}
                                                className="text-xs text-on-surface-variant hover:underline"
                                            >
                                                Cancel
                                            </button>
                                        </span>
                                    ) : (
                                        <button
                                            onClick={() => setConfirmDeleteId(skill.id)}
                                            className="text-xs text-on-surface-variant hover:text-error transition-colors"
                                        >
                                            Delete
                                        </button>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}
