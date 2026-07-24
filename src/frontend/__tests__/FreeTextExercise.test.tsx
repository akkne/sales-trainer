import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { FreeTextExercise } from "@/features/exercise/components/free-text-exercise";

// Stub the dictation hook so the component test stays focused on wiring, not the
// Web Speech API. The mock lets us drive a "final transcript" into the field.
const dictation = {
    state: "idle" as const,
    interimText: "",
    isAvailable: true,
    isListening: false,
    start: vi.fn(),
    stop: vi.fn(),
    toggle: vi.fn(),
};
let capturedOnFinal: ((fragment: string) => void) | undefined;
vi.mock("@/features/exercise/hooks/use-speech-dictation", () => ({
    useSpeechDictation: (opts: { onFinalTranscript: (f: string) => void }) => {
        capturedOnFinal = opts.onFinalTranscript;
        return dictation;
    },
}));

const CONTENT = { instruction: "Ответь на возражение клиента" };

describe("FreeTextExercise", () => {
    beforeEach(() => {
        dictation.toggle.mockReset();
        dictation.stop.mockReset();
        dictation.isAvailable = true;
        dictation.interimText = "";
        capturedOnFinal = undefined;
    });

    it("renders the voice button when dictation is available", () => {
        render(<FreeTextExercise content={CONTENT} onSubmit={vi.fn()} isSubmitting={false} submittedResult={null} />);
        expect(screen.getByText("Голос")).toBeTruthy();
    });

    it("wires the voice button to toggle dictation (previously a dead stub)", () => {
        render(<FreeTextExercise content={CONTENT} onSubmit={vi.fn()} isSubmitting={false} submittedResult={null} />);
        fireEvent.click(screen.getByText("Голос").closest("button")!);
        expect(dictation.toggle).toHaveBeenCalledTimes(1);
    });

    it("appends finalized speech fragments into the answer field", () => {
        render(<FreeTextExercise content={CONTENT} onSubmit={vi.fn()} isSubmitting={false} submittedResult={null} />);
        const textarea = screen.getByPlaceholderText("Минимум 20 символов…") as HTMLTextAreaElement;

        act(() => capturedOnFinal!("Понимаю ваше беспокойство"));
        expect(textarea.value).toBe("Понимаю ваше беспокойство");

        act(() => capturedOnFinal!("давайте разберёмся"));
        expect(textarea.value).toBe("Понимаю ваше беспокойство давайте разберёмся");
    });

    it("hides the voice button when dictation is unavailable", () => {
        dictation.isAvailable = false;
        render(<FreeTextExercise content={CONTENT} onSubmit={vi.fn()} isSubmitting={false} submittedResult={null} />);
        expect(screen.queryByText("Голос")).toBeNull();
    });
});
