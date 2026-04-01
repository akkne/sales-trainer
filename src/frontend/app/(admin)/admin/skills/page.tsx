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
                <h1 className="text-xl font-semibold text-gray-900">Skills</h1>
                <button
                    onClick={() => setShowForm((v) => !v)}
                    className="px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 transition-colors"
                >
                    {showForm ? "Cancel" : "+ New skill"}
                </button>
            </div>

            {showForm && (
                <div className="bg-white border border-gray-200 rounded-lg p-5 mb-6">
                    <h2 className="text-sm font-medium text-gray-700 mb-4">New skill</h2>
                    <div className="grid grid-cols-2 gap-4">
                        <label className="block">
                            <span className="text-xs text-gray-500">Title</span>
                            <input
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                value={form.title}
                                onChange={(e) => setForm({ ...form, title: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-gray-500">Slug</span>
                            <input
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                value={form.slug}
                                onChange={(e) => setForm({ ...form, slug: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-gray-500">Icon name</span>
                            <input
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                value={form.iconName}
                                onChange={(e) => setForm({ ...form, iconName: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-gray-500">Sort order</span>
                            <input
                                type="number"
                                className="mt-1 w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-gray-400"
                                value={form.sortOrder}
                                onChange={(e) =>
                                    setForm({ ...form, sortOrder: Number(e.target.value) })
                                }
                            />
                        </label>
                    </div>
                    <div className="mt-4">
                        <span className="text-xs text-gray-500 block mb-2">
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
                                            ? "bg-gray-900 text-white border-gray-900"
                                            : "bg-white text-gray-600 border-gray-300 hover:border-gray-500"
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
                        className="mt-4 px-4 py-2 text-sm bg-gray-900 text-white rounded-md hover:bg-gray-700 disabled:opacity-50 transition-colors"
                    >
                        {createSkill.isPending ? "Saving..." : "Create"}
                    </button>
                    {createSkill.isError && (
                        <p className="mt-2 text-xs text-red-500">
                            {(createSkill.error as Error).message}
                        </p>
                    )}
                </div>
            )}

            {isLoading ? (
                <p className="text-sm text-gray-400">Loading...</p>
            ) : skills.length === 0 ? (
                <p className="text-sm text-gray-400">No skills yet.</p>
            ) : (
                <table className="w-full text-sm border-collapse">
                    <thead>
                        <tr className="border-b border-gray-200">
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Title
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Slug
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Order
                            </th>
                            <th className="text-left py-2 px-3 text-xs text-gray-500 font-medium">
                                Sales types
                            </th>
                            <th className="py-2 px-3" />
                        </tr>
                    </thead>
                    <tbody>
                        {skills.map((skill) => (
                            <tr
                                key={skill.id}
                                className="border-b border-gray-100 hover:bg-gray-50"
                            >
                                <td className="py-2.5 px-3 font-medium text-gray-800">
                                    <Link
                                        href={`/admin/skills/${skill.id}`}
                                        className="hover:underline"
                                    >
                                        {skill.title}
                                    </Link>
                                </td>
                                <td className="py-2.5 px-3 text-gray-500">{skill.slug}</td>
                                <td className="py-2.5 px-3 text-gray-500">{skill.sortOrder}</td>
                                <td className="py-2.5 px-3 text-gray-500">
                                    {skill.applicableSalesTypes.join(", ") || "—"}
                                </td>
                                <td className="py-2.5 px-3 text-right">
                                    {confirmDeleteId === skill.id ? (
                                        <span className="inline-flex gap-2">
                                            <button
                                                onClick={() => handleDelete(skill.id)}
                                                className="text-xs text-red-600 hover:underline"
                                            >
                                                Confirm
                                            </button>
                                            <button
                                                onClick={() => setConfirmDeleteId(null)}
                                                className="text-xs text-gray-400 hover:underline"
                                            >
                                                Cancel
                                            </button>
                                        </span>
                                    ) : (
                                        <button
                                            onClick={() => setConfirmDeleteId(skill.id)}
                                            className="text-xs text-gray-400 hover:text-red-500 transition-colors"
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
