import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { VoiceMicButton } from "@/features/voice/components/voice-mic-button";
import type { VoicePipelineState } from "@/features/voice/hooks/use-voice";

function renderButton(state: VoicePipelineState, overrides: Partial<Parameters<typeof VoiceMicButton>[0]> = {}) {
    const onStart = vi.fn();
    const onStop = vi.fn();
    render(
        <VoiceMicButton
            state={state}
            isAvailable
            onStart={onStart}
            onStop={onStop}
            {...overrides}
        />
    );
    return { onStart, onStop };
}

describe("VoiceMicButton", () => {
    it("renders nothing when voice is unavailable", () => {
        const { container } = render(
            <VoiceMicButton state="idle" isAvailable={false} onStart={vi.fn()} onStop={vi.fn()} />
        );
        expect(container.firstChild).toBeNull();
    });

    it("idle: shows prompt text and calls onStart on click", () => {
        const { onStart, onStop } = renderButton("idle");
        expect(screen.getByText("Нажми для голосового ввода")).toBeTruthy();
        fireEvent.click(screen.getByRole("button"));
        expect(onStart).toHaveBeenCalledOnce();
        expect(onStop).not.toHaveBeenCalled();
    });

    it("listening: active state calls onStop on click", () => {
        const { onStart, onStop } = renderButton("listening");
        expect(screen.getByText("Слушаю...")).toBeTruthy();
        fireEvent.click(screen.getByRole("button"));
        expect(onStop).toHaveBeenCalledOnce();
        expect(onStart).not.toHaveBeenCalled();
    });

    it("processing: button disabled, no callbacks fire", () => {
        const { onStart, onStop } = renderButton("processing");
        const button = screen.getByRole("button") as HTMLButtonElement;
        expect(button.disabled).toBe(true);
        fireEvent.click(button);
        expect(onStart).not.toHaveBeenCalled();
        expect(onStop).not.toHaveBeenCalled();
    });

    it("playing: disabled with AI status text", () => {
        renderButton("playing");
        expect(screen.getByText("ИИ отвечает...")).toBeTruthy();
        expect((screen.getByRole("button") as HTMLButtonElement).disabled).toBe(true);
    });

    it("error: shows error status and restarts on click", () => {
        const { onStart } = renderButton("error");
        expect(screen.getByText("Ошибка")).toBeTruthy();
        fireEvent.click(screen.getByRole("button"));
        expect(onStart).toHaveBeenCalledOnce();
    });
});
