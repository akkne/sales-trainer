"use client";

import { AnimatePresence, motion } from "framer-motion";
import { useState } from "react";
import { useCompleteOnboarding, useSkillsForOnboarding } from "@/features/auth/hooks/use-onboarding";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { Wordmark } from "@/shared/components/wordmark";

const PERSONA_OPTIONS = [
    { id: "sdr", label: "SDR", desc: "Холодный outbound, квалификация", shape: "square" },
    { id: "account_executive", label: "Account Executive", desc: "Переговоры, закрытие", shape: "circle" },
    { id: "account_manager", label: "Account Manager", desc: "Развитие клиента, upsell", shape: "triangle" },
    { id: "founder", label: "Основатель", desc: "Founder-led sales", shape: "diamond" },
    { id: "other", label: "Другое", desc: "Продажи в свободной форме", shape: "arc" },
];

const SALES_TYPE_OPTIONS = [
    { id: "b2b_saas", label: "B2B SaaS" },
    { id: "enterprise", label: "Enterprise / complex" },
    { id: "smb", label: "SMB / транзакционные" },
    { id: "services", label: "Услуги / консалтинг" },
    { id: "agency", label: "Агентство" },
    { id: "other", label: "Другое" },
];

const EXPERIENCE_OPTIONS = [
    { id: "0-1", label: "< 1 года", desc: "только начинаю" },
    { id: "1-3", label: "1–3 года", desc: "есть рутина" },
    { id: "3-5", label: "3–5 лет", desc: "понимаю систему" },
    { id: "5+", label: "5+ лет", desc: "руковожу командой" },
];

const DEFAULT_SKILL_SLUG = "sales-basics";

function PersonaShape({ shape, selected }: { shape: string; selected: boolean }) {
    const c = selected ? "var(--primary)" : "var(--ink-3)";
    return (
        <svg width={22} height={22} viewBox="0 0 26 26" fill="none" aria-hidden>
            {shape === "square" && <rect x="4" y="4" width="18" height="18" rx="2" fill={c} />}
            {shape === "circle" && <circle cx="13" cy="13" r="9" fill={c} />}
            {shape === "triangle" && <polygon points="13,3 23,22 3,22" fill={c} />}
            {shape === "diamond" && <polygon points="13,2 24,13 13,24 2,13" fill={c} />}
            {shape === "arc" && <path d="M4 20 Q 13 2 22 20 Z" fill={c} />}
        </svg>
    );
}

