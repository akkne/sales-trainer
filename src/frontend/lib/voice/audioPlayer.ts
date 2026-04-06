export type AudioPlayerState = "idle" | "loading" | "playing" | "ended" | "error";

export interface AudioPlayerOptions {
    onStateChange?: (state: AudioPlayerState) => void;
    onPlaybackEnd?: () => void;
    onError?: (error: Error) => void;
}

export class AudioPlayer {
    private audioContext: AudioContext | null = null;
    private sourceNode: AudioBufferSourceNode | null = null;
    private state: AudioPlayerState = "idle";
    private options: AudioPlayerOptions;

    constructor(options: AudioPlayerOptions = {}) {
        this.options = options;
    }

    private getAudioContext(): AudioContext {
        if (!this.audioContext) {
            this.audioContext = new AudioContext();
        }
        return this.audioContext;
    }

    async playStream(audioStream: ReadableStream<Uint8Array>): Promise<void> {
        this.stop();
        this.setState("loading");

        try {
            const reader = audioStream.getReader();
            const chunks: Uint8Array[] = [];

            // Read all chunks
            while (true) {
                const { done, value } = await reader.read();
                if (done) break;
                chunks.push(value);
            }

            // Combine chunks into single buffer
            const totalLength = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
            const combined = new Uint8Array(totalLength);
            let offset = 0;
            for (const chunk of chunks) {
                combined.set(chunk, offset);
                offset += chunk.length;
            }

            await this.playBuffer(combined.buffer);
        } catch (error) {
            this.setState("error");
            this.options.onError?.(error instanceof Error ? error : new Error("Playback failed"));
        }
    }

    async playBlob(blob: Blob): Promise<void> {
        this.stop();
        this.setState("loading");

        try {
            const arrayBuffer = await blob.arrayBuffer();
            await this.playBuffer(arrayBuffer);
        } catch (error) {
            this.setState("error");
            this.options.onError?.(error instanceof Error ? error : new Error("Playback failed"));
        }
    }

    async playBuffer(arrayBuffer: ArrayBuffer): Promise<void> {
        try {
            const audioContext = this.getAudioContext();

            // Resume context if suspended (browser autoplay policy)
            if (audioContext.state === "suspended") {
                await audioContext.resume();
            }

            const audioBuffer = await audioContext.decodeAudioData(arrayBuffer);

            this.sourceNode = audioContext.createBufferSource();
            this.sourceNode.buffer = audioBuffer;
            this.sourceNode.connect(audioContext.destination);

            this.sourceNode.onended = () => {
                this.setState("ended");
                this.options.onPlaybackEnd?.();
            };

            this.sourceNode.start();
            this.setState("playing");
        } catch (error) {
            this.setState("error");
            this.options.onError?.(error instanceof Error ? error : new Error("Playback failed"));
        }
    }

    stop(): void {
        if (this.sourceNode) {
            try {
                this.sourceNode.stop();
            } catch {
                // Already stopped
            }
            this.sourceNode.disconnect();
            this.sourceNode = null;
        }
        this.setState("idle");
    }

    getState(): AudioPlayerState {
        return this.state;
    }

    isPlaying(): boolean {
        return this.state === "playing";
    }

    private setState(state: AudioPlayerState): void {
        this.state = state;
        this.options.onStateChange?.(state);
    }

    destroy(): void {
        this.stop();
        if (this.audioContext) {
            this.audioContext.close();
            this.audioContext = null;
        }
    }
}
