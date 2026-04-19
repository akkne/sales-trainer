"use client";

import { AnimatePresence, motion } from "framer-motion";
import { useState } from "react";
import { useCompleteOnboarding, useSkillsForOnboarding } from "@/lib/hooks/useOnboarding";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";

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
    const c = selected ? "var(--rust)" : "var(--ink)";
    const bg = selected ? "var(--bg)" : "var(--rust-soft)";
    return (
        <div style={{ width: 48, height: 48, borderRadius: 10, background: bg, display: "flex", alignItems: "center", justifyContent: "center" }}>
            <svg width={26} height={26} viewBox="0 0 26 26" fill="none">
                {shape === "square" && <rect x="4" y="4" width="18" height="18" rx="2" fill={c} />}
                {shape === "circle" && <circle cx="13" cy="13" r="9" fill={c} />}
                {shape === "triangle" && <polygon points="13,3 23,22 3,22" fill={c} />}
                {shape === "diamond" && <polygon points="13,2 24,13 13,24 2,13" fill={c} />}
                {shape === "arc" && <path d="M4 20 Q 13 2 22 20 Z" fill={c} />}
            </svg>
        </div>
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
        (step === 0) ||
        (step === 1 && selectedSalesType) ||
        (step === 2 && selectedExperience) ||
        (step === 3 && selectedSlugs.size > 0);

    return (
        <div style={{ minHeight: "100vh", background: "var(--bg)", display: "flex", flexDirection: "column" }}>
            {/* Top bar */}
            <div style={{ padding: "20px 32px", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                <div style={{ fontWeight: 600, fontSize: 18, letterSpacing: -0.5 }}>Sellevate</div>
                <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                    {Array.from({ length: totalSteps }).map((_, i) => (
                        <div
                            key={i}
                            style={{
                                width: i === step ? 28 : 8,
                                height: 8,
                                borderRadius: 4,
                                background: i <= step ? "var(--ink)" : "var(--line-2)",
                                transition: "width 0.3s cubic-bezier(.2,.8,.2,1), background 0.3s",
                            }}
                        />
                    ))}
                    <span style={{ marginLeft: 12, fontFamily: "var(--f-mono)", fontSize: 12, color: "var(--ink-3)" }}>
                        0{step + 1} / 0{totalSteps}
                    </span>
                </div>
                <button
                    onClick={handleSubmit}
                    style={{
                        background: "transparent",
                        border: "none",
                        color: "var(--ink-3)",
                        fontSize: 13,
                        cursor: "pointer",
                        fontFamily: "var(--f-sans)",
                    }}
                >
                    Пропустить
                </button>
            </div>

            {/* Step body */}
            <div style={{ flex: 1, display: "flex", alignItems: "center", justifyContent: "center", padding: 32 }}>
                <div style={{ maxWidth: 880, width: "100%" }}>
                    <AnimatePresence mode="wait">
                        {/* Step 0: Persona */}
                        {step === 0 && (
                            <motion.div
                                key="step-persona"
                                initial={{ x: 60, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                exit={{ x: -60, opacity: 0 }}
                                transition={{ duration: 0.25 }}
                            >
                                <div style={{ textAlign: "center", marginBottom: 48 }}>
                                    <div style={{ fontFamily: "var(--f-mono)", fontSize: 12, color: "var(--rust)", letterSpacing: 2, marginBottom: 12 }}>
                                        ШАГ 01 / 04
                                    </div>
                                    <h1 style={{ fontSize: 48, margin: 0, letterSpacing: -1.5, fontWeight: 500, lineHeight: 1.05 }}>
                                        Кто вы в продажах?
                                    </h1>
                                    <p style={{ color: "var(--ink-3)", fontSize: 16, marginTop: 12 }}>
                                        Настроим сценарии под вашу роль. Можно поменять позже.
                                    </p>
                                </div>
                                <div style={{ display: "grid", gridTemplateColumns: "repeat(5, 1fr)", gap: 12 }}>
                                    {PERSONA_OPTIONS.map((p) => {
                                        const selected = selectedPersona === p.id;
                                        return (
                                            <button
                                                key={p.id}
                                                onClick={() => setSelectedPersona(p.id)}
                                                style={{
                                                    padding: 20,
                                                    textAlign: "left",
                                                    background: selected ? "var(--ink)" : "var(--surface)",
                                                    color: selected ? "var(--bg)" : "var(--ink)",
                                                    border: `1px solid ${selected ? "var(--ink)" : "var(--line)"}`,
                                                    borderRadius: 16,
                                                    cursor: "pointer",
                                                    transition: "all 0.15s",
                                                    boxShadow: selected ? "var(--sh-2)" : "var(--sh-1)",
                                                    minHeight: 180,
                                                    display: "flex",
                                                    flexDirection: "column",
                                                    justifyContent: "space-between",
                                                    fontFamily: "var(--f-sans)",
                                                }}
                                            >
                                                <PersonaShape shape={p.shape} selected={selected} />
                                                <div>
                                                    <div style={{ fontSize: 15, fontWeight: 500, marginBottom: 4 }}>{p.label}</div>
                                                    <div style={{ fontSize: 12, color: selected ? "var(--ink-4)" : "var(--ink-3)", lineHeight: 1.4 }}>
                                                        {p.desc}
                                                    </div>
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
                                initial={{ x: 60, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                exit={{ x: -60, opacity: 0 }}
                                transition={{ duration: 0.25 }}
                            >
                                <div style={{ textAlign: "center", marginBottom: 48 }}>
                                    <div style={{ fontFamily: "var(--f-mono)", fontSize: 12, color: "var(--rust)", letterSpacing: 2, marginBottom: 12 }}>
                                        ШАГ 02 / 04
                                    </div>
                                    <h1 style={{ fontSize: 48, margin: 0, letterSpacing: -1.5, fontWeight: 500, lineHeight: 1.05 }}>
                                        Что вы продаёте?
                                    </h1>
                                </div>
                                <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12, maxWidth: 720, margin: "0 auto" }}>
                                    {SALES_TYPE_OPTIONS.map((t) => {
                                        const sel = selectedSalesType === t.id;
                                        return (
                                            <button
                                                key={t.id}
                                                onClick={() => setSelectedSalesType(t.id)}
                                                style={{
                                                    padding: "20px 24px",
                                                    background: sel ? "var(--rust)" : "var(--surface)",
                                                    color: sel ? "white" : "var(--ink)",
                                                    border: `1px solid ${sel ? "var(--rust)" : "var(--line)"}`,
                                                    borderRadius: 14,
                                                    cursor: "pointer",
                                                    fontSize: 15,
                                                    fontWeight: 500,
                                                    textAlign: "left",
                                                    fontFamily: "var(--f-sans)",
                                                }}
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
                                initial={{ x: 60, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                exit={{ x: -60, opacity: 0 }}
                                transition={{ duration: 0.25 }}
                            >
                                <div style={{ textAlign: "center", marginBottom: 48 }}>
                                    <div style={{ fontFamily: "var(--f-mono)", fontSize: 12, color: "var(--rust)", letterSpacing: 2, marginBottom: 12 }}>
                                        ШАГ 03 / 04
                                    </div>
                                    <h1 style={{ fontSize: 48, margin: 0, letterSpacing: -1.5, fontWeight: 500, lineHeight: 1.05 }}>
                                        Сколько лет в продажах?
                                    </h1>
                                </div>
                                <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 12 }}>
                                    {EXPERIENCE_OPTIONS.map((l) => {
                                        const sel = selectedExperience === l.id;
                                        return (
                                            <button
                                                key={l.id}
                                                onClick={() => setSelectedExperience(l.id)}
                                                style={{
                                                    padding: 28,
                                                    background: sel ? "var(--ink)" : "var(--surface)",
                                                    color: sel ? "var(--bg)" : "var(--ink)",
                                                    border: `1px solid ${sel ? "var(--ink)" : "var(--line)"}`,
                                                    borderRadius: 16,
                                                    cursor: "pointer",
                                                    textAlign: "left",
                                                    fontFamily: "var(--f-sans)",
                                                }}
                                            >
                                                <div style={{ fontSize: 36, fontWeight: 500, letterSpacing: -1, fontFamily: "var(--f-mono)" }}>
                                                    {l.label}
                                                </div>
                                                <div style={{ fontSize: 12, color: sel ? "var(--ink-4)" : "var(--ink-3)", marginTop: 6 }}>
                                                    {l.desc}
                                                </div>
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
                                initial={{ x: 60, opacity: 0 }}
                                animate={{ x: 0, opacity: 1 }}
                                exit={{ x: -60, opacity: 0 }}
                                transition={{ duration: 0.25 }}
                            >
                                <div style={{ textAlign: "center", marginBottom: 48 }}>
                                    <div style={{ fontFamily: "var(--f-mono)", fontSize: 12, color: "var(--rust)", letterSpacing: 2, marginBottom: 12 }}>
                                        ШАГ 04 / 04
                                    </div>
                                    <h1 style={{ fontSize: 48, margin: 0, letterSpacing: -1.5, fontWeight: 500, lineHeight: 1.05 }}>
                                        С чего начнём?
                                    </h1>
                                    <p style={{ color: "var(--ink-3)", fontSize: 16, marginTop: 12 }}>
                                        Выберите 2–3 навыка. Остальные разблокируются по ходу.
                                    </p>
                                </div>

                                {skillsLoading ? (
                                    <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12 }}>
                                        {[1, 2, 3, 4, 5, 6].map((i) => (
                                            <div key={i} style={{ height: 80, borderRadius: 14, background: "var(--surface)", animation: "pulse 2s infinite" }} />
                                        ))}
                                    </div>
                                ) : (
                                    <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12 }}>
                                        {(allSkills ?? []).map((skill) => {
                                            const sel = selectedSlugs.has(skill.slug);
                                            const isDefault = skill.slug === DEFAULT_SKILL_SLUG;
                                            return (
                                                <button
                                                    key={skill.skillId}
                                                    onClick={() => toggleSkill(skill.slug)}
                                                    disabled={isDefault}
                                                    style={{
                                                        padding: 20,
                                                        textAlign: "left",
                                                        background: sel ? "var(--rust-soft)" : "var(--surface)",
                                                        border: `1px solid ${sel ? "var(--rust)" : "var(--line)"}`,
                                                        borderRadius: 14,
                                                        cursor: isDefault ? "default" : "pointer",
                                                        fontFamily: "var(--f-sans)",
                                                        display: "flex",
                                                        alignItems: "center",
                                                        gap: 14,
                                                        opacity: isDefault ? 0.6 : 1,
                                                    }}
                                                >
                                                    <div
                                                        style={{
                                                            width: 44,
                                                            height: 44,
                                                            borderRadius: 10,
                                                            background: sel ? "var(--rust)" : "var(--bg-2)",
                                                            color: sel ? "white" : "var(--ink-2)",
                                                            display: "flex",
                                                            alignItems: "center",
                                                            justifyContent: "center",
                                                        }}
                                                    >
                                                        <Icon name="target" size="sm" />
                                                    </div>
                                                    <div style={{ flex: 1 }}>
                                                        <div style={{ fontSize: 14, fontWeight: 500, color: sel ? "var(--rust-ink)" : "var(--ink)" }}>
                                                            {skill.title}
                                                        </div>
                                                        <div style={{ fontSize: 11, color: "var(--ink-3)", fontFamily: "var(--f-mono)", marginTop: 2 }}>
                                                            {skill.totalLessonCount ?? "—"} уроков
                                                        </div>
                                                    </div>
                                                    <div
                                                        style={{
                                                            width: 20,
                                                            height: 20,
                                                            borderRadius: 6,
                                                            background: sel ? "var(--rust)" : "transparent",
                                                            border: `1.5px solid ${sel ? "var(--rust)" : "var(--line-2)"}`,
                                                            display: "flex",
                                                            alignItems: "center",
                                                            justifyContent: "center",
                                                        }}
                                                    >
                                                        {sel && <Icon name="check" size="xs" color="white" />}
                                                    </div>
                                                </button>
                                            );
                                        })}
                                    </div>
                                )}

                                {completeOnboardingMutation.isError && (
                                    <p style={{ marginTop: 16, color: "var(--bad)", fontSize: 14, textAlign: "center" }}>
                                        Произошла ошибка. Попробуй ещё раз.
                                    </p>
                                )}
                            </motion.div>
                        )}
                    </AnimatePresence>
                </div>
            </div>

            {/* Bottom bar */}
            <div
                style={{
                    padding: 32,
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    borderTop: "1px solid var(--line)",
                    background: "var(--surface)",
                }}
            >
                <Button variant="ghost" onClick={back} iconLeftName="chevron-left" disabled={step === 0}>
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
