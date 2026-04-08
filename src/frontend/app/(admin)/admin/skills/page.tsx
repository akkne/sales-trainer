"use client";

import { useState } from "react";
import Link from "next/link";
import {
    useAdminSkills,
    useCreateSkill,
    useDeleteSkill,
    type AdminSkill,
} from "@/lib/hooks/useAdmin";

const SALES_TYPES = [
    "b2b_saas",
    "retail",
    "real_estate",
    "finance",
    "b2c",
];

const emptyForm = (): Omit<AdminSkill, "id"> => ({
    title: "",
    slug: "",
    iconName: "star",
    sortOrder: 0,
    prerequisiteSkillId: null,
    applicableSalesTypes: [],
});

export default function AdminSkillsPage() {
    const { data: skills = [], isLoading } = useAdminSkills();
    const createSkill = useCreateSkill();
    const deleteSkill = useDeleteSkill();

    const [showForm, setShowForm] = useState(false);
    const [form, setForm] = useState(emptyForm());
    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

    function handleSalesTypeToggle(type: string) {
        setForm((prev) => ({
            ...prev,
            applicableSalesTypes: prev.applicableSalesTypes.includes(type)
                ? prev.applicableSalesTypes.filter((t) => t !== type)
                : [...prev.applicableSalesTypes, type],
        }));
    }

    async function handleCreate() {
        await createSkill.mutateAsync(form);
        setForm(emptyForm());
        setShowForm(false);
    }

    async function handleDelete(id: string) {
        await deleteSkill.mutateAsync(id);
        setConfirmDeleteId(null);
    }

    return (
        <div>
            <div className="flex items-center justify-between mb-6">
                <h1 className="text-xl font-semibold text-on-surface">Skills</h1>
                <button
                    onClick={() => setShowForm((v) => !v)}
                    className="px-4 py-2 text-sm bg-primary text-on-primary rounded-md hover:bg-primary-dim transition-colors"
                >
                    {showForm ? "Cancel" : "+ New skill"}
                </button>
            </div>

            {showForm && (
                <div className="bg-surface-container-lowest rounded-2xl p-5 mb-6">
                    <h2 className="text-sm font-medium text-on-surface mb-4">New skill</h2>
                    <div className="grid grid-cols-2 gap-4">
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Title</span>
                            <input
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={form.title}
                                onChange={(e) => setForm({ ...form, title: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Slug</span>
                            <input
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={form.slug}
                                onChange={(e) => setForm({ ...form, slug: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Icon name</span>
                            <input
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={form.iconName}
                                onChange={(e) => setForm({ ...form, iconName: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-on-surface-variant">Sort order</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-outline-variant rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-primary"
                                value={form.sortOrder}
                                onChange={(e) =>
                                    setForm({ ...form, sortOrder: Number(e.target.value) })
                                }
                            />
                        </label>
                    </div>
                    <div className="mt-4">
                        <span className="text-xs text-on-surface-variant block mb-2">
                            Applicable sales types
                        </span>
                        <div className="flex flex-wrap gap-2">
                            {SALES_TYPES.map((type) => (
                                <button
                                    key={type}
                                    type="button"
                                    onClick={() => handleSalesTypeToggle(type)}
                                    className={`px-3 py-1 text-xs rounded-full border transition-colors ${
                                        form.applicableSalesTypes.includes(type)
                                            ? "bg-primary text-on-primary border-primary"
                                            : "bg-surface-container-lowest text-on-surface-variant border-outline-variant hover:border-on-surface-variant"
                                    }`}
                                >
                                    {type}
                                </button>
                            ))}
                        </div>
                    </div>
                    <button
                        onClick={handleCreate}
                        disabled={createSkill.isPending || !form.title || !form.slug}
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
                                Title
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Slug
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Order
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-on-surface-variant font-medium">
                                Sales types
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
                                <td className="py-2.5 px-3 font-medium text-on-surface">
                                    <Link
                                        href={`/admin/skills/${skill.id}`}
                                        className="hover:underline"
                                    >
                                        {skill.title}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 text-on-surface-variant">{skill.slug}</td>
                                <td className="py-2.5 px-3 text-on-surface-variant">{skill.sortOrder}</td>
                                <td className="py-2.5 px-3 text-on-surface-variant">
                                    {skill.applicableSalesTypes.join(", ") || "—"}
                                </td>
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
