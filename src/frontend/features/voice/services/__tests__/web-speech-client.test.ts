import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { WebSpeechClient } from "@/features/voice/services/web-speech-client";

// Minimal fake of the browser SpeechRecognition. Each instance tracks whether it
// has been started/stopped and lets the test fire the lifecycle callbacks the way
// a real (mobile) engine would — most importantly, auto-ending after an utterance.
class FakeSpeechRecognition {
    static instances: FakeSpeechRecognition[] = [];

    continuous = false;
    interimResults = false;
    lang = "";
    maxAlternatives = 1;

    onstart: (() => void) | null = null;
    onend: (() => void) | null = null;
    onresult: ((event: unknown) => void) | null = null;
    onerror: ((event: unknown) => void) | null = null;
    onspeechstart: (() => void) | null = null;
    onspeechend: (() => void) | null = null;

    startCount = 0;
    stopped = false;

    constructor() {
        FakeSpeechRecognition.instances.push(this);
    }

    start(): void {
        this.startCount += 1;
        this.stopped = false;
        this.onstart?.();
    }

    stop(): void {
        this.stopped = true;
        this.onend?.();
    }

    abort(): void {
        this.stopped = true;
    }

    addEventListener(): void {}
    removeEventListener(): void {}
    dispatchEvent(): boolean {
        return false;
    }

    /** Simulate the engine ending on its own (mobile behaviour after a phrase). */
    fireAutoEnd(): void {
        this.stopped = true;
        this.onend?.();
    }
}

function liveInstances() {
    return FakeSpeechRecognition.instances.filter((r) => !r.stopped);
}

describe("WebSpeechClient (mobile mic lifecycle)", () => {
    beforeEach(() => {
        FakeSpeechRecognition.instances = [];
        (window as unknown as { SpeechRecognition: unknown }).SpeechRecognition =
            FakeSpeechRecognition as unknown;
        (window as unknown as { webkitSpeechRecognition?: unknown }).webkitSpeechRecognition =
            FakeSpeechRecognition as unknown;
        Object.defineProperty(navigator, "mediaDevices", {
            configurable: true,
            value: { getUserMedia: vi.fn().mockResolvedValue({}) },
        });
    });

    afterEach(() => {
        delete (window as unknown as { SpeechRecognition?: unknown }).SpeechRecognition;
        delete (window as unknown as { webkitSpeechRecognition?: unknown }).webkitSpeechRecognition;
    });

    it("spins up a fresh recognition instance on each resume so the mic returns after a turn", async () => {
        const client = new WebSpeechClient({ onResult: vi.fn() });
        await client.start();

        expect(FakeSpeechRecognition.instances).toHaveLength(1);
        const first = FakeSpeechRecognition.instances[0];
        expect(first.startCount).toBe(1);

        // A turn: pause while the AI speaks, then resume when playback ends.
        client.pause();
        expect(first.stopped).toBe(true);

        client.resume();

        // Reuse of the ended instance is exactly what fails on mobile, so resume
        // must have created a new one that is actively listening.
        expect(FakeSpeechRecognition.instances).toHaveLength(2);
        expect(liveInstances()).toHaveLength(1);
        expect(FakeSpeechRecognition.instances[1].startCount).toBe(1);
    });

    it("relaunches a fresh instance when the engine auto-ends mid-listening", async () => {
        const client = new WebSpeechClient({ onResult: vi.fn() });
        await client.start();

        const first = FakeSpeechRecognition.instances[0];
        first.fireAutoEnd();

        expect(FakeSpeechRecognition.instances).toHaveLength(2);
        expect(liveInstances()).toHaveLength(1);
    });

    it("does not resume after stop()", async () => {
        const client = new WebSpeechClient({ onResult: vi.fn() });
        await client.start();

        client.stop();
        client.resume();

        expect(liveInstances()).toHaveLength(0);
    });

    it("ignores resume() while a recognition is already live", async () => {
        const client = new WebSpeechClient({ onResult: vi.fn() });
        await client.start();

        client.resume();

        expect(FakeSpeechRecognition.instances).toHaveLength(1);
    });
});
