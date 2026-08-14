"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { startDialogSession } from "@/features/dialog/hooks/use-dialog";
import {
    SCENARIO_MAX_LENGTH,
    SCENARIO_MIN_LENGTH,
    validateScenario,
} from "@/features/dialog/hooks/use-custom-scenario";

interface CustomScenarioModalProps {
    bundleId: string;
    modeId: string;
    onClose: () => void;
}

const PLACEHOLDER =
    "Например: я продаю CRM небольшим агентствам. Звоню операционному директору, " +
    "который уже пользуется таблицами и не понимает, зачем платить за систему. " +
    "У него мало времени и он не любит, когда ему что-то навязывают.";

export function CustomScenarioModal({ bundleId, modeId, onClose }: CustomScenarioModalProps) {
    const router = useRouter();
    const [scenario, setScenario] = useState("");
    const [error, setError] = useState<string | null>(null);
    const [isSubmitting, setIsSubmitting] = useState(false);

    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape" && !isSubmitting) {
                onClose();
            }
        };

        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [onClose, isSubmitting]);

    const trimmedLength = scenario.trim().length;
    const isLongEnough = trimmedLength >= SCENARIO_MIN_LENGTH;
    const isTooLong = trimmedLength > SCENARIO_MAX_LENGTH;
    const canSubmit = isLongEnough && !isTooLong && !isSubmitting;

    const handleSubmit = async () => {
        if (!canSubmit) return;

        setIsSubmitting(true);
        setError(null);

        try {
            // Checked before the session is created so a rejected scenario never turns into a
            // conversation the user then has to back out of.
            const verdict = await validateScenario(scenario);
            if (!verdict.isValid) {
                setError(verdict.rejectionReason ?? "Недопустимый сценарий: он не связан с продажами.");
                return;
            }

            const session = await startDialogSession(bundleId, modeId, undefined, scenario.trim());
            router.push(`/dialog/${bundleId}/${modeId}?session=${session.id}`);
        } catch (submitError) {
            setError(
                submitError instanceof Error
                    ? submitError.message
                    : "Не удалось начать разговор. Попробуйте ещё раз."
            );
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="modal-overlay" onClick={isSubmitting ? undefined : onClose}>
            <div className="modal fade-up" onClick={(event) => event.stopPropagation()}>
                <div className="modal-head">
                    <div className="row gap-3">
                        <span className="itile primary" style={{ width: 40, height: 40 }}>
                            <Icon name="edit" size="md" />
                        </span>
                        <h2 className="h3">Свой сценарий</h2>
                    </div>
                    <button
                        className="icon-btn"
                        onClick={onClose}
                        disabled={isSubmitting}
                        aria-label="Закрыть"
                    >
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="modal-body">
                    <p className="scenario-help">
                        Опишите ситуацию словами: кому вы продаёте, что за продукт и что мешает
                        сделке. Чем конкретнее собеседник и его возражение, тем ближе разговор
                        будет к реальному.
                    </p>

                    <textarea
                        className="scenario-input"
                        value={scenario}
                        onChange={(event) => {
                            setScenario(event.target.value);
                            if (error) setError(null);
                        }}
                        placeholder={PLACEHOLDER}
                        rows={7}
                        maxLength={SCENARIO_MAX_LENGTH * 2}
                        autoFocus
                        disabled={isSubmitting}
                        aria-invalid={error != null}
                    />

                    <div className="scenario-meta">
                        <span className={isTooLong ? "scenario-count over" : "scenario-count"}>
                            {trimmedLength} / {SCENARIO_MAX_LENGTH}
                        </span>
                        {!isLongEnough && trimmedLength > 0 && (
                            <span className="scenario-count">
                                ещё {SCENARIO_MIN_LENGTH - trimmedLength} симв.
                            </span>
                        )}
                    </div>

                    {error && (
                        <div className="scenario-error" role="alert">
                            <Icon name="warning" size="sm" />
                            <span>{error}</span>
                        </div>
                    )}
                </div>

                <div className="modal-foot row" style={{ justifyContent: "flex-end", gap: 10 }}>
                    <button className="btn btn-ghost" onClick={onClose} disabled={isSubmitting}>
                        Отмена
                    </button>
                    <button className="btn btn-primary" onClick={handleSubmit} disabled={!canSubmit}>
                        {isSubmitting ? "Проверяем…" : "Начать разговор"}
                    </button>
                </div>
            </div>
        </div>
    );
}
