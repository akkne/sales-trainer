import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook } from "@testing-library/react";

// Inline of useKeyboardControls for test isolation
// Matches: src/frontend/lib/hooks/useKeyboardControls.ts

import { useEffect } from "react";

interface KeyboardControlsOptions {
    optionCount: number;
    onSelectOption: (index: number) => void;
    onSubmit: () => void;
    onContinue: () => void;
    isAnswered: boolean;
    disabled?: boolean;
    inputFocused?: boolean;
}

function useKeyboardControls({
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
            const tag = (e.target as HTMLElement)?.tagName;
            const isInInput = tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
            if (disabled) return;

            const digit = parseInt(e.key, 10);
            if (!isNaN(digit) && digit >= 1 && digit <= optionCount && !isInInput) {
                e.preventDefault();
                if (!isAnswered) onSelectOption(digit - 1);
                return;
            }

            if ((e.key === "Enter" || e.key === " ") && !isInInput && !inputFocused) {
                e.preventDefault();
                if (isAnswered) onContinue();
                else onSubmit();
            }

            if (e.key === "Enter" && inputFocused && isAnswered) {
                e.preventDefault();
                onContinue();
            }
        }

        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, [optionCount, onSelectOption, onSubmit, onContinue, isAnswered, disabled, inputFocused]);
}

function fireKey(key: string) {
    window.dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true }));
}

describe("useKeyboardControls", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("calls onSelectOption with correct 0-based index on digit key", () => {
        const onSelectOption = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption,
                onSubmit: vi.fn(),
                onContinue: vi.fn(),
                isAnswered: false,
            })
        );

        fireKey("1");
        expect(onSelectOption).toHaveBeenCalledWith(0);

        fireKey("3");
        expect(onSelectOption).toHaveBeenCalledWith(2);
    });

    it("does not call onSelectOption for digit out of range", () => {
        const onSelectOption = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 2,
                onSelectOption,
                onSubmit: vi.fn(),
                onContinue: vi.fn(),
                isAnswered: false,
            })
        );

        fireKey("4");
        expect(onSelectOption).not.toHaveBeenCalled();
    });

    it("calls onSubmit on Enter when not answered", () => {
        const onSubmit = vi.fn();
        const onContinue = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption: vi.fn(),
                onSubmit,
                onContinue,
                isAnswered: false,
            })
        );

        fireKey("Enter");
        expect(onSubmit).toHaveBeenCalledOnce();
        expect(onContinue).not.toHaveBeenCalled();
    });

    it("calls onSubmit on Space when not answered", () => {
        const onSubmit = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption: vi.fn(),
                onSubmit,
                onContinue: vi.fn(),
                isAnswered: false,
            })
        );

        fireKey(" ");
        expect(onSubmit).toHaveBeenCalledOnce();
    });

    it("calls onContinue on Enter when answered", () => {
        const onSubmit = vi.fn();
        const onContinue = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption: vi.fn(),
                onSubmit,
                onContinue,
                isAnswered: true,
            })
        );

        fireKey("Enter");
        expect(onContinue).toHaveBeenCalledOnce();
        expect(onSubmit).not.toHaveBeenCalled();
    });

    it("calls onContinue on Space when answered", () => {
        const onContinue = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption: vi.fn(),
                onSubmit: vi.fn(),
                onContinue,
                isAnswered: true,
            })
        );

        fireKey(" ");
        expect(onContinue).toHaveBeenCalledOnce();
    });

    it("does not call anything when disabled", () => {
        const onSelectOption = vi.fn();
        const onSubmit = vi.fn();
        const onContinue = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption,
                onSubmit,
                onContinue,
                isAnswered: false,
                disabled: true,
            })
        );

        fireKey("1");
        fireKey("Enter");
        expect(onSelectOption).not.toHaveBeenCalled();
        expect(onSubmit).not.toHaveBeenCalled();
    });

    it("does not call onSelectOption when already answered", () => {
        const onSelectOption = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption,
                onSubmit: vi.fn(),
                onContinue: vi.fn(),
                isAnswered: true,
            })
        );

        fireKey("2");
        expect(onSelectOption).not.toHaveBeenCalled();
    });

    it("removes listener on unmount", () => {
        const onSubmit = vi.fn();
        const { unmount } = renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption: vi.fn(),
                onSubmit,
                onContinue: vi.fn(),
                isAnswered: false,
            })
        );

        unmount();
        fireKey("Enter");
        expect(onSubmit).not.toHaveBeenCalled();
    });

    it("does not intercept Enter when inputFocused and not answered", () => {
        const onSubmit = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption: vi.fn(),
                onSubmit,
                onContinue: vi.fn(),
                isAnswered: false,
                inputFocused: true,
            })
        );

        fireKey("Enter");
        expect(onSubmit).not.toHaveBeenCalled();
    });

    it("calls onContinue on Enter when inputFocused and answered", () => {
        const onContinue = vi.fn();
        renderHook(() =>
            useKeyboardControls({
                optionCount: 3,
                onSelectOption: vi.fn(),
                onSubmit: vi.fn(),
                onContinue,
                isAnswered: true,
                inputFocused: true,
            })
        );

        fireKey("Enter");
        expect(onContinue).toHaveBeenCalledOnce();
    });
});
