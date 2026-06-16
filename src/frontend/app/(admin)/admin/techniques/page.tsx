"use client";

import { useMemo, useState, useDeferredValue, useRef } from "react";
import {
    useAdminTechniques,
    useAdminSkills,
    useCreateTechnique,
    useUpdateTechnique,
    useDeleteTechnique,
    useImportTechniques,
    type AdminTechnique,
    type AdminTechniqueWriteBody,
    type AdminTechniqueImportResult,
} from "@/features/admin/hooks/use-admin";

const DIFFICULTY_OPTIONS = [
    { value: 1, label: "1 · Novice" },
    { value: 2, label: "2 · Practitioner" },
    { value: 3, label: "3 · Expert" },
    { value: 4, label: "4 · Master" },
];

const EMPTY_FORM: AdminTechniqueWriteBody = {
    slug: "",
    name: "",
    summary: "",
    body: "",
    tags: [],
    primarySkillId: null,
    additionalSkillIds: [],
    difficulty: 1,
    sortOrder: 0,
    dialog: null,
    case: null,
    coach: null,
};

export default function AdminTechniquesPage() {
    const [rawSearch, setRawSearch] = useState("");
    const search = useDeferredValue(rawSearch);
    const [selectedSkill, setSelectedSkill] = useState("");

    const [showCreateForm, setShowCreateForm] = useState(false);
    const [createForm, setCreateForm] = useState<AdminTechniqueWriteBody>(EMPTY_FORM);

    const [editingId, setEditingId] = useState<string | null>(null);
    const [editForm, setEditForm] = useState<AdminTechniqueWriteBody>(EMPTY_FORM);

    const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
    const [importResult, setImportResult] = useState<AdminTechniqueImportResult | null>(null);
    const [importError, setImportError] = useState<string | null>(null);
    const importInputRef = useRef<HTMLInputElement>(null);

    const { data: techniques = [], isLoading } = useAdminTechniques({
        skill: selectedSkill || undefined,
        search: search || undefined,
    });
    const { data: skills = [] } = useAdminSkills();

    const createTechnique = useCreateTechnique();
    const updateTechnique = useUpdateTechnique(editingId ?? "");
    const deleteTechnique = useDeleteTechnique();
    const importTechniques = useImportTechniques();

    const skillOptions = useMemo(
        () => skills.map((s) => ({ value: s.id, label: `${s.title} (${s.iconicName})`, iconicName: s.iconicName })),
        [skills]
    );

    function startEdit(technique: AdminTechnique) {
        setEditingId(technique.id);
        setEditForm({
            slug: technique.slug,
            name: technique.name,
            summary: technique.summary,
            body: technique.body,
            tags: technique.tags,
            primarySkillId: technique.primarySkillId,
            additionalSkillIds: technique.additionalSkillIds,
            difficulty: technique.difficulty,
            sortOrder: technique.sortOrder,
            dialog: technique.dialog ?? null,
            case: technique.case ?? null,
            coach: technique.coach,
        });
    }

    async function handleCreate() {
        await createTechnique.mutateAsync(createForm);
        setCreateForm(EMPTY_FORM);
        setShowCreateForm(false);
    }

    async function handleSave() {
        await updateTechnique.mutateAsync(editForm);
        setEditingId(null);
    }

    async function handleImportFile(event: React.ChangeEvent<HTMLInputElement>) {
        const file = event.target.files?.[0];
        if (!file) return;
        setImportError(null);
        setImportResult(null);
        try {
            const text = await file.text();
            const parsed = JSON.parse(text);
            if (!Array.isArray(parsed)) {
                setImportError("JSON must be an array of techniques.");
                return;
            }
            const result = await importTechniques.mutateAsync(parsed as AdminTechniqueWriteBody[]);
            setImportResult(result);
        } catch (err) {
            setImportError((err as Error).message);
        } finally {
            if (importInputRef.current) importInputRef.current.value = "";
        }
    }

    return (
        <div>
            <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
                <h1 className="text-xl font-semibold text-ink">Techniques</h1>
                <div className="flex flex-wrap gap-2">
                    <input
                        ref={importInputRef}
                        type="file"
                        accept="application/json,.json"
                        className="hidden"
                        onChange={handleImportFile}
                    />
                    <button
                        onClick={() => importInputRef.current?.click()}
                        disabled={importTechniques.isPending}
                        className="px-4 py-2 text-sm bg-bg-2 text-ink rounded-md hover:bg-surface-2 disabled:opacity-50 transition-colors"
                    >
                        {importTechniques.isPending ? "Importing..." : "Import JSON"}
                    </button>
                    <button
                        onClick={() => setShowCreateForm((v) => !v)}
                        className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 transition-colors"
                    >
                        {showCreateForm ? "Cancel" : "+ New technique"}
                    </button>
                </div>
            </div>

            {importError && (
                <div className="bg-bad-soft text-bad rounded-lg px-4 py-3 mb-4 text-sm">
                    Import failed: {importError}
                </div>
            )}
            {importResult && (
                <div className="bg-accent-soft text-accent rounded-lg px-4 py-3 mb-4 text-sm">
                    Import finished — created {importResult.createdCount}, updated {importResult.updatedCount}, failed {importResult.failedCount}
                    {importResult.errors.length > 0 && (
                        <ul className="mt-2 list-disc list-inside text-xs">
                            {importResult.errors.map((err) => (
                                <li key={err}>{err}</li>
                            ))}
                        </ul>
                    )}
                </div>
            )}

            {showCreateForm && (
                <div className="bg-surface rounded-2xl border border-line p-5 mb-6 space-y-4">
                    <h2 className="text-sm font-semibold text-ink">Create technique</h2>
                    <TechniqueFormFields form={createForm} onChange={setCreateForm} skillOptions={skillOptions} />
                    <button
                        onClick={handleCreate}
                        disabled={createTechnique.isPending || !createForm.slug || !createForm.name}
                        className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                    >
                        {createTechnique.isPending ? "Saving..." : "Create"}
                    </button>
                </div>
            )}

            <div className="flex flex-wrap gap-3 mb-5">
                <input
                    type="search"
                    placeholder="Search name, summary or body..."
                    value={rawSearch}
                    onChange={(e) => setRawSearch(e.target.value)}
                    className="border border-line rounded-md px-3 py-1.5 text-sm w-72 focus:outline-none focus:ring-1 focus:ring-indigo/30"
                />
                <select
                    value={selectedSkill}
                    onChange={(e) => setSelectedSkill(e.target.value)}
                    className="border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                >
                    <option value="">All skills</option>
                    {skillOptions.map((option) => (
                        <option key={option.value} value={option.iconicName}>
                            {option.label}
                        </option>
                    ))}
                </select>
            </div>

            {isLoading ? (
                <p className="text-sm text-ink-3">Loading...</p>
            ) : techniques.length === 0 ? (
                <p className="text-sm text-ink-3">No techniques found.</p>
            ) : (
                <div className="space-y-4">
                    {techniques.map((technique) => (
                        <div key={technique.id} className="bg-surface rounded-2xl border border-line p-5">
                            {editingId === technique.id ? (
                                <div className="space-y-4">
                                    <TechniqueFormFields form={editForm} onChange={setEditForm} skillOptions={skillOptions} />
                                    <div className="flex gap-3">
                                        <button
                                            onClick={handleSave}
                                            disabled={updateTechnique.isPending}
                                            className="px-4 py-2 text-sm bg-ink text-bg rounded-md hover:opacity-90 disabled:opacity-50 transition-colors"
                                        >
                                            {updateTechnique.isPending ? "Saving..." : "Save"}
                                        </button>
                                        <button
                                            onClick={() => setEditingId(null)}
                                            className="px-4 py-2 text-sm text-ink-3 hover:text-ink transition-colors"
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div>
                                    <div className="flex items-start justify-between mb-2">
                                        <div>
                                            <h3 className="font-medium text-ink">
                                                {technique.name}{" "}
                                                <span className="text-xs text-ink-3 font-mono">({technique.slug})</span>
                                            </h3>
                                            <div className="flex flex-wrap gap-2 mt-1">
                                                <span className="text-xs bg-indigo-soft text-indigo rounded px-2 py-0.5">
                                                    {technique.difficultyName}
                                                </span>
                                                {technique.primarySkillTitle && (
                                                    <span className="text-xs bg-bg-2 text-ink-3 rounded px-2 py-0.5">
                                                        {technique.primarySkillTitle}
                                                    </span>
                                                )}
                                                {technique.tags.map((tag) => (
                                                    <span key={tag} className="text-xs bg-accent-soft text-accent rounded px-2 py-0.5">
                                                        {tag}
                                                    </span>
                                                ))}
                                                <span className="text-xs text-ink-3">order: {technique.sortOrder}</span>
                                                {technique.dialog ? (
                                                    <span className="text-xs bg-indigo-soft text-indigo rounded px-2 py-0.5">dialog</span>
                                                ) : null}
                                                {technique.case ? (
                                                    <span className="text-xs bg-indigo-soft text-indigo rounded px-2 py-0.5">case</span>
                                                ) : null}
                                                {technique.coach && (
                                                    <span className="text-xs bg-indigo-soft text-indigo rounded px-2 py-0.5">coach</span>
                                                )}
                                            </div>
                                        </div>
                                        <div className="flex gap-3 shrink-0 ml-4">
                                            <button
                                                onClick={() => startEdit(technique)}
                                                className="text-sm text-ink-3 hover:text-ink transition-colors"
                                            >
                                                Edit
                                            </button>
                                            {confirmDeleteId === technique.id ? (
                                                <>
                                                    <button
                                                        onClick={() => {
                                                            deleteTechnique.mutate(technique.id);
                                                            setConfirmDeleteId(null);
                                                        }}
                                                        className="text-sm text-bad hover:underline"
                                                    >
                                                        Confirm
                                                    </button>
                                                    <button
                                                        onClick={() => setConfirmDeleteId(null)}
                                                        className="text-sm text-ink-3 hover:underline"
                                                    >
                                                        Cancel
                                                    </button>
                                                </>
                                            ) : (
                                                <button
                                                    onClick={() => setConfirmDeleteId(technique.id)}
                                                    className="text-sm text-ink-3 hover:text-bad transition-colors"
                                                >
                                                    Delete
                                                </button>
                                            )}
                                        </div>
                                    </div>
                                    <p className="text-sm text-ink-3 mt-2">{technique.summary}</p>
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

interface SkillOption {
    value: string;
    iconicName: string;
    label: string;
}

function TechniqueFormFields({
    form,
    onChange,
    skillOptions,
}: {
    form: AdminTechniqueWriteBody;
    onChange: (updated: AdminTechniqueWriteBody) => void;
    skillOptions: SkillOption[];
}) {
    const [dialogText, setDialogText] = useState(form.dialog ? JSON.stringify(form.dialog, null, 2) : "");
    const [dialogError, setDialogError] = useState<string | null>(null);
    const [caseText, setCaseText] = useState(form.case ? JSON.stringify(form.case, null, 2) : "");
    const [caseError, setCaseError] = useState<string | null>(null);
    const [tagsText, setTagsText] = useState(form.tags.join(", "));
    const [coachEnabled, setCoachEnabled] = useState(!!form.coach);
    const [challengesText, setChallengesText] = useState(
        form.coach?.challenges ? JSON.stringify(form.coach.challenges, null, 2) : ""
    );
    const [challengesError, setChallengesError] = useState<string | null>(null);

    function updateDialog(text: string) {
        setDialogText(text);
        if (!text.trim()) {
            setDialogError(null);
            onChange({ ...form, dialog: null });
            return;
        }
        try {
            const parsed = JSON.parse(text);
            setDialogError(null);
            onChange({ ...form, dialog: parsed });
        } catch (err) {
            setDialogError((err as Error).message);
        }
    }

    function updateCase(text: string) {
        setCaseText(text);
        if (!text.trim()) {
            setCaseError(null);
            onChange({ ...form, case: null });
            return;
        }
        try {
            const parsed = JSON.parse(text);
            setCaseError(null);
            onChange({ ...form, case: parsed });
        } catch (err) {
            setCaseError((err as Error).message);
        }
    }

    function updateTags(text: string) {
        setTagsText(text);
        const tags = text
            .split(",")
            .map((t) => t.trim())
            .filter((t) => t.length > 0);
        onChange({ ...form, tags });
    }

    function toggleCoach(enabled: boolean) {
        setCoachEnabled(enabled);
        if (!enabled) {
            onChange({ ...form, coach: null });
            return;
        }
        onChange({
            ...form,
            coach: form.coach ?? { avatarSeed: "", name: "", role: "", quote: "", challenges: null },
        });
    }

    function updateCoach(patch: Partial<NonNullable<AdminTechniqueWriteBody["coach"]>>) {
        const base = form.coach ?? { avatarSeed: "", name: "", role: "", quote: "", challenges: null };
        onChange({ ...form, coach: { ...base, ...patch } });
    }

    function updateChallenges(text: string) {
        setChallengesText(text);
        if (!text.trim()) {
            setChallengesError(null);
            updateCoach({ challenges: null });
            return;
        }
        try {
            const parsed = JSON.parse(text);
            setChallengesError(null);
            updateCoach({ challenges: parsed });
        } catch (err) {
            setChallengesError((err as Error).message);
        }
    }

    return (
        <>
            <div className="flex flex-wrap gap-3">
                <label className="block flex-1 min-w-[140px]">
                    <span className="text-xs text-ink-3">Slug *</span>
                    <input
                        className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        value={form.slug}
                        onChange={(e) => onChange({ ...form, slug: e.target.value })}
                    />
                </label>
                <label className="block flex-1 min-w-[140px]">
                    <span className="text-xs text-ink-3">Name *</span>
                    <input
                        className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        value={form.name}
                        onChange={(e) => onChange({ ...form, name: e.target.value })}
                    />
                </label>
            </div>

            <label className="block">
                <span className="text-xs text-ink-3">Summary</span>
                <input
                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                    value={form.summary}
                    onChange={(e) => onChange({ ...form, summary: e.target.value })}
                />
            </label>

            <div className="flex flex-wrap gap-3">
                <label className="block flex-1 min-w-[160px]">
                    <span className="text-xs text-ink-3">Primary skill</span>
                    <select
                        className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        value={form.primarySkillId ?? ""}
                        onChange={(e) => onChange({ ...form, primarySkillId: e.target.value || null })}
                    >
                        <option value="">— none —</option>
                        {skillOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                                {option.label}
                            </option>
                        ))}
                    </select>
                </label>
                <label className="block w-40">
                    <span className="text-xs text-ink-3">Difficulty</span>
                    <select
                        className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        value={form.difficulty}
                        onChange={(e) => onChange({ ...form, difficulty: Number(e.target.value) })}
                    >
                        {DIFFICULTY_OPTIONS.map((option) => (
                            <option key={option.value} value={option.value}>
                                {option.label}
                            </option>
                        ))}
                    </select>
                </label>
                <label className="block w-28">
                    <span className="text-xs text-ink-3">Sort order</span>
                    <input
                        type="number"
                        className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                        value={form.sortOrder}
                        onChange={(e) => onChange({ ...form, sortOrder: Number(e.target.value) })}
                    />
                </label>
            </div>

            <label className="block">
                <span className="text-xs text-ink-3">Tags (comma-separated)</span>
                <input
                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                    placeholder="discovery, objection-handling"
                    value={tagsText}
                    onChange={(e) => updateTags(e.target.value)}
                />
            </label>

            <label className="block">
                <span className="text-xs text-ink-3">Body (Markdown)</span>
                <textarea
                    rows={6}
                    className="mt-1 w-full border border-line rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30"
                    value={form.body}
                    onChange={(e) => onChange({ ...form, body: e.target.value })}
                />
            </label>

            <label className="block">
                <span className="text-xs text-ink-3">
                    Dialog JSON (optional) — array of turns with {"{ orderIndex, side, text, annotations }"}
                </span>
                <textarea
                    rows={6}
                    className="mt-1 w-full border border-line rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30"
                    value={dialogText}
                    onChange={(e) => updateDialog(e.target.value)}
                />
                {dialogError && <span className="text-xs text-bad">{dialogError}</span>}
            </label>

            <label className="block">
                <span className="text-xs text-ink-3">
                    Case JSON (optional) — object with {"{ title, body, metrics? }"}
                </span>
                <textarea
                    rows={5}
                    className="mt-1 w-full border border-line rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30"
                    value={caseText}
                    onChange={(e) => updateCase(e.target.value)}
                />
                {caseError && <span className="text-xs text-bad">{caseError}</span>}
            </label>

            <div className="border border-line rounded-xl p-4 space-y-3">
                <label className="flex items-center gap-2 text-sm">
                    <input
                        type="checkbox"
                        checked={coachEnabled}
                        onChange={(e) => toggleCoach(e.target.checked)}
                    />
                    Attach coach
                </label>
                {coachEnabled && form.coach && (
                    <>
                        <div className="flex flex-wrap gap-3">
                            <label className="block flex-1 min-w-[120px]">
                                <span className="text-xs text-ink-3">Avatar seed</span>
                                <input
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={form.coach.avatarSeed}
                                    onChange={(e) => updateCoach({ avatarSeed: e.target.value })}
                                />
                            </label>
                            <label className="block flex-1 min-w-[120px]">
                                <span className="text-xs text-ink-3">Name</span>
                                <input
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={form.coach.name}
                                    onChange={(e) => updateCoach({ name: e.target.value })}
                                />
                            </label>
                            <label className="block flex-1 min-w-[120px]">
                                <span className="text-xs text-ink-3">Role</span>
                                <input
                                    className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                    value={form.coach.role}
                                    onChange={(e) => updateCoach({ role: e.target.value })}
                                />
                            </label>
                        </div>
                        <label className="block">
                            <span className="text-xs text-ink-3">Quote</span>
                            <input
                                className="mt-1 w-full border border-line rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={form.coach.quote}
                                onChange={(e) => updateCoach({ quote: e.target.value })}
                            />
                        </label>
                        <label className="block">
                            <span className="text-xs text-ink-3">
                                Challenges JSON — array of {"{ label, kind?, targetSlug? }"}
                            </span>
                            <textarea
                                rows={4}
                                className="mt-1 w-full border border-line rounded-md px-3 py-2 text-sm font-mono focus:outline-none focus:ring-1 focus:ring-indigo/30"
                                value={challengesText}
                                onChange={(e) => updateChallenges(e.target.value)}
                            />
                            {challengesError && <span className="text-xs text-bad">{challengesError}</span>}
                        </label>
                    </>
                )}
            </div>
        </>
    );
}
