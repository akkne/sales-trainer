"use client";

import { AnimatePresence, motion } from "framer-motion";
import { useState } from "react";
import { useCompleteOnboarding, useSkillsForOnboarding } from "@/features/auth/hooks/use-onboarding";
import { Icon } from "@/shared/components/icon";

const PERSONA_OPTIONS = [
    { value: "sdr", label: "SDR", description: "Разведчик продаж — ищу и квалифицирую лиды", icon: "call" },
    { value: "account_executive", label: "Account Executive", description: "Провожу сделки от демо до закрытия", icon: "handshake" },
    { value: "account_manager", label: "Account Manager", description: "Развиваю текущих клиентов", icon: "assignment_turned_in" },
    { value: "founder", label: "Основатель", description: "Продаю лично как CEO или co-founder", icon: "rocket_launch" },
    { value: "other", label: "Другое", description: "Моя роль не вписывается в список", icon: "star" },
];

const SALES_TYPE_OPTIONS = [
    { value: "b2b_saas", label: "B2B SaaS", icon: "cloud_done", popular: true },
    { value: "retail", label: "Розница", icon: "shopping_bag" },
    { value: "real_estate", label: "Недвижимость", icon: "home_work" },
    { value: "finance", label: "Финансы", icon: "account_balance" },
    { value: "b2c", label: "B2C", icon: "diversity_3" },
];

const EXPERIENCE_LEVEL_OPTIONS = [
    { value: "beginner", label: "Новичок", description: "Менее 1 года в продажах", icon: "school" },
    { value: "experienced", label: "Опытный", description: "1–5 лет в продажах", icon: "trending_up" },
    { value: "manager", label: "Руководитель", description: "Управляю командой продаж", icon: "groups" },
];

const DEFAULT_SKILL_SLUG = "sales-basics";

const SLIDE_VARIANTS = {
    enter: { x: 60, opacity: 0 },
    center: { x: 0, opacity: 1 },
    exit: { x: -60, opacity: 0 },
};

