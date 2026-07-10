"use client";

import { useState } from "react";
import { Icon } from "@/shared/components/icon";
import type {
    CompanyPersona,
    GeneratedPersona,
    GeneratePersonaPayload,
    PersonaDifficulty,
} from "@/features/companies/hooks/use-company-personas";

export interface SelectedPersona {
    name: string;
    position: string;
    personality: string;
    difficulty: PersonaDifficulty;
}

interface PrecallPanelProps {
    hasDescription: boolean;
    recentGoals: string[];
    onCall: (goal: string, persona: SelectedPersona | null) => void;
    onChat: (goal: string, persona: SelectedPersona | null) => void;
    personas: CompanyPersona[];
    onGeneratePersona: (payload: GeneratePersonaPayload) => Promise<GeneratedPersona>;
    isGeneratingPersona?: boolean;
    onSavePersona: (payload: GeneratedPersona & { difficulty: PersonaDifficulty }) => void;
    isSavingPersona?: boolean;
}

const DIFFICULTY_OPTIONS: { value: PersonaDifficulty; label: string }[] = [
    { value: "Easy", label: "Лёгкий" },
    { value: "Medium", label: "Средний" },
    { value: "Hard", label: "Сложный" },
];

export function PrecallPanel({
    hasDescription,
    recentGoals,
    onCall,
    onChat,
    personas,
    onGeneratePersona,
    isGeneratingPersona = false,
    onSavePersona,
    isSavingPersona = false,
}: PrecallPanelProps) {
    const [goal, setGoal] = useState("");
    const [selectedPersonaId, setSelectedPersonaId] = useState<string | null>(null);
    const [isGenerateOpen, setGenerateOpen] = useState(false);
    const [contactName, setContactName] = useState("");
    const [contactPosition, setContactPosition] = useState("");
    const [difficulty, setDifficulty] = useState<PersonaDifficulty>("Medium");
    const [draftPersona, setDraftPersona] = useState<GeneratedPersona | null>(null);
    const [generateError, setGenerateError] = useState<string | null>(null);

    const selectedPersona: CompanyPersona | null =
        personas.find((persona) => persona.id === selectedPersonaId) ?? null;

    const toSelectedPersona = (): SelectedPersona | null =>
        selectedPersona
            ? {
                  name: selectedPersona.name,
                  position: selectedPersona.position,
                  personality: selectedPersona.personality,
                  difficulty: selectedPersona.difficulty,
              }
            : null;

    const handleGenerate = async () => {
        setGenerateError(null);
        try {
            const generated = await onGeneratePersona({
                contactName: contactName.trim() || undefined,
                contactPosition: contactPosition.trim() || undefined,
                difficulty,
            });
            setDraftPersona(generated);
        } catch (error) {
            setGenerateError(error instanceof Error ? error.message : "Не удалось сгенерировать собеседника");
        }
    };

    const handleSaveDraft = () => {
        if (!draftPersona) return;
        onSavePersona({ ...draftPersona, difficulty });
        setDraftPersona(null);
        setGenerateOpen(false);
    };

    return (
        <div className="co-cta">
            <span className="eyebrow">ТРЕНИРОВКА ПЕРЕД ЗВОНКОМ</span>

            <div className="co-cta-row">
                <input
                    className="field co-goal-input"
                    value={goal}
                    onChange={(event) => setGoal(event.target.value)}
                    placeholder="Цель звонка: напр. договориться о встрече с ЛПР"
                    aria-label="Цель звонка"
                    maxLength={1000}
                />
                <button className="btn btn-dark" onClick={() => onCall(goal.trim(), toSelectedPersona())}>
                    <Icon name="phone" size={16} />
                    Позвонить
                </button>
                <button className="btn btn-outline" onClick={() => onChat(goal.trim(), toSelectedPersona())}>
                    <Icon name="message" size={16} />
                    Чат
                </button>
            </div>

            {recentGoals.length > 0 && (
                <>
                    <p className="co-recent-label">Недавние цели</p>
                    <div className="co-recent-goals">
                        {recentGoals.slice(0, 5).map((recentGoal, index) => (
                            <button
                                key={`${recentGoal}-${index}`}
                                type="button"
                                className="chip-tag"
                                onClick={() => setGoal(recentGoal)}
                            >
                                {recentGoal}
                            </button>
                        ))}
                    </div>
                </>
            )}

            <p className="co-recent-label">Собеседник</p>
            <div className="co-recent-goals">
                <button
                    type="button"
                    className={"chip-tag" + (selectedPersonaId === null ? " active" : "")}
                    onClick={() => setSelectedPersonaId(null)}
                >
                    Без персоны
                </button>
                {personas.map((persona) => (
                    <button
                        key={persona.id}
                        type="button"
                        className={"chip-tag" + (selectedPersonaId === persona.id ? " active" : "")}
                        onClick={() => setSelectedPersonaId(persona.id)}
                    >
                        {persona.name}
                    </button>
                ))}
                <button
                    type="button"
                    className="chip-tag"
                    onClick={() => setGenerateOpen((open) => !open)}
                >
                    <Icon name="sparkle" size="sm" /> Сгенерировать собеседника
                </button>
            </div>

            {isGenerateOpen && (
                <div className="co-persona-generate">
                    <div className="co-cta-row">
                        <input
                            className="field"
                            value={contactName}
                            onChange={(event) => setContactName(event.target.value)}
                            placeholder="Имя контакта (необязательно)"
                            aria-label="Имя контакта"
                            maxLength={200}
                        />
                        <input
                            className="field"
                            value={contactPosition}
                            onChange={(event) => setContactPosition(event.target.value)}
                            placeholder="Должность контакта (необязательно)"
                            aria-label="Должность контакта"
                            maxLength={200}
                        />
                    </div>

                    <div className="co-recent-goals">
                        {DIFFICULTY_OPTIONS.map((option) => (
                            <button
                                key={option.value}
                                type="button"
                                className={"chip-tag" + (difficulty === option.value ? " active" : "")}
                                onClick={() => setDifficulty(option.value)}
                            >
                                {option.label}
                            </button>
                        ))}
                    </div>

                    <button className="btn btn-outline" onClick={handleGenerate} disabled={isGeneratingPersona}>
                        {isGeneratingPersona ? "Генерируем…" : "Сгенерировать"}
                    </button>

                    {generateError && (
                        <p className="small" style={{ color: "var(--heart)" }}>{generateError}</p>
                    )}

                    {draftPersona && (
                        <div className="co-persona-draft">
                            <div className="co-contact-name">{draftPersona.name}</div>
                            <div className="co-contact-position">{draftPersona.position}</div>
                            <p className="small">{draftPersona.personality}</p>
                            <button className="btn btn-dark" onClick={handleSaveDraft} disabled={isSavingPersona}>
                                {isSavingPersona ? "Сохраняем…" : "Сохранить собеседника"}
                            </button>
                        </div>
                    )}
                </div>
            )}

            <p className="co-cta-help">
                {hasDescription
                    ? "ИИ сыграет сотрудника этой компании, используя описание выше и указанную цель."
                    : "Совет: заполните описание компании — звонок станет реалистичнее."}
            </p>
        </div>
    );
}
