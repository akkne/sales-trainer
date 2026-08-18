"use client";

import { Button, IconButton } from "@/shared/components/button";
import { Chip } from "@/shared/components/common";
import { TextInput, Textarea } from "@/shared/components/input";
import { ORGANIZATION_PROFILE_FIELDS, TONE_SUGGESTIONS } from "../constants/profile-fields";
import type { ProfileAnswerDraft } from "../utils/interview-answers";

const TEXT_PLACEHOLDERS: Record<string, string> = {
    [ORGANIZATION_PROFILE_FIELDS.product]:
        "Например: облачная система учёта складских остатков для розничных сетей.",
    [ORGANIZATION_PROFILE_FIELDS.icp]:
        "Например: розничные сети 50–300 точек, решает операционный директор, средний чек 1,2 млн ₽.",
    [ORGANIZATION_PROFILE_FIELDS.tone]:
        "Например: на равных, без канцелярита, всегда с цифрами.",
};

interface AnswerEditorProps {
    fieldCode: string;
    draft: ProfileAnswerDraft;
    onChange: (draft: ProfileAnswerDraft) => void;
    disabled?: boolean;
}

/**
 * The input behind one interview question. Which of the four shapes appears is decided by the gap
 * code, never by the question text — the code is the closed vocabulary, the sentence is prose.
 */
export function AnswerEditor({ fieldCode, draft, onChange, disabled }: AnswerEditorProps) {
    switch (draft.kind) {
        case "text":
            return (
                <TextAnswerEditor
                    fieldCode={fieldCode}
                    value={draft.text}
                    onChange={(text) => onChange({ kind: "text", text })}
                    disabled={disabled}
                />
            );
        case "stringList":
            return fieldCode === ORGANIZATION_PROFILE_FIELDS.bannedClaims ? (
                <BannedClaimsEditor
                    claims={draft.items}
                    onChange={(items) => onChange({ kind: "stringList", items })}
                    disabled={disabled}
                />
            ) : (
                <ScriptStagesEditor
                    stages={draft.items}
                    onChange={(items) => onChange({ kind: "stringList", items })}
                    disabled={disabled}
                />
            );
        case "objections":
            return (
                <ObjectionsEditor
                    objections={draft.objections}
                    onChange={(objections) => onChange({ kind: "objections", objections })}
                    disabled={disabled}
                />
            );
        case "glossary":
            return (
                <GlossaryEditor
                    entries={draft.entries}
                    onChange={(entries) => onChange({ kind: "glossary", entries })}
                    disabled={disabled}
                />
            );
    }
}

function TextAnswerEditor({
    fieldCode,
    value,
    onChange,
    disabled,
}: {
    fieldCode: string;
    value: string;
    onChange: (value: string) => void;
    disabled?: boolean;
}) {
    return (
        <div className="space-y-2">
            <Textarea
                value={value}
                rows={fieldCode === ORGANIZATION_PROFILE_FIELDS.tone ? 2 : 3}
                placeholder={TEXT_PLACEHOLDERS[fieldCode]}
                onChange={(event) => onChange(event.target.value)}
                disabled={disabled}
            />
            {fieldCode === ORGANIZATION_PROFILE_FIELDS.tone && (
                <div className="flex flex-wrap gap-2">
                    {TONE_SUGGESTIONS.map((suggestion) => (
                        <Chip
                            key={suggestion}
                            disabled={disabled}
                            onClick={() =>
                                onChange(value.trim().length > 0 ? value : suggestion)
                            }
                        >
                            {suggestion}
                        </Chip>
                    ))}
                </div>
            )}
        </div>
    );
}

function ScriptStagesEditor({
    stages,
    onChange,
    disabled,
}: {
    stages: string[];
    onChange: (stages: string[]) => void;
    disabled?: boolean;
}) {
    const replaceStage = (index: number, value: string) =>
        onChange(stages.map((stage, stageIndex) => (stageIndex === index ? value : stage)));

    return (
        <div className="space-y-2">
            {stages.map((stage, index) => (
                <div key={index} className="flex items-center gap-2">
                    <span className="w-6 shrink-0 text-center text-xs text-ink-3 font-mono tabular-nums">
                        {index + 1}
                    </span>
                    <div className="flex-1 min-w-0">
                        <TextInput
                            value={stage}
                            inputSize="sm"
                            placeholder="Например: выявление потребности"
                            onChange={(event) => replaceStage(index, event.target.value)}
                            disabled={disabled}
                        />
                    </div>
                    <IconButton
                        icon="close"
                        variant="ghost"
                        size="sm"
                        aria-label={`Убрать этап ${index + 1}`}
                        disabled={disabled || stages.length === 1}
                        onClick={() =>
                            onChange(stages.filter((_, stageIndex) => stageIndex !== index))
                        }
                    />
                </div>
            ))}
            <Button
                variant="ghost"
                size="sm"
                iconLeft="plus"
                disabled={disabled}
                onClick={() => onChange([...stages, ""])}
            >
                Ещё этап
            </Button>
        </div>
    );
}

/**
 * The forbidden-statements editor.
 *
 * Everything about it says «нельзя»: the entries are prefixed with a stop marker, the placeholder is
 * a promise rather than a rule, and the note underneath states what the list actually does — it
 * binds both the AI persona and the grader (docs/CONTENT_PARAMETERIZATION.md §4). A rep who reads
 * this list as «наши обещания» would fill it with the opposite of what protects them.
 */
