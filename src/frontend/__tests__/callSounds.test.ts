import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

import {
    startRingingTone,
    stopRingingTone,
    playHangupBeep,
    vibrateOnConnect,
} from "@/lib/voice/callSounds";

class FakeGainParam {
    value = 0;
    cancelScheduledValues = vi.fn();
    setValueAtTime = vi.fn();
    linearRampToValueAtTime = vi.fn();
}

class FakeGainNode {
    gain = new FakeGainParam();
    connect = vi.fn();
    disconnect = vi.fn();
}

class FakeOscillatorNode {
    type = "sine";
    frequency = { value: 0 };
    onended: (() => void) | null = null;
    connect = vi.fn();
    disconnect = vi.fn();
    start = vi.fn();
    stop = vi.fn();
}

class FakeAudioContext {
    state = "running";
    currentTime = 0;
    destination = {};
    createdOscillators: FakeOscillatorNode[] = [];
    createOscillator() {
        const node = new FakeOscillatorNode();
        this.createdOscillators.push(node);
        return node;
    }
    createGain() {
        return new FakeGainNode();
    }
    resume = vi.fn();
}

// The module caches its AudioContext across calls, so all tests share one fake.
const fakeContext = new FakeAudioContext();
vi.stubGlobal(
    "AudioContext",
    vi.fn(() => fakeContext)
);

describe("callSounds", () => {
    beforeEach(() => {
        vi.useFakeTimers();
        fakeContext.createdOscillators = [];
    });

    afterEach(() => {
        stopRingingTone();
        vi.useRealTimers();
    });

    it("startRingingTone creates and starts an oscillator", () => {
        startRingingTone();
        expect(fakeContext.createdOscillators).toHaveLength(1);
        expect(fakeContext.createdOscillators[0].start).toHaveBeenCalled();
        expect(fakeContext.createdOscillators[0].frequency.value).toBe(425);
    });

    it("startRingingTone is idempotent while ringing", () => {
        startRingingTone();
        startRingingTone();
        expect(fakeContext.createdOscillators).toHaveLength(1);
    });

    it("stopRingingTone stops and disconnects the oscillator", () => {
        startRingingTone();
        const oscillator = fakeContext.createdOscillators[0];
        stopRingingTone();
        expect(oscillator.stop).toHaveBeenCalled();
        expect(oscillator.disconnect).toHaveBeenCalled();
    });

    it("ringing can be restarted after stop", () => {
        startRingingTone();
        stopRingingTone();
        startRingingTone();
        expect(fakeContext.createdOscillators).toHaveLength(2);
    });

    it("playHangupBeep schedules a finite tone", () => {
        playHangupBeep();
        const oscillator = fakeContext.createdOscillators[0];
        expect(oscillator.start).toHaveBeenCalled();
        expect(oscillator.stop).toHaveBeenCalled();
    });

    it("vibrateOnConnect calls navigator.vibrate when available", () => {
        const vibrate = vi.fn();
        vi.stubGlobal("navigator", { vibrate });
        vibrateOnConnect();
        expect(vibrate).toHaveBeenCalledWith(80);
    });

    it("vibrateOnConnect is a no-op without vibration support", () => {
        vi.stubGlobal("navigator", {});
        expect(() => vibrateOnConnect()).not.toThrow();
    });
});
