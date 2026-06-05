"use client";

import { useEffect } from "react";

interface KeyboardControlsOptions {
    /** Number of available options (1-indexed keys will be clipped to this) */
    optionCount: number;
    /** Called when a digit key 1–N is pressed (0-indexed value) */
    onSelectOption: (index: number) => void;
    /** Called when Enter or Space pressed and exercise is NOT yet answered */
    onSubmit: () => void;
    /** Called when Enter or Space pressed and exercise IS answered */
    onContinue: () => void;
    /** Whether the result has been shown (switches Enter/Space to "continue" mode) */
    isAnswered: boolean;
    /** Disable all keyboard handling (e.g. submitting, loading) */
    disabled?: boolean;
    /** Whether focus is inside a text input — suppresses Space/Enter so typing still works */
    inputFocused?: boolean;
}

/**
 * Attach keyboard shortcuts to a choice-based exercise:
 *  - Digit 1–N  → select option at that index
 *  - Enter / Space → submit (if not answered) OR continue (if answered)
 *
 * Cleanup is automatic on unmount.
 */
export function useKeyboardControls({
    optionCount,
    onSelectOption,
    onSubmit,
    onContinue,
    isAnswered,
    disabled = false,
    inputFocused = false,
}: KeyboardControlsOptions) {
    useEffect(() => {
        function handleKeyDown(e: KeyboardEvent) {
            // Never intercept when a real input element has focus (except Enter on submit button)
            const tag = (e.target as HTMLElement)?.tagName;
            const isInInput = tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";

            if (disabled) return;

            const digit = parseInt(e.key, 10);
            if (!isNaN(digit) && digit >= 1 && digit <= optionCount && !isInInput) {
                e.preventDefault();
                if (!isAnswered) {
                    onSelectOption(digit - 1);
                }
                return;
            }

            if ((e.key === "Enter" || e.key === " ") && !isInInput && !inputFocused) {
                e.preventDefault();
                if (isAnswered) {
                    onContinue();
                } else {
                    onSubmit();
                }
            }

            // When inputFocused, still allow Enter to trigger "Continue" after answer is shown
            if (e.key === "Enter" && inputFocused && isAnswered) {
                e.preventDefault();
                onContinue();
            }
        }

        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, [optionCount, onSelectOption, onSubmit, onContinue, isAnswered, disabled, inputFocused]);
}
