export class CallSoundsPlayer {
    private static readonly RingbackFrequencyHz = 425;
    private static readonly RingbackOnMs = 1000;
    private static readonly RingbackPeriodMs = 4000;
    private static readonly ToneVolume = 0.08;

    private audioContext: AudioContext | null = null;
    private ringingGain: GainNode | null = null;
    private ringingOscillator: OscillatorNode | null = null;
    private ringingInterval: ReturnType<typeof setInterval> | null = null;

    private resolveAudioContext(): AudioContext | null {
        if (typeof window === "undefined") return null;
        if (!this.audioContext) {
            const AudioContextClass = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
            if (!AudioContextClass) return null;
            this.audioContext = new AudioContextClass();
        }
        if (this.audioContext.state === "suspended") {
            void this.audioContext.resume();
        }
        return this.audioContext;
    }

    startRinging(): void {
        const context = this.resolveAudioContext();
        if (!context || this.ringingOscillator) return;

        const oscillator = context.createOscillator();
        const gain = context.createGain();
        oscillator.type = "sine";
        oscillator.frequency.value = CallSoundsPlayer.RingbackFrequencyHz;
        gain.gain.value = 0;
        oscillator.connect(gain);
        gain.connect(context.destination);
        oscillator.start();

        const beepOnce = () => {
            const now = context.currentTime;
            gain.gain.cancelScheduledValues(now);
            gain.gain.setValueAtTime(0, now);
            gain.gain.linearRampToValueAtTime(CallSoundsPlayer.ToneVolume, now + 0.02);
            gain.gain.setValueAtTime(CallSoundsPlayer.ToneVolume, now + CallSoundsPlayer.RingbackOnMs / 1000 - 0.02);
            gain.gain.linearRampToValueAtTime(0, now + CallSoundsPlayer.RingbackOnMs / 1000);
        };

        beepOnce();
        this.ringingInterval = setInterval(beepOnce, CallSoundsPlayer.RingbackPeriodMs);
        this.ringingOscillator = oscillator;
        this.ringingGain = gain;
    }

    stopRinging(): void {
        if (this.ringingInterval) {
            clearInterval(this.ringingInterval);
            this.ringingInterval = null;
        }
        if (this.ringingGain && this.audioContext) {
            this.ringingGain.gain.cancelScheduledValues(this.audioContext.currentTime);
            this.ringingGain.gain.setValueAtTime(0, this.audioContext.currentTime);
        }
        if (this.ringingOscillator) {
            try {
                this.ringingOscillator.stop();
            } catch {
            }
            this.ringingOscillator.disconnect();
            this.ringingOscillator = null;
        }
        if (this.ringingGain) {
            this.ringingGain.disconnect();
            this.ringingGain = null;
        }
    }

    playHangupBeep(): void {
        const context = this.resolveAudioContext();
        if (!context) return;

        const oscillator = context.createOscillator();
        const gain = context.createGain();
        oscillator.type = "sine";
        oscillator.frequency.value = CallSoundsPlayer.RingbackFrequencyHz;
        gain.gain.value = 0;
        oscillator.connect(gain);
        gain.connect(context.destination);

        const now = context.currentTime;
        const beepLengthSeconds = 0.18;
        const gapSeconds = 0.18;
        for (let i = 0; i < 3; i++) {
            const start = now + i * (beepLengthSeconds + gapSeconds);
            gain.gain.setValueAtTime(0, start);
            gain.gain.linearRampToValueAtTime(CallSoundsPlayer.ToneVolume, start + 0.015);
            gain.gain.setValueAtTime(CallSoundsPlayer.ToneVolume, start + beepLengthSeconds - 0.015);
            gain.gain.linearRampToValueAtTime(0, start + beepLengthSeconds);
        }

        oscillator.start(now);
        oscillator.stop(now + 3 * (beepLengthSeconds + gapSeconds));
        oscillator.onended = () => {
            oscillator.disconnect();
            gain.disconnect();
        };
    }

    vibrateOnConnect(): void {
        if (typeof navigator !== "undefined" && typeof navigator.vibrate === "function") {
            navigator.vibrate(80);
        }
    }
}