function BannedClaimsEditor({
    claims,
    onChange,
    disabled,
}: {
    claims: string[];
    onChange: (claims: string[]) => void;
    disabled?: boolean;
}) {
    const replaceClaim = (index: number, value: string) =>
        onChange(claims.map((claim, claimIndex) => (claimIndex === index ? value : claim)));

    return (
        <div className="space-y-2">
            <p className="text-xs text-ink-3">
                Здесь перечисляются фразы, которые <b className="text-ink">запрещено</b> обещать
                клиенту. Собеседник-ИИ никогда их не произнесёт, а проверяющий снизит за них оценку.
            </p>
            {claims.map((claim, index) => (
                <div key={index} className="flex items-center gap-2">
                    <span className="w-6 shrink-0 text-center text-bad" aria-hidden>
                        ✕
                    </span>
                    <div className="flex-1 min-w-0">
                        <TextInput
                            value={claim}
                            inputSize="sm"
                            placeholder="Например: гарантируем рост выручки на 30%"
                            onChange={(event) => replaceClaim(index, event.target.value)}
                            disabled={disabled}
                        />
                    </div>
                    <IconButton
                        icon="close"
                        variant="ghost"
                        size="sm"
                        aria-label={`Убрать строку ${index + 1}`}
                        disabled={disabled || claims.length === 1}
                        onClick={() =>
                            onChange(claims.filter((_, claimIndex) => claimIndex !== index))
                        }
                    />
                </div>
            ))}
            <Button
                variant="ghost"
                size="sm"
                iconLeft="plus"
                disabled={disabled}
                onClick={() => onChange([...claims, ""])}
            >
                Ещё запрет
            </Button>
        </div>
    );
}

function ObjectionsEditor({
    objections,
    onChange,
    disabled,
}: {
    objections: { text: string; bestResponse: string }[];
    onChange: (objections: { text: string; bestResponse: string }[]) => void;
    disabled?: boolean;
}) {
    const replaceObjection = (
        index: number,
        patch: Partial<{ text: string; bestResponse: string }>
    ) =>
        onChange(
            objections.map((objection, objectionIndex) =>
                objectionIndex === index ? { ...objection, ...patch } : objection
            )
        );

    return (
        <div className="space-y-3">
            {objections.map((objection, index) => (
                <div key={index} className="rounded-xl border border-line p-3 space-y-2">
                    <div className="flex items-center gap-2">
                        <div className="flex-1 min-w-0">
                            <TextInput
                                value={objection.text}
                                inputSize="sm"
                                placeholder="Возражение: например, «дорого»"
                                onChange={(event) =>
                                    replaceObjection(index, { text: event.target.value })
                                }
                                disabled={disabled}
                            />
                        </div>
                        <IconButton
                            icon="close"
                            variant="ghost"
                            size="sm"
                            aria-label={`Убрать возражение ${index + 1}`}
                            disabled={disabled || objections.length === 1}
                            onClick={() =>
                                onChange(
                                    objections.filter(
                                        (_, objectionIndex) => objectionIndex !== index
                                    )
                                )
                            }
                        />
                    </div>
                    <TextInput
                        value={objection.bestResponse}
                        inputSize="sm"
                        placeholder="Как на него отвечают ваши сильные менеджеры (необязательно)"
                        onChange={(event) =>
                            replaceObjection(index, { bestResponse: event.target.value })
                        }
                        disabled={disabled}
                    />
                </div>
            ))}
            <Button
                variant="ghost"
                size="sm"
                iconLeft="plus"
                disabled={disabled}
                onClick={() => onChange([...objections, { text: "", bestResponse: "" }])}
            >
                Ещё возражение
            </Button>
        </div>
    );
}

function GlossaryEditor({
    entries,
    onChange,
    disabled,
}: {
    entries: { term: string; definition: string }[];
    onChange: (entries: { term: string; definition: string }[]) => void;
    disabled?: boolean;
}) {
    const replaceEntry = (index: number, patch: Partial<{ term: string; definition: string }>) =>
        onChange(
            entries.map((entry, entryIndex) =>
                entryIndex === index ? { ...entry, ...patch } : entry
            )
        );

    return (
        <div className="space-y-2">
            {entries.map((entry, index) => (
                <div key={index} className="flex items-center gap-2">
                    <div className="w-40 shrink-0">
                        <TextInput
                            value={entry.term}
                            inputSize="sm"
                            placeholder="сделка"
                            onChange={(event) => replaceEntry(index, { term: event.target.value })}
                            disabled={disabled}
                        />
                    </div>
                    <span className="text-ink-3" aria-hidden>
                        →
                    </span>
                    <div className="flex-1 min-w-0">
                        <TextInput
                            value={entry.definition}
                            inputSize="sm"
                            placeholder="проект"
                            onChange={(event) =>
                                replaceEntry(index, { definition: event.target.value })
                            }
                            disabled={disabled}
                        />
                    </div>
                    <IconButton
                        icon="close"
                        variant="ghost"
                        size="sm"
                        aria-label={`Убрать термин ${index + 1}`}
                        disabled={disabled || entries.length === 1}
                        onClick={() =>
                            onChange(entries.filter((_, entryIndex) => entryIndex !== index))
                        }
                    />
                </div>
            ))}
            <Button
                variant="ghost"
                size="sm"
                iconLeft="plus"
                disabled={disabled}
                onClick={() => onChange([...entries, { term: "", definition: "" }])}
            >
                Ещё термин
            </Button>
        </div>
    );
}
