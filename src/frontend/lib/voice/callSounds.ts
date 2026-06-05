/**
 * Telephone call sound effects synthesized with the Web Audio API.
 *
 * No binary assets needed: the classic RU/EU ringback (425 Hz, 1s on / 4s off)
 * and the hangup/busy beeps are simple sine tones. Synthesis keeps the bundle
 * small and avoids licensing concerns around sound libraries.
 */

let audioContext: AudioContext | null = null;
let ringingGain: GainNode | null = null;
let ringingOscillator: OscillatorNode | null = null;
let ringingInterval: ReturnType<typeof setInterval> | null = null;

function getAudioContext(): AudioContext | null {
    if (typeof window === "undefined") return null;
    if (!audioContext) {
        const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
        if (!Ctor) return null;
        audioContext = new Ctor();
    }
    if (audioContext.state === "suspended") {
        void audioContext.resume();
    }
    return audioContext;
}

const RINGBACK_FREQUENCY_HZ = 425;
const RINGBACK_ON_MS = 1000;
const RINGBACK_PERIOD_MS = 4000;
const TONE_VOLUME = 0.08;

/** Start the looping ringback tone (1s beep every 4s). Safe to call twice. */
export function startRingingTone(): void {
    const ctx = getAudioContext();
    if (!ctx || ringingOscillator) return;

    const oscillator = ctx.createOscillator();
    const gain = ctx.createGain();
    oscillator.type = "sine";
    oscillator.frequency.value = RINGBACK_FREQUENCY_HZ;
    gain.gain.value = 0;
    oscillator.connect(gain);
    gain.connect(ctx.destination);
    oscillator.start();

    const beepOnce = () => {
        const now = ctx.currentTime;
        gain.gain.cancelScheduledValues(now);
        gain.gain.setValueAtTime(0, now);
        gain.gain.linearRampToValueAtTime(TONE_VOLUME, now + 0.02);
        gain.gain.setValueAtTime(TONE_VOLUME, now + RINGBACK_ON_MS / 1000 - 0.02);
        gain.gain.linearRampToValueAtTime(0, now + RINGBACK_ON_MS / 1000);
    };

    beepOnce();
    ringingInterval = setInterval(beepOnce, RINGBACK_PERIOD_MS);
    ringingOscillator = oscillator;
    ringingGain = gain;
}

/** Stop the ringback tone immediately. */
export function stopRingingTone(): void {
    if (ringingInterval) {
        clearInterval(ringingInterval);
        ringingInterval = null;
    }
    if (ringingGain && audioContext) {
        ringingGain.gain.cancelScheduledValues(audioContext.currentTime);
        ringingGain.gain.setValueAtTime(0, audioContext.currentTime);
    }
    if (ringingOscillator) {
        try {
            ringingOscillator.stop();
        } catch {
            // already stopped
        }
        ringingOscillator.disconnect();
        ringingOscillator = null;
    }
    if (ringingGain) {
        ringingGain.disconnect();
        ringingGain = null;
    }
}

/** Short triple "busy" beep played when the call ends. */
export function playHangupBeep(): void {
    const ctx = getAudioContext();
    if (!ctx) return;

    const oscillator = ctx.createOscillator();
    const gain = ctx.createGain();
    oscillator.type = "sine";
    oscillator.frequency.value = RINGBACK_FREQUENCY_HZ;
    gain.gain.value = 0;
    oscillator.connect(gain);
    gain.connect(ctx.destination);

    const now = ctx.currentTime;
    const beepLengthSeconds = 0.18;
    const gapSeconds = 0.18;
    for (let i = 0; i < 3; i++) {
        const start = now + i * (beepLengthSeconds + gapSeconds);
        gain.gain.setValueAtTime(0, start);
        gain.gain.linearRampToValueAtTime(TONE_VOLUME, start + 0.015);
        gain.gain.setValueAtTime(TONE_VOLUME, start + beepLengthSeconds - 0.015);
        gain.gain.linearRampToValueAtTime(0, start + beepLengthSeconds);
    }

    oscillator.start(now);
    oscillator.stop(now + 3 * (beepLengthSeconds + gapSeconds));
    oscillator.onended = () => {
        oscillator.disconnect();
        gain.disconnect();
    };
}

/** Short vibration burst on supported (mobile) devices. */
export function vibrateOnConnect(): void {
    if (typeof navigator !== "undefined" && typeof navigator.vibrate === "function") {
        navigator.vibrate(80);
    }
}
