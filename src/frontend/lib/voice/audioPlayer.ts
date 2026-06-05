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
    private queue: AudioBuffer[] = [];
    private playingFromQueue = false;
    private queueEnded = false;

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

            while (true) {
                const { done, value } = await reader.read();
                if (done) break;
                chunks.push(value);
            }

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

    beginQueue(): void {
        this.stop();
        this.queue = [];
        this.playingFromQueue = false;
        this.queueEnded = false;
        this.setState("loading");
    }

    async enqueue(arrayBuffer: ArrayBuffer): Promise<void> {
        if (arrayBuffer.byteLength === 0) return;
        try {
            const audioContext = this.getAudioContext();
            if (audioContext.state === "suspended") {
                await audioContext.resume();
            }
            const buffer = await audioContext.decodeAudioData(arrayBuffer.slice(0));
            this.queue.push(buffer);
            if (!this.playingFromQueue) {
                this.playNextFromQueue();
            }
        } catch (error) {
            this.options.onError?.(error instanceof Error ? error : new Error("Decode failed"));
        }
    }

    markQueueComplete(): void {
        this.queueEnded = true;
        if (!this.playingFromQueue && this.queue.length === 0) {
            this.setState("ended");
            this.options.onPlaybackEnd?.();
        }
    }

    private playNextFromQueue(): void {
        const next = this.queue.shift();
        if (!next) {
            this.playingFromQueue = false;
            if (this.queueEnded) {
                this.setState("ended");
                this.options.onPlaybackEnd?.();
            }
            return;
        }

        this.playingFromQueue = true;
        const audioContext = this.getAudioContext();
        const source = audioContext.createBufferSource();
        source.buffer = next;
        source.connect(audioContext.destination);
        source.onended = () => {
            source.disconnect();
            if (this.sourceNode === source) {
                this.sourceNode = null;
            }
            this.playNextFromQueue();
        };
        this.sourceNode = source;
        source.start();
        this.setState("playing");
    }

    stop(): void {
        if (this.sourceNode) {
            try {
                this.sourceNode.stop();
            } catch {
                // noop
            }
            this.sourceNode.disconnect();
            this.sourceNode = null;
        }
        this.queue = [];
        this.playingFromQueue = false;
        this.queueEnded = false;
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
