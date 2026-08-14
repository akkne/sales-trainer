/**
 * Tactile call feedback. Calls are silent by design — no ringback or busy tones —
 * so the only "the line changed state" cue left is a short vibration on connect.
 */
export class CallHaptics {
    private static readonly ConnectVibrationMs = 80;

    vibrateOnConnect(): void {
        if (typeof navigator !== "undefined" && typeof navigator.vibrate === "function") {
            navigator.vibrate(CallHaptics.ConnectVibrationMs);
        }
    }
}
