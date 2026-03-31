"use client";

import { AnimatePresence, motion } from "framer-motion";
import { useState } from "react";
import { useCompleteOnboarding } from "@/lib/hooks/useOnboarding";

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

const GOAL_OPTIONS = [
    { value: "close_deals", label: "Научиться закрывать сделки" },
    { value: "cold_calls", label: "Улучшить холодные звонки" },
    { value: "objections", label: "Работать с возражениями" },
    { value: "everything", label: "Прокачать всё" },
];

const SLIDE_VARIANTS = {
    enter: { x: 60, opacity: 0 },
    center: { x: 0, opacity: 1 },
    exit: { x: -60, opacity: 0 },
};

export default function OnboardingPage() {
    const [currentStep, setCurrentStep] = useState(0);
    const [selectedSalesType, setSelectedSalesType] = useState("");
    const [selectedExperienceLevel, setSelectedExperienceLevel] = useState("");
    const [selectedGoal, setSelectedGoal] = useState("");
    const completeOnboardingMutation = useCompleteOnboarding();

    function handleSalesTypeSelection(salesType: string) {
        setSelectedSalesType(salesType);
        setCurrentStep(1);
    }

    function handleExperienceLevelSelection(experienceLevel: string) {
        setSelectedExperienceLevel(experienceLevel);
        setCurrentStep(2);
    }

    function handleGoalSelection(goal: string) {
        setSelectedGoal(goal);
        completeOnboardingMutation.mutate({
            salesType: selectedSalesType,
            experienceLevel: selectedExperienceLevel,
            goal,
        });
    }

    const totalStepCount = 3;

    return (
        <div className="w-full max-w-lg px-4">
            <div className="flex gap-2 mb-8">
                {Array.from({ length: totalStepCount }).map((_, stepIndex) => (
                    <div
                        key={stepIndex}
                        className={`h-2 flex-1 rounded-full transition-colors duration-300 ${
                            stepIndex <= currentStep ? "bg-[#58CC02]" : "bg-gray-200"
                        }`}
                    />
                ))}
            </div>

            <AnimatePresence mode="wait">
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
                            {SALES_TYPE_OPTIONS.map((salesTypeOption) => (
                                <button
                                    key={salesTypeOption.value}
                                    onClick={() =>
                                        handleSalesTypeSelection(salesTypeOption.value)
                                    }
                                    className="flex items-center gap-4 px-5 py-4 rounded-2xl bg-[#F7F7F7] text-left font-semibold text-gray-900 hover:bg-[#E8F9D6] transition-colors"
                                >
                                    <span className="text-2xl">{salesTypeOption.emoji}</span>
                                    {salesTypeOption.label}
                                </button>
                            ))}
                        </div>
                    </motion.div>
                )}

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
                            {EXPERIENCE_LEVEL_OPTIONS.map((experienceLevelOption) => (
                                <button
                                    key={experienceLevelOption.value}
                                    onClick={() =>
                                        handleExperienceLevelSelection(experienceLevelOption.value)
                                    }
                                    className="flex flex-col px-5 py-4 rounded-2xl bg-[#F7F7F7] text-left hover:bg-[#E8F9D6] transition-colors"
                                >
                                    <span className="font-semibold text-gray-900">
                                        {experienceLevelOption.label}
                                    </span>
                                    <span className="text-sm text-gray-500 mt-0.5">
                                        {experienceLevelOption.description}
                                    </span>
                                </button>
                            ))}
                        </div>
                    </motion.div>
                )}

                {currentStep === 2 && (
                    <motion.div
                        key="step-goal"
                        variants={SLIDE_VARIANTS}
                        initial="enter"
                        animate="center"
                        exit="exit"
                        transition={{ duration: 0.25 }}
                    >
                        <h1 className="font-[var(--font-space-grotesk)] text-2xl font-bold text-gray-900 mb-2">
                            Что хочешь прокачать?
                        </h1>
                        <p className="text-gray-500 mb-6">Выбери главную цель</p>
                        <div className="flex flex-col gap-3">
                            {GOAL_OPTIONS.map((goalOption) => (
                                <button
                                    key={goalOption.value}
                                    onClick={() => handleGoalSelection(goalOption.value)}
                                    disabled={completeOnboardingMutation.isPending}
                                    className="px-5 py-4 rounded-2xl bg-[#F7F7F7] text-left font-semibold text-gray-900 hover:bg-[#E8F9D6] transition-colors disabled:opacity-60"
                                >
                                    {goalOption.label}
                                </button>
                            ))}
                        </div>

                        {completeOnboardingMutation.isError && (
                            <p className="mt-4 text-red-500 text-sm">
                                Произошла ошибка. Попробуй ещё раз.
                            </p>
                        )}
                    </motion.div>
                )}
            </AnimatePresence>
        </div>
    );
}
