"use client";

import { useState } from "react";
import { Icon } from "@/shared/components/icon";

interface PrecallPanelProps {
    hasDescription: boolean;
    recentGoals: string[];
    onCall: (goal: string) => void;
}

/** Pre-call goal step: goal input + recent-goal chips + "Позвонить" CTA (§4.1 of the design spec). */
export function PrecallPanel({ hasDescription, recentGoals, onCall }: PrecallPanelProps) {
    const [goal, setGoal] = useState("");

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
                />
                <button className="btn btn-dark" onClick={() => onCall(goal.trim())}>
                    <Icon name="phone" size={16} />
                    Позвонить
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

            <p className="co-cta-help">
                {hasDescription
                    ? "ИИ сыграет сотрудника этой компании, используя описание выше и указанную цель."
                    : "Совет: заполните описание компании — звонок станет реалистичнее."}
            </p>
        </div>
    );
}
