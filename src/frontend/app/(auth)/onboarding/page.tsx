"use client";

import { AnimatePresence, motion } from "framer-motion";
import { useState } from "react";
import { useCompleteOnboarding, useSkillsForOnboarding } from "@/lib/hooks/useOnboarding";

const SALES_TYPE_OPTIONS = [
    { value: "b2b_saas", label: "B2B SaaS", emoji: "💻" },
    { value: "retail", label: "Розница", emoji: "🛒" },
    { value: "real_estate", label: "Недвижимость", emoji: "🏠" },
    { value: "finance", label: "Финансы", emoji: "💰" },
    { value: "b2c", label: "B2C", emoji: "👥" },
];

const EXPERIENCE_LEVEL_OPTIONS = [
    { value: "beginner", label: "Новичок", description: "Менее 1 года в продажах" },
    { value: "experienced", label: "Опытный", description: "1–5 лет в продажах" },
    { value: "manager", label: "Руководитель", description: "Управляю командой продаж" },
];

const DEFAULT_SKILL_SLUG = "sales-basics";

const SLIDE_VARIANTS = {
    enter: { x: 60, opacity: 0 },
    center: { x: 0, opacity: 1 },
    exit: { x: -60, opacity: 0 },
};

export default function OnboardingPage() {
    const [currentStep, setCurrentStep] = useState(0);
    const [selectedSalesType, setSelectedSalesType] = useState("");
    const [selectedExperienceLevel, setSelectedExperienceLevel] = useState("");
    const [selectedSlugs, setSelectedSlugs] = useState<Set<string>>(
        new Set([DEFAULT_SKILL_SLUG])
    );
    const completeOnboardingMutation = useCompleteOnboarding();
    const { data: allSkills, isLoading: skillsLoading } = useSkillsForOnboarding();

    const totalStepCount = 3;

    function handleSalesTypeSelection(salesType: string) {
        setSelectedSalesType(salesType);
        setCurrentStep(1);
    }

    function handleExperienceLevelSelection(experienceLevel: string) {
        setSelectedExperienceLevel(experienceLevel);
        setCurrentStep(2);
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
        });
    }

    return (
        <div className="w-full max-w-lg px-4">
            {/* Progress bar */}
            <div className="flex gap-2 mb-8">
                {Array.from({ length: totalStepCount }).map((_, i) => (
                    <div
                        key={i}
                        className={`h-2 flex-1 rounded-full transition-colors duration-300 ${
                            i <= currentStep ? "bg-[#58CC02]" : "bg-gray-200"
                        }`}
                    />
                ))}
            </div>

            <AnimatePresence mode="wait">
                {/* ── Step 0: Sales type ───────────────────────────────────── */}
                {currentStep === 0 && (
                    <motion.div
                        key="step-sales-type"
                        variants={SLIDE_VARIANTS}
                        initial="enter"
                        animate="center"
                        exit="exit"
                        transition={{ duration: 0.25 }}
                    >
                        <h1 className="font-[var(--font-space-grotesk)] text-2xl font-bold text-gray-900 mb-2">
                            Чем занимаешься?
                        </h1>
                        <p className="text-gray-500 mb-6">Выбери тип продаж</p>
                        <div className="flex flex-col gap-3">
                            {SALES_TYPE_OPTIONS.map((opt) => (
                                <button
                                    key={opt.value}
                                    onClick={() => handleSalesTypeSelection(opt.value)}
                                    className="flex items-center gap-4 px-5 py-4 rounded-2xl bg-[#F7F7F7] text-left font-semibold text-gray-900 hover:bg-[#E8F9D6] transition-colors"
                                >
                                    <span className="text-2xl">{opt.emoji}</span>
                                    {opt.label}
                                </button>
                            ))}
                        </div>
                    </motion.div>
                )}

                {/* ── Step 1: Experience level ─────────────────────────────── */}
                {currentStep === 1 && (
                    <motion.div
                        key="step-experience"
                        variants={SLIDE_VARIANTS}
                        initial="enter"
                        animate="center"
                        exit="exit"
                        transition={{ duration: 0.25 }}
                    >
                        <h1 className="font-[var(--font-space-grotesk)] text-2xl font-bold text-gray-900 mb-2">
                            Твой опыт?
                        </h1>
                        <p className="text-gray-500 mb-6">Это поможет подобрать упражнения</p>
                        <div className="flex flex-col gap-3">
                            {EXPERIENCE_LEVEL_OPTIONS.map((opt) => (
                                <button
                                    key={opt.value}
                                    onClick={() => handleExperienceLevelSelection(opt.value)}
                                    className="flex flex-col px-5 py-4 rounded-2xl bg-[#F7F7F7] text-left hover:bg-[#E8F9D6] transition-colors"
                                >
                                    <span className="font-semibold text-gray-900">{opt.label}</span>
                                    <span className="text-sm text-gray-500 mt-0.5">{opt.description}</span>
                                </button>
                            ))}
                        </div>
                    </motion.div>
                )}

                {/* ── Step 2: Skill selection ──────────────────────────────── */}
                {currentStep === 2 && (
                    <motion.div
                        key="step-skills"
                        variants={SLIDE_VARIANTS}
                        initial="enter"
                        animate="center"
                        exit="exit"
                        transition={{ duration: 0.25 }}
                    >
                        <h1 className="font-[var(--font-space-grotesk)] text-2xl font-bold text-gray-900 mb-2">
                            Что будешь изучать?
                        </h1>
                        <p className="text-gray-500 mb-6">
                            Выбери навыки — начнёшь с них. Можно изменить в профиле.
                        </p>

                        {skillsLoading ? (
                            <div className="flex flex-col gap-3">
                                {[1, 2, 3, 4].map((i) => (
                                    <div key={i} className="h-16 rounded-2xl bg-[#F7F7F7] animate-pulse" />
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
                                            className={`flex items-center gap-4 px-5 py-4 rounded-2xl text-left transition-all border-2 ${
                                                isSelected
                                                    ? "border-[#58CC02] bg-[#E8F9D6]"
                                                    : "border-transparent bg-[#F7F7F7] hover:bg-gray-100"
                                            } ${isDefault ? "cursor-default" : "cursor-pointer"}`}
                                        >
                                            <span className="text-2xl shrink-0">
                                                {skill.iconName || "📚"}
                                            </span>
                                            <div className="flex-1 min-w-0">
                                                <p
                                                    className={`font-semibold truncate ${
                                                        isSelected ? "text-[#3C8400]" : "text-gray-800"
                                                    }`}
                                                >
                                                    {skill.title}
                                                </p>
                                                {isDefault && (
                                                    <p className="text-xs text-[#58CC02] font-medium mt-0.5">
                                                        Базовый — всегда включён
                                                    </p>
                                                )}
                                            </div>
                                            {/* Toggle indicator */}
                                            <div
                                                className={`w-12 h-6 rounded-full transition-colors shrink-0 flex items-center px-1 ${
                                                    isSelected ? "bg-[#58CC02]" : "bg-gray-200"
                                                }`}
                                            >
                                                <div
                                                    className={`w-4 h-4 rounded-full bg-white shadow transition-transform ${
                                                        isSelected ? "translate-x-6" : "translate-x-0"
                                                    }`}
                                                />
                                            </div>
                                        </button>
                                    );
                                })}

                                {(!allSkills || allSkills.length === 0) && (
                                    <div className="text-center py-6 text-gray-400">
                                        Навыки ещё не добавлены администратором
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Selected count hint */}
                        <p className="text-xs text-center text-[#AFAFAF] mb-5">
                            Выбрано: {selectedSlugs.size} навык(а)
                        </p>

                        <button
                            onClick={handleSubmitSkills}
                            disabled={completeOnboardingMutation.isPending}
                            className="w-full py-4 rounded-2xl bg-[#58CC02] text-white font-bold text-base shadow-[0_4px_0_0_#58A700] active:translate-y-1 active:shadow-none transition-all disabled:opacity-60"
                        >
                            {completeOnboardingMutation.isPending ? "Сохраняем..." : "Начать обучение"}
                        </button>

                        {completeOnboardingMutation.isError && (
                            <p className="mt-4 text-red-500 text-sm text-center">
                                Произошла ошибка. Попробуй ещё раз.
                            </p>
                        )}
                    </motion.div>
                )}
            </AnimatePresence>
        </div>
    );
}