export default function OnboardingPage() {
    const [step, setStep] = useState(0);
    const [selectedPersona, setSelectedPersona] = useState("");
    const [selectedSalesType, setSelectedSalesType] = useState("");
    const [selectedExperience, setSelectedExperience] = useState("");
    const [selectedSlugs, setSelectedSlugs] = useState<Set<string>>(new Set([DEFAULT_SKILL_SLUG]));

    const completeOnboardingMutation = useCompleteOnboarding();
    const { data: allSkills, isLoading: skillsLoading } = useSkillsForOnboarding();

    const totalSteps = 4;

    function next() {
        if (step < totalSteps - 1) setStep(step + 1);
        else handleSubmit();
    }

    function back() {
        if (step > 0) setStep(step - 1);
    }

    function toggleSkill(slug: string) {
        if (slug === DEFAULT_SKILL_SLUG) return;
        setSelectedSlugs((prev) => {
            const next = new Set(prev);
            if (next.has(slug)) next.delete(slug);
            else next.add(slug);
            return next;
        });
    }

    function handleSubmit() {
        completeOnboardingMutation.mutate({
            salesType: selectedSalesType,
            experienceLevel: selectedExperience,
            selectedSkillSlugs: Array.from(selectedSlugs),
            persona: selectedPersona || undefined,
        });
    }

    const canContinue =
        step === 0 ||
        (step === 1 && !!selectedSalesType) ||
        (step === 2 && !!selectedExperience) ||
        (step === 3 && selectedSlugs.size > 0);

    const STEP_LABELS = ["Кто вы", "Что продаёте", "Опыт", "Навыки"];

    return (
        <div className="ob-shell">
            {/* Top bar */}
            <div className="ob-topbar">
                <div className="ob-wordmark">
                    <Wordmark size={22} />
                </div>
                <div className="ob-progress" role="progressbar" aria-valuenow={step + 1} aria-valuemax={totalSteps}>
                    {Array.from({ length: totalSteps }).map((_, i) => (
                        <div
                            key={i}
                            className={`ob-dot${i === step ? " active" : i < step ? " done" : ""}`}
                            style={{ width: i === step ? 28 : 8 }}
                        />
                    ))}
                    <span className="ob-step-label">{step + 1} / {totalSteps}</span>
                </div>
                <button className="ob-skip" onClick={handleSubmit} type="button">
                    Пропустить
                </button>
            </div>

            {/* Step body */}
            <div className="ob-body">
                <div className="ob-content">
                    <AnimatePresence mode="wait">
                        {/* Step 0: Persona */}
                        {step === 0 && (
                            <motion.div
                                key="step-persona"
                                initial={{ x: 40, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                exit={{ x: -40, opacity: 0 }}
                                transition={{ duration: 0.22 }}
                            >
                                <div className="ob-step-head">
                                    <div className="ob-eyebrow">{STEP_LABELS[0]}</div>
                                    <h1 className="ob-title">Кто вы в продажах?</h1>
                                    <p className="ob-desc">Настроим сценарии под вашу роль. Можно поменять позже.</p>
                                </div>
                                <div className="ob-persona-grid">
                                    {PERSONA_OPTIONS.map((p) => {
                                        const selected = selectedPersona === p.id;
                                        return (
                                            <button
                                                key={p.id}
                                                onClick={() => setSelectedPersona(p.id)}
                                                className={`ob-persona-btn${selected ? " selected" : ""}`}
                                                type="button"
                                            >
                                                <div className="ob-persona-icon">
                                                    <PersonaShape shape={p.shape} selected={selected} />
                                                </div>
                                                <div>
                                                    <div className="ob-persona-label">{p.label}</div>
                                                    <div className="ob-persona-desc">{p.desc}</div>
                                                </div>
                                            </button>
                                        );
                                    })}
                                </div>
                            </motion.div>
                        )}

                        {/* Step 1: Sales type */}
                        {step === 1 && (
                            <motion.div
                                key="step-sales"
                                initial={{ x: 40, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                exit={{ x: -40, opacity: 0 }}
                                transition={{ duration: 0.22 }}
                            >
                                <div className="ob-step-head">
                                    <div className="ob-eyebrow">{STEP_LABELS[1]}</div>
                                    <h1 className="ob-title">Что вы продаёте?</h1>
                                </div>
                                <div className="ob-sales-grid">
                                    {SALES_TYPE_OPTIONS.map((t) => {
                                        const sel = selectedSalesType === t.id;
                                        return (
                                            <button
                                                key={t.id}
                                                onClick={() => setSelectedSalesType(t.id)}
                                                className={`ob-sales-btn${sel ? " selected" : ""}`}
                                                type="button"
                                            >
                                                {t.label}
                                            </button>
                                        );
                                    })}
                                </div>
                            </motion.div>
                        )}

                        {/* Step 2: Experience */}
                        {step === 2 && (
                            <motion.div
                                key="step-exp"
                                initial={{ x: 40, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                exit={{ x: -40, opacity: 0 }}
                                transition={{ duration: 0.22 }}
                            >
                                <div className="ob-step-head">
                                    <div className="ob-eyebrow">{STEP_LABELS[2]}</div>
                                    <h1 className="ob-title">Сколько лет в продажах?</h1>
                                </div>
                                <div className="ob-exp-grid">
                                    {EXPERIENCE_OPTIONS.map((l) => {
                                        const sel = selectedExperience === l.id;
                                        return (
                                            <button
                                                key={l.id}
                                                onClick={() => setSelectedExperience(l.id)}
                                                className={`ob-exp-btn${sel ? " selected" : ""}`}
                                                type="button"
                                            >
                                                <div className="ob-exp-value">{l.label}</div>
                                                <div className="ob-exp-desc">{l.desc}</div>
                                            </button>
                                        );
                                    })}
                                </div>
                            </motion.div>
                        )}

                        {/* Step 3: Skills */}
                        {step === 3 && (
                            <motion.div
                                key="step-skills"
                                initial={{ x: 40, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                exit={{ x: -40, opacity: 0 }}
                                transition={{ duration: 0.22 }}
                            >
                                <div className="ob-step-head">
                                    <div className="ob-eyebrow">{STEP_LABELS[3]}</div>
                                    <h1 className="ob-title">С чего начнём?</h1>
                                    <p className="ob-desc">Выберите 2–3 навыка. Остальные разблокируются по ходу.</p>
                                </div>

                                {skillsLoading ? (
                                    <div className="ob-skills-grid">
                                        {[1, 2, 3, 4, 5, 6].map((i) => (
                                            <div key={i} className="ob-skill-skeleton" />
                                        ))}
                                    </div>
                                ) : !allSkills || allSkills.length === 0 ? (
                                    <p style={{ textAlign: "center", color: "var(--ink-3)", fontSize: 14, lineHeight: 1.6 }}>
                                        Навыки пока не загрузились. Нажмите «Начать» — они появятся
                                        в дереве навыков, и вы сможете выбрать их там.
                                    </p>
                                ) : (
                                    <div className="ob-skills-grid">
                                        {allSkills.map((skill) => {
                                            const sel = selectedSlugs.has(skill.slug);
                                            const isDefault = skill.slug === DEFAULT_SKILL_SLUG;
                                            return (
                                                <button
                                                    key={skill.skillId}
                                                    onClick={() => toggleSkill(skill.slug)}
                                                    disabled={isDefault}
                                                    className={`ob-skill-btn${sel ? " selected" : ""}`}
                                                    type="button"
                                                >
                                                    <div className="ob-skill-icon">
                                                        <Icon name="target" size="sm" />
                                                    </div>
                                                    <div className="ob-skill-name">{skill.title}</div>
                                                    <div className="ob-skill-check">
                                                        {sel && <Icon name="check" size="xs" color="white" />}
                                                    </div>
                                                </button>
                                            );
                                        })}
                                    </div>
                                )}

                                {completeOnboardingMutation.isError && (
                                    <p className="ob-error">
                                        Произошла ошибка. Попробуй ещё раз.
                                    </p>
                                )}
                            </motion.div>
                        )}
                    </AnimatePresence>
                </div>
            </div>

            {/* Bottom bar */}
            <div className="ob-bottombar">
                <Button variant="ghost" onClick={back} iconLeft="chevron-left" disabled={step === 0}>
                    Назад
                </Button>
                <Button
                    variant="accent"
                    size="lg"
                    onClick={next}
                    iconRightName="arrow-right"
                    disabled={!canContinue || completeOnboardingMutation.isPending}
                    loading={completeOnboardingMutation.isPending}
                >
                    {step === totalSteps - 1 ? "Начать" : "Продолжить"}
                </Button>
            </div>
        </div>
    );
}
