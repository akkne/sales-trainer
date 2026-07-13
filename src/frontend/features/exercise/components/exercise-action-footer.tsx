"use client";

import { Icon } from "@/shared/components/icon";

interface ExerciseActionFooterProps {
    onSkip?: () => void;
    onSubmit: () => void;
    submitLabel?: string;
    canSubmit: boolean;
    isSubmitting: boolean;
    /** Optional keyboard hint shown on pointer-fine devices. */
    keyboardHint?: string;
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
}: ExerciseActionFooterProps) {
    const disabled = !canSubmit || isSubmitting;

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
