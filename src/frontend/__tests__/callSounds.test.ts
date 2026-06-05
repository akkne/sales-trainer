import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

import { CallSoundsPlayer } from "@/features/voice/services/call-sounds-player";

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

const fakeContext = new FakeAudioContext();
vi.stubGlobal(
    "AudioContext",
    vi.fn(() => fakeContext)
);

describe("CallSoundsPlayer", () => {
    let player: CallSoundsPlayer;

    beforeEach(() => {
        vi.useFakeTimers();
        fakeContext.createdOscillators = [];
        player = new CallSoundsPlayer();
    });

    afterEach(() => {
        player.stopRinging();
        vi.useRealTimers();
    });

    it("startRinging creates and starts an oscillator", () => {
        player.startRinging();
        expect(fakeContext.createdOscillators).toHaveLength(1);
        expect(fakeContext.createdOscillators[0].start).toHaveBeenCalled();
        expect(fakeContext.createdOscillators[0].frequency.value).toBe(425);
    });

    it("startRinging is idempotent while ringing", () => {
        player.startRinging();
        player.startRinging();
        expect(fakeContext.createdOscillators).toHaveLength(1);
    });

    it("stopRinging stops and disconnects the oscillator", () => {
        player.startRinging();
        const oscillator = fakeContext.createdOscillators[0];
        player.stopRinging();
        expect(oscillator.stop).toHaveBeenCalled();
        expect(oscillator.disconnect).toHaveBeenCalled();
    });

    it("ringing can be restarted after stop", () => {
        player.startRinging();
        player.stopRinging();
        player.startRinging();
        expect(fakeContext.createdOscillators).toHaveLength(2);
    });

    it("playHangupBeep schedules a finite tone", () => {
        player.playHangupBeep();
        const oscillator = fakeContext.createdOscillators[0];
        expect(oscillator.start).toHaveBeenCalled();
        expect(oscillator.stop).toHaveBeenCalled();
    });

    it("vibrateOnConnect calls navigator.vibrate when available", () => {
        const vibrate = vi.fn();
        vi.stubGlobal("navigator", { vibrate });
        player.vibrateOnConnect();
        expect(vibrate).toHaveBeenCalledWith(80);
    });

    it("vibrateOnConnect is a no-op without vibration support", () => {
        vi.stubGlobal("navigator", {});
        expect(() => player.vibrateOnConnect()).not.toThrow();
    });
});
