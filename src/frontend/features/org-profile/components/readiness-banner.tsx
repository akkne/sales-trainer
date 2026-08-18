"use client";

import { Icon } from "@/shared/components/icon";
import { formatOptionalQuestionCount } from "../utils/russian-counts";

interface ReadinessBannerProps {
    isReadyForParameterization: boolean;
    /** Gaps that remain once nothing blocking is left. Shown only in the ready state. */
    remainingOptionalGapCount: number;
}

/**
 * The bar at the top of the profile screen. It has exactly two states, «нет» and «да», and no
 * percentage between them.
 *
 * A progress bar here would lie in both directions: two of the seven fields may honestly have no
 * answer, so «5 из 7» can be a finished profile, and one missing blocking field is enough for every
 * lesson in the product to read «ваш продукт» however full the other six are. What the customer
 * needs to know is binary, so it is shown as binary.
 */
export function ReadinessBanner({
    isReadyForParameterization,
    remainingOptionalGapCount,
}: ReadinessBannerProps) {
    if (!isReadyForParameterization) {
        return (
            <div className="rounded-2xl border border-line bg-bg-2 p-4" role="status">
                <div className="flex items-center gap-2 mb-1">
                    <span className="w-2 h-2 rounded-full bg-ink-4" aria-hidden />
                    <span className="text-sm font-medium text-ink">
                        Готов к подстановке: нет
                    </span>
                </div>
                <p className="text-sm text-ink-3">
                    Уроки пока говорят «ваш продукт» и «ваш клиент». Ответьте на вопросы ниже — и они
                    начнут говорить про вас.
                </p>
            </div>
        );
    }

    return (
        <div className="rounded-2xl border border-good/40 bg-good-soft p-4" role="status">
            <div className="flex items-center gap-2 mb-1">
                <Icon name="check" size="sm" className="text-good" />
                <span className="text-sm font-medium text-ink">
                    Уроки говорят про ваш продукт
                </span>
            </div>
            <p className="text-sm text-ink-3">
                {remainingOptionalGapCount > 0
                    ? `Осталось ещё ${formatOptionalQuestionCount(remainingOptionalGapCount)} — они ничего не блокируют.`
                    : "Профиль заполнен: подстановка работает во всех уроках и у собеседника-ИИ."}
            </p>
        </div>
    );
}
