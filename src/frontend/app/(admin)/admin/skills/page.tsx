"use client";

import { useState } from "react";
import Link from "next/link";
import {
    useAdminSkills,
    useCreateSkill,
    useDeleteSkill,
    useImportSkills,
    type AdminSkill,
} from "@/features/admin/hooks/use-admin";
import { getStageMeta } from "@/features/skills/constants/skill-stages";
import { useSkillStages } from "@/features/skills/hooks/use-skill-tree";
import { ImportPanel } from "@/features/admin/components/import-panel";
import { SKILLS_TEMPLATE } from "@/features/admin/lib/import-templates";

const emptyForm = (): Omit<AdminSkill, "id"> => ({
    iconicName: "",
    title: "",
    description: null,
    orderInTree: 0,
    stage: "",
});

export default function AdminSkillsPage() {
    const { data: skills = [], isLoading } = useAdminSkills();
    const { stages } = useSkillStages();
    const createSkill = useCreateSkill();
    const deleteSkill = useDeleteSkill();
    const importSkills = useImportSkills();

    const [showForm, setShowForm] = useState(false);
    const [form, setForm] = useState(emptyForm());
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    async function handleCreate() {
        const stage = form.stage || stages[0]?.key || "general";
        await createSkill.mutateAsync({ ...form, stage });
        setForm(emptyForm());
        setShowForm(false);
    }

    async function handleDelete(id: string) {
        await deleteSkill.mutateAsync(id);
        setConfirmDeleteId(null);
    }

    return (
        <div>
            <div className="flex flex-wrap gap-3 items-center justify-between mb-6">
                <h1 className="text-xl font-semibold text-ink">Skills</h1>
                <button
                    onClick={() => setShowForm((v) => !v)}
                    className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 transition-colors"
                >
                    {showForm ? "Cancel" : "+ New skill"}
                </button>
            </div>

            <ImportPanel
                title="Import Skills"
                description='JSON array: [{ iconicName, title, description, orderInTree, stage }]'
                templateData={SKILLS_TEMPLATE}
                templateFilename="skills_template.json"
                onImport={async ({ text }) => {
                    const file = new File([text], "import.json", { type: "application/json" });
                    const result = await importSkills.mutateAsync(file);
                    return {
                        created: result.skillsCreated,
                        updated: result.skillsUpdated,
                        errors: result.errors,
                    };
                }}
            />

            {showForm && (
                <div className="bg-surface rounded-2xl p-5 mb-6">
                    <h2 className="text-sm font-medium text-ink mb-4">New skill</h2>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <label className="block">
                            <span className="text-xs text-ink-3">Iconic Name (English ID)</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={form.iconicName}
                                onChange={(e) => setForm({ ...form, iconicName: e.target.value })}
                                placeholder="e.g. cold-calling"
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Title (Display Name)</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={form.title}
                                onChange={(e) => setForm({ ...form, title: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Order in tree</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={form.orderInTree}
                                onChange={(e) =>
                                    setForm({ ...form, orderInTree: Number(e.target.value) })
                                }
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">Stage</span>
                            <select
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                                value={form.stage || stages[0]?.key || ""}
                                onChange={(e) => setForm({ ...form, stage: e.target.value })}
                            >
                                {stages.map((s) => (
                                    <option key={s.key} value={s.key}>
                                        {s.label}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <label className="block md:col-span-2">
                            <span className="text-xs text-ink-3">Description</span>
                            <textarea
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                rows={2}
                                value={form.description || ""}
                                onChange={(e) => setForm({ ...form, description: e.target.value || null })}
                            />
                        </label>
                    </div>
                    <button
                        onClick={handleCreate}
                        disabled={createSkill.isPending || !form.iconicName || !form.title}
                        className="mt-4 px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                    >
                        {createSkill.isPending ? "Saving..." : "Create"}
                    </button>
                    {createSkill.isError && (
                        <p className="mt-2 text-xs text-bad">
                            {(createSkill.error as Error).message}
                        </p>
                    )}
                </div>
            )}

            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : skills.length === 0 ? (
                <p className="text-sm text-ink-3">No skills yet.</p>
            ) : (
                <div className="overflow-x-auto -mx-4 px-4">
                <table className="w-full text-sm border-collapse min-w-[540px]">
                    <thead>
                        <tr className="border-b border-line">
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Iconic Name
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Title
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Description
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Stage
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-ink-3 font-medium">
                                Order
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {skills.map((skill) => (
                            <tr
                                key={skill.id}
                                className="border-b border-line hover:bg-bg-2"
                            >
                                <td className="py-2.5 px-3 font-mono text-xs text-ink">
                                    <Link
                                        href={`/admin/skills/${skill.id}`}
                                        className="hover:underline"
                                    >
                                        {skill.iconicName}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 font-medium text-ink">
                                    {skill.title}
                                </td>
                                <td className="py-2.5 px-3 text-ink-3">
                                    {skill.description || "—"}
                                </td>
                                <td className="py-2.5 px-3 text-ink-3 text-xs">
                                    {getStageMeta(skill.stage, stages).label}
                                </td>
                                <td className="py-2.5 px-3 text-ink-3">{skill.orderInTree}</td>
                                <td className="py-2.5 px-3 text-right">
                                    {confirmDeleteId === skill.id ? (
                                        <span className="inline-flex gap-2">
                                            <button
                                                onClick={() => handleDelete(skill.id)}
                                                className="text-xs text-bad hover:underline"
                                            >
                                                Confirm
                                            </button>
                                            <button
                                                onClick={() => setConfirmDeleteId(null)}
                                                className="text-xs text-ink-3 hover:underline"
                                            >
                                                Cancel
                                            </button>
                                        </span>
                                    ) : (
                                        <button
                                            onClick={() => setConfirmDeleteId(skill.id)}
                                            className="text-xs text-ink-3 hover:text-bad transition-colors"
                                        >
                                            Delete
                                        </button>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                </div>
            )}
        </div>
    );
}
