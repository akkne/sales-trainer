"use client";

import { Icon } from "@/shared/components/icon";
import { useEnterAction } from "@/features/exercise/hooks/use-enter-action";

interface ExerciseActionFooterProps {
    onSkip?: () => void;
    onSubmit: () => void;
    submitLabel?: string;
    canSubmit: boolean;
    isSubmitting: boolean;
    /** Optional keyboard hint shown on pointer-fine devices. */
    keyboardHint?: string;
    /**
     * Set when the last submit attempt was rejected by the server. Shown above the button
     * row so a failed check reads as "try again", not as "the button did nothing" — every
     * exercise type that renders this shared footer gets the message for free instead of
     * needing its own copy (docs/AUDIT_SILENT_WRITES.md W-2).
     */
    submitError?: Error | null;
}

/**
 * Shared pre-submit footer for exercises, styled with the .session-foot tokens.
 * Renders an optional "Skip" ghost button and the primary submit button.
 */
export function ExerciseActionFooter({
    onSkip,
    onSubmit,
    submitLabel = "Проверить",
    canSubmit,
    isSubmitting,
    keyboardHint,
    submitError,
}: ExerciseActionFooterProps) {
    const disabled = !canSubmit || isSubmitting;

    // Enter anywhere on the page presses "Проверить" (see the hook for guards).
    useEnterAction(disabled ? null : onSubmit);

    return (
        <div
            className="session-foot"
            style={{
                position: "fixed",
                bottom: 0,
                left: 0,
                right: 0,
                paddingBottom: "max(18px, env(safe-area-inset-bottom))",
            }}
        >
            {submitError && (
                <div className="session-foot-inner" style={{ paddingBottom: 10 }}>
                    <p style={{ margin: 0, fontSize: 13, color: "var(--heart)" }} role="alert">
                        Произошла ошибка при проверке. Попробуй ещё раз.
                    </p>
                </div>
            )}
            <div className="session-foot-inner between grow">
                {onSkip ? (
                    <button className="btn btn-ghost" onClick={onSkip} disabled={isSubmitting}>
                        Пропустить
                    </button>
                ) : (
                    <span />
                )}
                <div className="row gap-4">
                    {keyboardHint && (
                        <div
                            style={{ fontSize: 11, color: "var(--ink-4)", fontFamily: "var(--font-mono)", display: "none" }}
                            data-keyboard-hint
                        >
                            {keyboardHint}
                        </div>
                    )}
                    <button
                        className="btn btn-primary btn-lg"
                        onClick={onSubmit}
                        disabled={disabled}
                        style={disabled ? { opacity: 0.5, pointerEvents: "none" } : undefined}
                    >
                        {isSubmitting ? (
                            <span
                                style={{
                                    width: 18,
                                    height: 18,
                                    border: "2px solid currentColor",
                                    borderTopColor: "transparent",
                                    borderRadius: "50%",
                                    animation: "spin 0.8s linear infinite",
                                }}
                            />
                        ) : (
                            <>
                                {submitLabel}
                                <Icon name="arrow-right" size={18} />
                            </>
                        )}
                    </button>
                </div>
            </div>
            {keyboardHint && (
                <style jsx global>{`
                    @media (pointer: fine) {
                        [data-keyboard-hint] {
                            display: block !important;
                        }
                    }
                `}</style>
            )}
        </div>
    );
}