export default function OnboardingPage() {
    const [currentStep, setCurrentStep] = useState(0);
    const [selectedPersona, setSelectedPersona] = useState("");
    const [selectedSalesType, setSelectedSalesType] = useState("");
    const [selectedExperienceLevel, setSelectedExperienceLevel] = useState("");
    const [selectedSlugs, setSelectedSlugs] = useState<Set<string>>(
        new Set([DEFAULT_SKILL_SLUG])
    );
    const completeOnboardingMutation = useCompleteOnboarding();
    const { data: allSkills, isLoading: skillsLoading } = useSkillsForOnboarding();

    const totalStepCount = 4;

    function handlePersonaSelection(persona: string) {
        setSelectedPersona(persona);
        setCurrentStep(1);
    }

    function handlePersonaSkip() {
        setCurrentStep(1);
    }

    function handleSalesTypeSelection(salesType: string) {
        setSelectedSalesType(salesType);
        setCurrentStep(2);
    }

    function handleExperienceLevelSelection(experienceLevel: string) {
        setSelectedExperienceLevel(experienceLevel);
        setCurrentStep(3);
    }

    function toggleSkill(slug: string) {
        if (slug === DEFAULT_SKILL_SLUG) return; // sales-basics is always on
        setSelectedSlugs((prev) => {
            const next = new Set(prev);
            if (next.has(slug)) next.delete(slug);
            else next.add(slug);
            return next;
        });
    }

    function handleSubmitSkills() {
        completeOnboardingMutation.mutate({
            salesType: selectedSalesType,
            experienceLevel: selectedExperienceLevel,
            selectedSkillSlugs: Array.from(selectedSlugs),
            persona: selectedPersona || undefined,
        });
    }

    return (
        <div className="w-full max-w-2xl px-4">
            {/* Progress bar */}
            <div className="mb-8">
                <p className="text-sm text-on-surface-variant font-medium mb-2">
                    Шаг {currentStep + 1} из {totalStepCount}
                </p>
                <div className="h-1 bg-surface-container-highest rounded-full overflow-hidden">
                    <div
                        className="h-full bg-primary rounded-full transition-all duration-300"
                        style={{ width: `${((currentStep + 1) / totalStepCount) * 100}%` }}
                    />
                </div>
            </div>

            <AnimatePresence mode="wait">
                {/* ── Step 0: Persona selection ────────────────────────────── */}
                {currentStep === 0 && (
                    <motion.div
                        key="step-persona"
                        variants={SLIDE_VARIANTS}
                        initial="enter"
                        animate="center"
                        exit="exit"
                        transition={{ duration: 0.25 }}
                    >
                        <h1 className="font-headline text-3xl font-bold text-on-surface mb-3">
                            Кто ты в продажах?
                        </h1>
                        <p className="text-on-surface-variant mb-8 max-w-xl">
                            Выбери свою роль — мы подберём упражнения под твои задачи
                        </p>
                        <div className="flex flex-col gap-3">
                            {PERSONA_OPTIONS.map((opt) => (
                                <button
                                    key={opt.value}
                                    onClick={() => handlePersonaSelection(opt.value)}
                                    className="flex items-center gap-4 px-5 py-4 rounded-2xl bg-surface-container text-left hover:bg-surface-container-high tonal-transition group"
                                >
                                    <div className="w-12 h-12 rounded-full bg-surface-container-high flex items-center justify-center group-hover:bg-primary-container tonal-transition">
                                        <Icon name={opt.icon} size="md" className="text-on-surface-variant group-hover:text-primary" />
                                    </div>
                                    <div className="flex-1">
                                        <p className="font-semibold text-on-surface">{opt.label}</p>
                                        <p className="text-sm text-on-surface-variant mt-0.5">{opt.description}</p>
                                    </div>
                                    <Icon name="chevron_right" size="md" className="text-outline" />
                                </button>
                            ))}
                        </div>
                        <button
                            onClick={handlePersonaSkip}
                            className="mt-6 w-full py-3 text-sm text-on-surface-variant hover:text-on-surface tonal-transition"
                        >
                            Пропустить
                        </button>
                    </motion.div>
                )}

                {/* ── Step 1: Sales type (Bento Grid) ───────────────────────── */}
                {currentStep === 1 && (
                    <motion.div
                        key="step-sales-type"
                        variants={SLIDE_VARIANTS}
                        initial="enter"
                        animate="center"
                        exit="exit"
                        transition={{ duration: 0.25 }}
                    >
                        <h1 className="font-headline text-3xl font-bold text-on-surface mb-3">
                            Выбери сферу
                        </h1>
                        <p className="text-on-surface-variant mb-8 max-w-xl">
                            Мы адаптируем контент под специфику твоей индустрии
                        </p>
                        <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
                            {SALES_TYPE_OPTIONS.map((opt) => {
                                const isSelected = selectedSalesType === opt.value;
                                return (
                                    <button
                                        key={opt.value}
                                        onClick={() => handleSalesTypeSelection(opt.value)}
                                        className={`relative flex flex-col items-start p-5 rounded-2xl text-left tonal-transition ${
                                            isSelected
                                                ? "bg-primary-container border-2 border-primary"
                                                : "bg-surface-container border-2 border-transparent hover:border-outline-variant hover:bg-surface-container-high"
                                        }`}
                                    >
                                        {opt.popular && (
                                            <span className="absolute top-3 right-3 text-xs font-semibold bg-primary text-on-primary px-2.5 py-0.5 rounded-full">
                                                Популярно
                                            </span>
                                        )}
                                        <div className={`w-10 h-10 rounded-full flex items-center justify-center mb-3 ${
                                            isSelected ? "bg-primary" : "bg-surface-container-high"
                                        }`}>
                                            <Icon
                                                name={opt.icon}
                                                size="md"
                                                className={isSelected ? "text-on-primary" : "text-on-surface-variant"}
                                            />
                                        </div>
                                        <p className={`font-semibold ${isSelected ? "text-primary" : "text-on-surface"}`}>
                                            {opt.label}
                                        </p>
                                    </button>
                                );
                            })}
                        </div>
                    </motion.div>
                )}

                {/* ── Step 2: Experience level ─────────────────────────────── */}
                {currentStep === 2 && (
                    <motion.div
                        key="step-experience"
                        variants={SLIDE_VARIANTS}
                        initial="enter"
                        animate="center"
                        exit="exit"
                        transition={{ duration: 0.25 }}
                    >
                        <h1 className="font-headline text-3xl font-bold text-on-surface mb-3">
                            Твой опыт?
                        </h1>
                        <p className="text-on-surface-variant mb-8 max-w-xl">
                            Это поможет подобрать уровень сложности
                        </p>
                        <div className="flex flex-col gap-3">
                            {EXPERIENCE_LEVEL_OPTIONS.map((opt) => (
                                <button
                                    key={opt.value}
                                    onClick={() => handleExperienceLevelSelection(opt.value)}
                                    className="flex items-center gap-4 px-5 py-4 rounded-2xl bg-surface-container text-left hover:bg-surface-container-high tonal-transition group"
                                >
                                    <div className="w-12 h-12 rounded-full bg-surface-container-high flex items-center justify-center group-hover:bg-secondary-container tonal-transition">
                                        <Icon name={opt.icon} size="md" className="text-on-surface-variant group-hover:text-secondary" />
                                    </div>
                                    <div className="flex-1">
                                        <p className="font-semibold text-on-surface">{opt.label}</p>
                                        <p className="text-sm text-on-surface-variant mt-0.5">{opt.description}</p>
                                    </div>
                                    <Icon name="chevron_right" size="md" className="text-outline" />
                                </button>
                            ))}
                        </div>
                    </motion.div>
                )}

                {/* ── Step 3: Skill selection ──────────────────────────────── */}
                {currentStep === 3 && (
                    <motion.div
                        key="step-skills"
                        variants={SLIDE_VARIANTS}
                        initial="enter"
                        animate="center"
                        exit="exit"
                        transition={{ duration: 0.25 }}
                    >
                        <h1 className="font-headline text-3xl font-bold text-on-surface mb-3">
                            Что будешь изучать?
                        </h1>
                        <p className="text-on-surface-variant mb-8 max-w-xl">
                            Выбери навыки для старта. Можно изменить позже в профиле.
                        </p>

                        {skillsLoading ? (
                            <div className="flex flex-col gap-3">
                                {[1, 2, 3, 4].map((i) => (
                                    <div key={i} className="h-16 rounded-2xl bg-surface-container animate-pulse" />
                                ))}
                            </div>
                        ) : (
                            <div className="flex flex-col gap-3 mb-6">
                                {(allSkills ?? []).map((skill) => {
                                    const isDefault = skill.slug === DEFAULT_SKILL_SLUG;
                                    const isSelected = selectedSlugs.has(skill.slug);

                                    return (
                                        <button
                                            key={skill.skillId}
                                            onClick={() => toggleSkill(skill.slug)}
                                            disabled={isDefault}
                                            className={`flex items-center gap-4 px-5 py-4 rounded-2xl text-left tonal-transition border-2 ${
                                                isDefault
                                                    ? "border-outline-variant bg-surface-container-low cursor-default opacity-60"
                                                    : isSelected
                                                    ? "border-primary bg-primary-container cursor-pointer"
                                                    : "border-transparent bg-surface-container hover:bg-surface-container-high cursor-pointer"
                                            }`}
                                        >
                                            <div className="flex-1 min-w-0">
                                                <p
                                                    className={`font-semibold truncate ${
                                                        isDefault
                                                            ? "text-on-surface-variant"
                                                            : isSelected
                                                            ? "text-primary"
                                                            : "text-on-surface"
                                                    }`}
                                                >
                                                    {skill.title}
                                                </p>
                                                {isDefault && (
                                                    <p className="text-xs text-on-surface-variant font-medium mt-0.5">
                                                        Базовый — всегда включён
                                                    </p>
                                                )}
                                            </div>
                                            {/* Toggle switch */}
                                            <div
                                                className={`w-12 h-6 rounded-full shrink-0 flex items-center px-1 tonal-transition ${
                                                    isDefault
                                                        ? "bg-outline-variant"
                                                        : isSelected
                                                        ? "bg-primary"
                                                        : "bg-surface-container-highest"
                                                }`}
                                            >
                                                <div
                                                    className={`w-4 h-4 rounded-full bg-surface-container-lowest shadow transition-transform ${
                                                        isDefault || isSelected
                                                            ? "translate-x-6"
                                                            : "translate-x-0"
                                                    }`}
                                                />
                                            </div>
                                        </button>
                                    );
                                })}

                                {(!allSkills || allSkills.length === 0) && (
                                    <div className="text-center py-6 text-on-surface-variant">
                                        Навыки ещё не добавлены администратором
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Info hint */}
                        <div className="flex items-center gap-2 text-sm text-on-surface-variant mb-6">
                            <Icon name="info" size="sm" />
                            <span>Выбрано: {selectedSlugs.size} навык(а)</span>
                        </div>

                        <button
                            onClick={handleSubmitSkills}
                            disabled={completeOnboardingMutation.isPending}
                            className="w-full py-4 rounded-full bg-primary text-on-primary font-bold text-base shadow-[0_4px_0_0_var(--color-primary-dim)] active:translate-y-1 active:shadow-none transition-all disabled:opacity-60 flex items-center justify-center gap-2"
                        >
                            {completeOnboardingMutation.isPending ? (
                                "Сохраняем..."
                            ) : (
                                <>
                                    Начать обучение
                                    <Icon name="arrow_forward" size="sm" />
                                </>
                            )}
                        </button>

                        {completeOnboardingMutation.isError && (
                            <p className="mt-4 text-error text-sm text-center">
                                Произошла ошибка. Попробуй ещё раз.
                            </p>
                        )}
                    </motion.div>
                )}
            </AnimatePresence>
        </div>
    );
}
