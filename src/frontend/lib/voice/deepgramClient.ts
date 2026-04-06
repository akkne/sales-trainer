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
    private ws: WebSocket | null = null;
    private config: DeepgramConfig;
    private options: DeepgramClientOptions;
    private state: DeepgramState = "disconnected";
    private reconnectAttempts = 0;
    private maxReconnectAttempts = 3;
    private keepAliveInterval: NodeJS.Timeout | null = null;

    constructor(config: DeepgramConfig, options: DeepgramClientOptions) {
        this.config = config;
        this.options = options;
    }

    async connect(): Promise<void> {
        if (this.ws?.readyState === WebSocket.OPEN) {
            return;
        }

        this.setState("connecting");

        const params = new URLSearchParams({
            model: this.config.model || "nova-3",
            language: this.config.language || "ru",
            smart_format: String(this.config.smartFormat ?? true),
            punctuate: String(this.config.punctuate ?? true),
            encoding: "linear16",
            sample_rate: "16000",
            channels: "1",
            interim_results: "true",
            endpointing: "600",
        });

        const url = `wss://api.deepgram.com/v1/listen?${params.toString()}`;

        this.ws = new WebSocket(url, ["token", this.config.apiKey]);

        this.ws.onopen = () => {
            this.setState("connected");
            this.reconnectAttempts = 0;
            this.startKeepAlive();
        };

        this.ws.onmessage = (event) => {
            try {
                const response = JSON.parse(event.data);
                if (response.type === "Results" && response.channel?.alternatives?.[0]) {
                    const transcript = response.channel.alternatives[0].transcript;
                    const isFinal = response.is_final;
                    if (transcript) {
                        this.options.onTranscript(transcript, isFinal);
                    }
                }
            } catch (error) {
                console.error("Deepgram message parse error:", error);
            }
        };

        this.ws.onerror = (event) => {
            console.error("Deepgram WebSocket error:", event);
            this.setState("error");
            this.options.onError?.(new Error("Deepgram connection error"));
        };

        this.ws.onclose = () => {
            this.stopKeepAlive();
            if (this.state === "connected") {
                this.setState("disconnected");
                this.attemptReconnect();
            }
        };
    }

    sendAudio(audio: Int16Array): void {
        if (this.ws?.readyState === WebSocket.OPEN) {
            this.ws.send(audio.buffer);
        }
    }

    disconnect(): void {
        this.stopKeepAlive();
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }
        this.setState("disconnected");
        this.reconnectAttempts = 0;
    }

    getState(): DeepgramState {
        return this.state;
    }

    private setState(state: DeepgramState): void {
        this.state = state;
        this.options.onStateChange?.(state);
    }

    private startKeepAlive(): void {
        this.keepAliveInterval = setInterval(() => {
            if (this.ws?.readyState === WebSocket.OPEN) {
                this.ws.send(JSON.stringify({ type: "KeepAlive" }));
            }
        }, 10000);
    }

    private stopKeepAlive(): void {
        if (this.keepAliveInterval) {
            clearInterval(this.keepAliveInterval);
            this.keepAliveInterval = null;
        }
    }

    private attemptReconnect(): void {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            setTimeout(() => this.connect(), 1000 * this.reconnectAttempts);
        } else {
            this.options.onError?.(new Error("Max reconnection attempts reached"));
        }
    }
}
