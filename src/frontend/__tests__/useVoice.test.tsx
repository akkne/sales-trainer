import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";

const post = vi.fn();
vi.mock("@/shared/api/api-client", () => ({
    apiClient: { post: (...args: unknown[]) => post(...args) },
}));

vi.mock("@/features/voice/services/web-speech-client", () => ({
    isWebSpeechSupported: () => true,
    WebSpeechClient: class {
        start = vi.fn(async () => {});
        stop = vi.fn();
        pause = vi.fn();
        resume = vi.fn();
    },
}));

vi.mock("@/features/voice/services/audio-player", () => ({
    AudioPlayer: class {
        beginQueue = vi.fn();
        enqueue = vi.fn(async () => {});
        markQueueComplete = vi.fn();
        stop = vi.fn();
        destroy = vi.fn();
    },
}));

vi.mock("@/features/voice/hooks/use-voice-config", () => ({
    useVoiceConfig: () => ({ data: { enabled: true, vadSilenceMs: 1200 } }),
}));

let streamedFrames: { text: string; audio: ArrayBuffer; isFinal: boolean; isStopSignal: boolean }[] = [];
vi.mock("@/features/voice/services/voice-stream-reader", () => ({
    VoiceStreamReader: class {
        async *read() {
            for (const frame of streamedFrames) yield frame;
        }
    },
}));

let emitUtterance: ((utterance: string) => void) | null = null;
vi.mock("@/features/voice/services/speech-endpointer", () => ({
    SpeechEndpointer: class {
        currentText = "";
        constructor(options: { onUtterance: (utterance: string) => void }) {
            emitUtterance = options.onUtterance;
        }
        handleResult = vi.fn();
        reset = vi.fn();
    },
}));

import { useVoice } from "@/features/voice/hooks/use-voice";

describe("useVoice — session lifecycle", () => {
    beforeEach(() => {
        post.mockReset();
        post.mockResolvedValueOnce({ id: "sess-1" }).mockResolvedValueOnce({ id: "sess-2" });
        emitUtterance = null;
        streamedFrames = [];
    });

    it("creates a session on the first call and reports it as ready", async () => {
        const onSessionReady = vi.fn();
        const { result } = renderHook(() =>
            useVoice({ sessionId: null, modeVoiceEnabled: true, bundleId: "b1", modeId: "m1", onSessionReady })
        );

        await act(async () => {
            await result.current.startVoice();
        });

        expect(post).toHaveBeenCalledWith("/dialog/sessions", { bundleId: "b1", modeId: "m1" });
        expect(onSessionReady).toHaveBeenCalledWith("sess-1");
    });

    it("reports a session handed in from outside as ready without creating a new one", async () => {
        const onSessionReady = vi.fn();
        const { result } = renderHook(() =>
            useVoice({ sessionId: "pre-started", modeVoiceEnabled: true, bundleId: "b1", modeId: "m1", onSessionReady })
        );

        await act(async () => {
            await result.current.startVoice();
        });

        expect(post).not.toHaveBeenCalled();
        expect(onSessionReady).toHaveBeenCalledWith("pre-started");
    });

    it("creates a fresh session for the next call after a hang-up", async () => {
        // Regression: the completed session used to be reused, the backend rejected it, and the
        // caller never got a "session ready" — the call hung on «Соединение…» and, once ended,
        // on «Готовим разбор…» forever.
        const onSessionReady = vi.fn();
        const { result } = renderHook(() =>
            useVoice({ sessionId: null, modeVoiceEnabled: true, bundleId: "b1", modeId: "m1", onSessionReady })
        );

        await act(async () => {
            await result.current.startVoice();
        });
        act(() => {
            result.current.stopVoice();
        });
        await act(async () => {
            await result.current.startVoice();
        });

        expect(post).toHaveBeenCalledTimes(2);
        expect(onSessionReady).toHaveBeenLastCalledWith("sess-2");
    });

    it("reports a turn that came back with nothing instead of leaving the persona silent", async () => {
        // Regression: an empty stream (backend refused the session) looked exactly like a broken
        // microphone — the persona simply never answered and nothing was shown.
        const onError = vi.fn();
        vi.stubGlobal("fetch", vi.fn(async () => ({ ok: true, status: 200 })));
        const { result } = renderHook(() =>
            useVoice({ sessionId: null, modeVoiceEnabled: true, bundleId: "b1", modeId: "m1", onError })
        );

        await act(async () => {
            await result.current.startVoice();
        });
        await act(async () => {
            emitUtterance?.("Здравствуйте");
        });

        expect(onError).toHaveBeenCalledWith(
            expect.objectContaining({ message: "Собеседник не ответил. Попробуйте сказать ещё раз" })
        );
        vi.unstubAllGlobals();
    });

    it("explains a turn refused because the call is already over (409)", async () => {
        const onError = vi.fn();
        vi.stubGlobal("fetch", vi.fn(async () => ({ ok: false, status: 409 })));
        const { result } = renderHook(() =>
            useVoice({ sessionId: null, modeVoiceEnabled: true, bundleId: "b1", modeId: "m1", onError })
        );

        await act(async () => {
            await result.current.startVoice();
        });
        await act(async () => {
            emitUtterance?.("Здравствуйте");
        });

        expect(onError).toHaveBeenCalledWith(
            expect.objectContaining({ message: "Этот звонок уже завершён. Начните новый" })
        );
        vi.unstubAllGlobals();
    });

    it("creates a fresh session for the next call after the persona ended the previous one", async () => {
        const onSessionReady = vi.fn();
        const { result } = renderHook(() =>
            useVoice({ sessionId: null, modeVoiceEnabled: true, bundleId: "b1", modeId: "m1", onSessionReady })
        );

        await act(async () => {
            await result.current.startVoice();
        });
        act(() => {
            result.current.endSession();
        });
        await act(async () => {
            await result.current.startVoice();
        });

        expect(post).toHaveBeenCalledTimes(2);
        expect(onSessionReady).toHaveBeenLastCalledWith("sess-2");
    });
});
