import { TimingConstants } from "@/shared/constants/timing-constants";

export interface DeepgramConfig {
    apiKey: string;
    model?: string;
    language?: string;
    smartFormat?: boolean;
    punctuate?: boolean;
}

export type DeepgramState = "disconnected" | "connecting" | "connected" | "error";

export interface DeepgramClientOptions {
    onTranscript: (transcript: string, isFinal: boolean) => void;
    onError?: (error: Error) => void;
    onStateChange?: (state: DeepgramState) => void;
}

export class DeepgramClient {
    private webSocketConnection: WebSocket | null = null;
    private deepgramConfiguration: DeepgramConfig;
    private clientOptions: DeepgramClientOptions;
    private state: DeepgramState = "disconnected";
    private currentReconnectAttemptCount = 0;
    private maximumReconnectAttemptCount = 3;
    private keepAliveInterval: NodeJS.Timeout | null = null;

    constructor(deepgramConfiguration: DeepgramConfig, clientOptions: DeepgramClientOptions) {
        this.deepgramConfiguration = deepgramConfiguration;
        this.clientOptions = clientOptions;
    }

    async connect(): Promise<void> {
        if (this.webSocketConnection?.readyState === WebSocket.OPEN) {
            return;
        }

        this.setState("connecting");

        const parameters = new URLSearchParams({
            model: this.deepgramConfiguration.model || "nova-3",
            language: this.deepgramConfiguration.language || "ru",
            smart_format: String(this.deepgramConfiguration.smartFormat ?? true),
            punctuate: String(this.deepgramConfiguration.punctuate ?? true),
            encoding: "linear16",
            sample_rate: "16000",
            channels: "1",
            interim_results: "true",
            endpointing: "600",
        });

        const url = `wss://api.deepgram.com/v1/listen?${parameters.toString()}`;

        this.webSocketConnection = new WebSocket(url, ["token", this.deepgramConfiguration.apiKey]);

        this.webSocketConnection.onopen = () => {
            this.setState("connected");
            this.currentReconnectAttemptCount = 0;
            this.startKeepAlive();
        };

        this.webSocketConnection.onmessage = (event) => {
            try {
                const response = JSON.parse(event.data);
                if (response.type === "Results" && response.channel?.alternatives?.[0]) {
                    const transcript = response.channel.alternatives[0].transcript;
                    const isFinal = response.is_final;
                    if (transcript) {
                        this.clientOptions.onTranscript(transcript, isFinal);
                    }
                }
            } catch (error) {
                this.clientOptions.onError?.(error instanceof Error ? error : new Error("Deepgram message parse error"));
            }
        };

        this.webSocketConnection.onerror = () => {
            this.setState("error");
            this.clientOptions.onError?.(new Error("Deepgram connection error"));
        };

        this.webSocketConnection.onclose = () => {
            this.stopKeepAlive();
            if (this.state === "connected") {
                this.setState("disconnected");
                this.attemptReconnect();
            }
        };
    }

    sendAudio(audio: Int16Array): void {
        if (this.webSocketConnection?.readyState === WebSocket.OPEN) {
            this.webSocketConnection.send(audio.buffer.slice(audio.byteOffset, audio.byteOffset + audio.byteLength));
        }
    }

    disconnect(): void {
        this.stopKeepAlive();
        if (this.webSocketConnection) {
            this.webSocketConnection.close();
            this.webSocketConnection = null;
        }
        this.setState("disconnected");
        this.currentReconnectAttemptCount = 0;
    }

    getState(): DeepgramState {
        return this.state;
    }

    private setState(state: DeepgramState): void {
        this.state = state;
        this.clientOptions.onStateChange?.(state);
    }

    private startKeepAlive(): void {
        this.keepAliveInterval = setInterval(() => {
            if (this.webSocketConnection?.readyState === WebSocket.OPEN) {
                this.webSocketConnection.send(JSON.stringify({ type: "KeepAlive" }));
            }
        }, TimingConstants.deepgramConnectionTimeoutMs);
    }

    private stopKeepAlive(): void {
        if (this.keepAliveInterval) {
            clearInterval(this.keepAliveInterval);
            this.keepAliveInterval = null;
        }
    }

    private attemptReconnect(): void {
        if (this.currentReconnectAttemptCount < this.maximumReconnectAttemptCount) {
            this.currentReconnectAttemptCount++;
            setTimeout(() => this.connect(), TimingConstants.deepgramReconnectDelayMs * this.currentReconnectAttemptCount);
        } else {
            this.clientOptions.onError?.(new Error("Max reconnection attempts reached"));
        }
    }
}
