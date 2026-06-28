import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { AiDialogueExercise } from "@/features/exercise/components/ai-dialogue-exercise";

// The voice pipeline pulls in WebSpeech/AudioContext/react-query; stub it so the
// component test stays focused on the dialog flow.
const voiceState = {
    state: "idle" as const,
    currentTranscript: "",
    isVoiceAvailable: true,
    startVoice: vi.fn(),
    stopVoice: vi.fn(),
};
vi.mock("@/features/exercise/hooks/use-exercise-voice", () => ({
    useExerciseVoice: () => voiceState,
}));

const postMock = vi.fn();
vi.mock("@/shared/api/api-client", () => ({
    apiClient: { post: (...args: unknown[]) => postMock(...args) },
}));

const CONTENT = {
    persona: "Skeptical Sam",
    scenario: "Cold call to IT director",
    max_turns: 4,
};

describe("AiDialogueExercise", () => {
    beforeEach(() => {
        postMock.mockReset();
        voiceState.isVoiceAvailable = true;
    });

    it("shows the text/voice mode choice and does NOT auto-start a conversation", () => {
        render(
            <AiDialogueExercise
                content={CONTENT}
                exerciseId="ex-1"
                onSubmit={vi.fn()}
                isSubmitting={false}
                submittedResult={null}
            />
        );
        expect(screen.getByText("Text")).toBeTruthy();
        expect(screen.getByText("Voice")).toBeTruthy();
        // User speaks first — no greeting request fired on mount.
        expect(postMock).not.toHaveBeenCalled();
    });

    it("text mode shows the input so the user writes first", () => {
        render(
            <AiDialogueExercise
                content={CONTENT}
                exerciseId="ex-1"
                onSubmit={vi.fn()}
                isSubmitting={false}
                submittedResult={null}
            />
        );
        fireEvent.click(screen.getByText("Text"));
        expect(screen.getByPlaceholderText("Your line…")).toBeTruthy();
        expect(screen.getByText("Write your first line")).toBeTruthy();
    });

    it("sends the user's first message to the chat endpoint", async () => {
        postMock.mockResolvedValue({ response: "Sure, I'm listening.", isComplete: false, isFinished: false });
        render(
            <AiDialogueExercise
                content={CONTENT}
                exerciseId="ex-1"
                onSubmit={vi.fn()}
                isSubmitting={false}
                submittedResult={null}
            />
        );
        fireEvent.click(screen.getByText("Text"));
        const input = screen.getByPlaceholderText("Your line…") as HTMLInputElement;
        fireEvent.change(input, { target: { value: "Hi, this is Max from FinTech Pro" } });
        fireEvent.keyDown(input, { key: "Enter" });

        expect(postMock).toHaveBeenCalledWith(
            "/exercises/ex-1/chat",
            { message: "Hi, this is Max from FinTech Pro" }
        );
    });

    it("disables the voice option when voice is unavailable", () => {
        voiceState.isVoiceAvailable = false;
        render(
            <AiDialogueExercise
                content={CONTENT}
                exerciseId="ex-1"
                onSubmit={vi.fn()}
                isSubmitting={false}
                submittedResult={null}
            />
        );
        const voiceButton = screen.getByText("Voice").closest("button") as HTMLButtonElement;
        expect(voiceButton.disabled).toBe(true);
    });
});
