export type WebSpeechState = "idle" | "listening" | "error";

export interface WebSpeechClientOptions {
    language?: string;
    continuous?: boolean;
    interimResults?: boolean;
    onResult: (transcript: string, isFinal: boolean) => void;
    onError?: (error: Error) => void;
    onStateChange?: (state: WebSpeechState) => void;
    onSpeechStart?: () => void;
    onSpeechEnd?: () => void;
}

interface SpeechRecognitionEvent {
    resultIndex: number;
    results: SpeechRecognitionResultList;
}

interface SpeechRecognitionResultList {
    length: number;
    item(index: number): SpeechRecognitionResult;
    [index: number]: SpeechRecognitionResult;
}

interface SpeechRecognitionResult {
    isFinal: boolean;
    length: number;
    item(index: number): SpeechRecognitionAlternative;
    [index: number]: SpeechRecognitionAlternative;
}

interface SpeechRecognitionAlternative {
    transcript: string;
    confidence: number;
}

interface SpeechRecognitionErrorEvent {
    error: string;
    message: string;
}

interface SpeechRecognition extends EventTarget {
    continuous: boolean;
    interimResults: boolean;
    lang: string;
    maxAlternatives: number;
    onresult: ((event: SpeechRecognitionEvent) => void) | null;
    onerror: ((event: SpeechRecognitionErrorEvent) => void) | null;
    onstart: (() => void) | null;
    onend: (() => void) | null;
    onspeechstart: (() => void) | null;
    onspeechend: (() => void) | null;
    start(): void;
    stop(): void;
    abort(): void;
}

declare global {
    interface Window {
        SpeechRecognition?: new () => SpeechRecognition;
        webkitSpeechRecognition?: new () => SpeechRecognition;
    }
}

export function isWebSpeechSupported(): boolean {
    return typeof window !== "undefined" &&
           !!(window.SpeechRecognition || window.webkitSpeechRecognition);
}

export class WebSpeechClient {
    private recognition: SpeechRecognition | null = null;
    private options: WebSpeechClientOptions;
    private state: WebSpeechState = "idle";
    private shouldRestartAfterEnd = false;
    private isRecognitionStarted = false;

    constructor(options: WebSpeechClientOptions) {
        this.options = options;
    }

    async start(): Promise<void> {
        if (!isWebSpeechSupported()) {
            this.setState("error");
            this.options.onError?.(new Error("Web Speech API is not supported in this browser"));
            return;
        }

        try {
            await navigator.mediaDevices.getUserMedia({ audio: true });
        } catch {
            this.setState("error");
            this.options.onError?.(new Error("Microphone access denied"));
            return;
        }

        const recognition = this.createRecognition();
        if (!recognition) {
            this.setState("error");
            this.options.onError?.(new Error("Web Speech API is not supported"));
            return;
        }

        this.recognition = recognition;
        this.shouldRestartAfterEnd = true;

        try {
            this.recognition.start();
        } catch (error) {
            this.setState("error");
            this.options.onError?.(error instanceof Error ? error : new Error("Failed to start recognition"));
        }
    }

    stop(): void {
        this.shouldRestartAfterEnd = false;
        if (this.recognition) {
            this.recognition.onend = null;
            this.recognition.stop();
            this.recognition = null;
        }
        this.isRecognitionStarted = false;
        this.setState("idle");
    }

    pause(): void {
        this.shouldRestartAfterEnd = false;
        if (this.recognition) {
            try {
                this.recognition.stop();
            } catch {
            }
        }
        this.setState("idle");
    }

    resume(): void {
        if (!this.recognition) return;

        this.shouldRestartAfterEnd = true;

        if (this.isRecognitionStarted) return;

        try {
            this.recognition.start();
        } catch {
        }
    }

    getState(): WebSpeechState {
        return this.state;
    }

    private createRecognition(): SpeechRecognition | null {
        const SpeechRecognitionClass = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognitionClass) return null;

        const recognition = new SpeechRecognitionClass();
        recognition.continuous = this.options.continuous ?? true;
        recognition.interimResults = this.options.interimResults ?? true;
        recognition.lang = this.options.language ?? "ru-RU";
        recognition.maxAlternatives = 1;

        recognition.onstart = () => {
            this.isRecognitionStarted = true;
            this.setState("listening");
        };

        recognition.onresult = (event: SpeechRecognitionEvent) => {
            let interimTranscript = "";
            let finalTranscript = "";

            for (let i = event.resultIndex; i < event.results.length; i++) {
                const result = event.results[i];
                const transcript = result[0].transcript;

                if (result.isFinal) {
                    finalTranscript += transcript;
                } else {
                    interimTranscript += transcript;
                }
            }

            if (finalTranscript) {
                this.options.onResult(finalTranscript.trim(), true);
            } else if (interimTranscript) {
                this.options.onResult(interimTranscript.trim(), false);
            }
        };

        recognition.onspeechstart = () => {
            this.options.onSpeechStart?.();
        };

        recognition.onspeechend = () => {
            this.options.onSpeechEnd?.();
        };

        recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
            if (event.error === "no-speech" || event.error === "aborted") return;

            this.setState("error");
            this.options.onError?.(new Error(`Recognition error: ${event.error}`));
        };

        recognition.onend = () => {
            this.isRecognitionStarted = false;
            if (this.shouldRestartAfterEnd && this.state !== "error") {
                try {
                    this.recognition?.start();
                } catch {
                }
            } else if (this.state !== "error") {
                this.setState("idle");
            }
        };

        return recognition;
    }

    private setState(state: WebSpeechState): void {
        this.state = state;
        this.options.onStateChange?.(state);
    }
}
