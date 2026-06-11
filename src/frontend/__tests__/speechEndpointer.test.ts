import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

import { SpeechEndpointer } from "@/features/voice/services/speech-endpointer";

const silenceMs = 450;
const interimGraceMs = 250;

describe("SpeechEndpointer", () => {
    let onUtterance: ReturnType<typeof vi.fn>;
    let endpointer: SpeechEndpointer;

    beforeEach(() => {
        vi.useFakeTimers();
        onUtterance = vi.fn();
        endpointer = new SpeechEndpointer({ silenceMs, interimGraceMs, onUtterance });
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it("commits a final result after the silence window", () => {
        endpointer.handleResult("привет", true);

        vi.advanceTimersByTime(silenceMs - 1);
        expect(onUtterance).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("привет");
    });

    it("commits an interim result after the wider silence window without waiting for a final", () => {
        endpointer.handleResult("добрый день", false);

        vi.advanceTimersByTime(silenceMs + interimGraceMs - 1);
        expect(onUtterance).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("добрый день");
    });

    it("restarts the timer while interim results keep updating", () => {
        endpointer.handleResult("я", false);
        vi.advanceTimersByTime(silenceMs);
        endpointer.handleResult("я хочу", false);
        vi.advanceTimersByTime(silenceMs);
        endpointer.handleResult("я хочу купить", false);

        expect(onUtterance).not.toHaveBeenCalled();

        vi.advanceTimersByTime(silenceMs + interimGraceMs);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("я хочу купить");
    });

    it("replaces interim text when the final result for it arrives", () => {
        endpointer.handleResult("сколько это сто", false);
        endpointer.handleResult("сколько это стоит", true);

        vi.advanceTimersByTime(silenceMs);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("сколько это стоит");
    });

    it("accumulates multiple final results into one utterance", () => {
        endpointer.handleResult("здравствуйте", true);
        vi.advanceTimersByTime(silenceMs - 1);
        endpointer.handleResult("меня зовут Иван", true);

        vi.advanceTimersByTime(silenceMs);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("здравствуйте меня зовут Иван");
    });

    it("exposes combined committed and interim text via currentText", () => {
        endpointer.handleResult("здравствуйте", true);
        endpointer.handleResult("меня зовут", false);

        expect(endpointer.currentText).toBe("здравствуйте меня зовут");
    });

    it("does not fire for whitespace-only text", () => {
        endpointer.handleResult("   ", true);

        vi.advanceTimersByTime(silenceMs + interimGraceMs);
        expect(onUtterance).not.toHaveBeenCalled();
    });

    it("reset cancels the pending commit and clears accumulated text", () => {
        endpointer.handleResult("привет", true);
        endpointer.reset();

        vi.advanceTimersByTime(silenceMs + interimGraceMs);
        expect(onUtterance).not.toHaveBeenCalled();
        expect(endpointer.currentText).toBe("");
    });

    it("starts the next utterance from a clean state after committing", () => {
        endpointer.handleResult("первая фраза", true);
        vi.advanceTimersByTime(silenceMs);

        endpointer.handleResult("вторая фраза", true);
        vi.advanceTimersByTime(silenceMs);

        expect(onUtterance).toHaveBeenCalledTimes(2);
        expect(onUtterance).toHaveBeenLastCalledWith("вторая фраза");
    });

    it("uses the default interim grace when none is provided", () => {
        const defaultedCallback = vi.fn();
        const defaulted = new SpeechEndpointer({ silenceMs, onUtterance: defaultedCallback });

        defaulted.handleResult("тест", false);
        vi.advanceTimersByTime(silenceMs + 250 - 1);
        expect(defaultedCallback).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1);
        expect(defaultedCallback).toHaveBeenCalledExactlyOnceWith("тест");
    });
});
