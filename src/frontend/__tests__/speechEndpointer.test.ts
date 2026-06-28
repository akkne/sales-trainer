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
        endpointer.handleResult("hello", true);

        vi.advanceTimersByTime(silenceMs - 1);
        expect(onUtterance).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("hello");
    });

    it("commits an interim result after the wider silence window without waiting for a final", () => {
        endpointer.handleResult("good morning", false);

        vi.advanceTimersByTime(silenceMs + interimGraceMs - 1);
        expect(onUtterance).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("good morning");
    });

    it("restarts the timer while interim results keep updating", () => {
        endpointer.handleResult("I", false);
        vi.advanceTimersByTime(silenceMs);
        endpointer.handleResult("I want", false);
        vi.advanceTimersByTime(silenceMs);
        endpointer.handleResult("I want to buy", false);

        expect(onUtterance).not.toHaveBeenCalled();

        vi.advanceTimersByTime(silenceMs + interimGraceMs);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("I want to buy");
    });

    it("replaces interim text when the final result for it arrives", () => {
        endpointer.handleResult("how much does it co", false);
        endpointer.handleResult("how much does it cost", true);

        vi.advanceTimersByTime(silenceMs);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("how much does it cost");
    });

    it("accumulates multiple final results into one utterance", () => {
        endpointer.handleResult("hello", true);
        vi.advanceTimersByTime(silenceMs - 1);
        endpointer.handleResult("my name is Ivan", true);

        vi.advanceTimersByTime(silenceMs);
        expect(onUtterance).toHaveBeenCalledExactlyOnceWith("hello my name is Ivan");
    });

    it("exposes combined committed and interim text via currentText", () => {
        endpointer.handleResult("hello", true);
        endpointer.handleResult("my name is", false);

        expect(endpointer.currentText).toBe("hello my name is");
    });

    it("does not fire for whitespace-only text", () => {
        endpointer.handleResult("   ", true);

        vi.advanceTimersByTime(silenceMs + interimGraceMs);
        expect(onUtterance).not.toHaveBeenCalled();
    });

    it("reset cancels the pending commit and clears accumulated text", () => {
        endpointer.handleResult("hello", true);
        endpointer.reset();

        vi.advanceTimersByTime(silenceMs + interimGraceMs);
        expect(onUtterance).not.toHaveBeenCalled();
        expect(endpointer.currentText).toBe("");
    });

    it("starts the next utterance from a clean state after committing", () => {
        endpointer.handleResult("first phrase", true);
        vi.advanceTimersByTime(silenceMs);

        endpointer.handleResult("second phrase", true);
        vi.advanceTimersByTime(silenceMs);

        expect(onUtterance).toHaveBeenCalledTimes(2);
        expect(onUtterance).toHaveBeenLastCalledWith("second phrase");
    });

    it("uses the default interim grace when none is provided", () => {
        const defaultedCallback = vi.fn();
        const defaulted = new SpeechEndpointer({ silenceMs, onUtterance: defaultedCallback });

        defaulted.handleResult("test", false);
        vi.advanceTimersByTime(silenceMs + 250 - 1);
        expect(defaultedCallback).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1);
        expect(defaultedCallback).toHaveBeenCalledExactlyOnceWith("test");
    });
});
