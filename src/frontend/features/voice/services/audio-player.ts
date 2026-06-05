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
    private playerOptions: AudioPlayerOptions;
    private audioBufferQueue: AudioBuffer[] = [];
    private isPlayingFromQueue = false;
    private hasQueueEnded = false;

    constructor(playerOptions: AudioPlayerOptions = {}) {
        this.playerOptions = playerOptions;
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
            this.playerOptions.onError?.(error instanceof Error ? error : new Error("Playback failed"));
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
            this.playerOptions.onError?.(error instanceof Error ? error : new Error("Playback failed"));
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
                this.playerOptions.onPlaybackEnd?.();
            };

            this.sourceNode.start();
            this.setState("playing");
        } catch (error) {
            this.setState("error");
            this.playerOptions.onError?.(error instanceof Error ? error : new Error("Playback failed"));
        }
    }

    beginQueue(): void {
        this.stop();
        this.audioBufferQueue = [];
        this.isPlayingFromQueue = false;
        this.hasQueueEnded = false;
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
            this.audioBufferQueue.push(buffer);
            if (!this.isPlayingFromQueue) {
                this.playNextFromQueue();
            }
        } catch (error) {
            this.playerOptions.onError?.(error instanceof Error ? error : new Error("Decode failed"));
        }
    }

    markQueueComplete(): void {
        this.hasQueueEnded = true;
        if (!this.isPlayingFromQueue && this.audioBufferQueue.length === 0) {
            this.setState("ended");
            this.playerOptions.onPlaybackEnd?.();
        }
    }

    private playNextFromQueue(): void {
        const next = this.audioBufferQueue.shift();
        if (!next) {
            this.isPlayingFromQueue = false;
            if (this.hasQueueEnded) {
                this.setState("ended");
                this.playerOptions.onPlaybackEnd?.();
            }
            return;
        }

        this.isPlayingFromQueue = true;
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
            }
            this.sourceNode.disconnect();
            this.sourceNode = null;
        }
        this.audioBufferQueue = [];
        this.isPlayingFromQueue = false;
        this.hasQueueEnded = false;
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
        this.playerOptions.onStateChange?.(state);
    }

    destroy(): void {
        this.stop();
        if (this.audioContext) {
            this.audioContext.close();
            this.audioContext = null;
        }
    }
}
