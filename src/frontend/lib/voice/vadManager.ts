import { MicVAD, RealTimeVADOptions } from "@ricky0123/vad-web";

export type VadState = "idle" | "listening" | "speaking";

export interface VadManagerOptions {
    onSpeechStart?: () => void;
    onSpeechEnd?: (audio: Float32Array) => void;
    onVADMisfire?: () => void;
    silenceMs?: number;
}

export class VadManager {
    private vad: MicVAD | null = null;
    private state: VadState = "idle";
    private options: VadManagerOptions;

    constructor(options: VadManagerOptions = {}) {
        this.options = options;
    }

    async start(): Promise<void> {
        if (this.vad) return;

        const vadOptions: Partial<RealTimeVADOptions> = {
            onSpeechStart: () => {
                this.state = "speaking";
                this.options.onSpeechStart?.();
            },
            onSpeechEnd: (audio: Float32Array) => {
                this.state = "listening";
                this.options.onSpeechEnd?.(audio);
            },
            onVADMisfire: () => {
                this.options.onVADMisfire?.();
            },
            positiveSpeechThreshold: 0.8,
            negativeSpeechThreshold: 0.65,
        };

        this.vad = await MicVAD.new(vadOptions);
        this.vad.start();
        this.state = "listening";
    }

    pause(): void {
        if (this.vad) {
            this.vad.pause();
            this.state = "idle";
        }
    }

    resume(): void {
        if (this.vad) {
            this.vad.start();
            this.state = "listening";
        }
    }

    async stop(): Promise<void> {
        if (this.vad) {
            this.vad.pause();
            this.vad.destroy();
            this.vad = null;
            this.state = "idle";
        }
    }

    getState(): VadState {
        return this.state;
    }

    isActive(): boolean {
        return this.vad !== null && this.state !== "idle";
    }
}

export function floatTo16BitPCM(float32Array: Float32Array): Int16Array {
    const int16Array = new Int16Array(float32Array.length);
    for (let i = 0; i < float32Array.length; i++) {
        const s = Math.max(-1, Math.min(1, float32Array[i]));
        int16Array[i] = s < 0 ? s * 0x8000 : s * 0x7fff;
    }
    return int16Array;
}
