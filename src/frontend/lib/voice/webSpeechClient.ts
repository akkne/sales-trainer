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
    private shouldRestart = false;

    constructor(options: WebSpeechClientOptions) {
        this.options = options;
    }

    async start(): Promise<void> {
        if (!isWebSpeechSupported()) {
            this.setState("error");
            this.options.onError?.(new Error("Web Speech API не поддерживается в этом браузере"));
            return;
        }

        // Request microphone permission
        try {
            await navigator.mediaDevices.getUserMedia({ audio: true });
        } catch {
            this.setState("error");
            this.options.onError?.(new Error("Нет доступа к микрофону"));
            return;
        }

        const SpeechRecognitionClass = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognitionClass) {
            this.setState("error");
            this.options.onError?.(new Error("Web Speech API не поддерживается"));
            return;
        }

        this.recognition = new SpeechRecognitionClass();
        this.recognition.continuous = this.options.continuous ?? true;
        this.recognition.interimResults = this.options.interimResults ?? true;
        this.recognition.lang = this.options.language ?? "ru-RU";
        this.recognition.maxAlternatives = 1;

        this.shouldRestart = true;

        this.recognition.onstart = () => {
            this.setState("listening");
        };

        this.recognition.onresult = (event: SpeechRecognitionEvent) => {
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

        this.recognition.onspeechstart = () => {
            this.options.onSpeechStart?.();
        };

        this.recognition.onspeechend = () => {
            this.options.onSpeechEnd?.();
        };

        this.recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
            // "no-speech" and "aborted" are not real errors
            if (event.error === "no-speech" || event.error === "aborted") {
                return;
            }

            this.setState("error");
            this.options.onError?.(new Error(`Ошибка распознавания: ${event.error}`));
        };

        this.recognition.onend = () => {
            // Auto-restart if should continue
            if (this.shouldRestart && this.state === "listening") {
                try {
                    this.recognition?.start();
                } catch {
                    // Ignore if already started
                }
            } else {
                this.setState("idle");
            }
        };

        try {
            this.recognition.start();
        } catch (error) {
            this.setState("error");
            this.options.onError?.(error instanceof Error ? error : new Error("Не удалось запустить распознавание"));
        }
    }

    stop(): void {
        this.shouldRestart = false;
        if (this.recognition) {
            this.recognition.stop();
            this.recognition = null;
        }
        this.setState("idle");
    }

    pause(): void {
        this.shouldRestart = false;
        if (this.recognition) {
            try {
                this.recognition.stop();
            } catch {
                // Ignore if not started
            }
        }
        this.setState("idle");
    }

    resume(): void {
        if (this.state !== "idle") {
            return;
        }

        this.shouldRestart = true;

        // If recognition exists but may have stopped, recreate it
        if (!this.recognition) {
            const SpeechRecognitionClass = window.SpeechRecognition || window.webkitSpeechRecognition;
            if (!SpeechRecognitionClass) {
                return;
            }
            this.recognition = new SpeechRecognitionClass();
            this.setupRecognition();
        }

        try {
            this.recognition.start();
        } catch {
            // If start fails (already running), let onend handler restart
        }
    }

    private setupRecognition(): void {
        if (!this.recognition) return;

        this.recognition.continuous = this.options.continuous ?? true;
        this.recognition.interimResults = this.options.interimResults ?? true;
        this.recognition.lang = this.options.language ?? "ru-RU";
        this.recognition.maxAlternatives = 1;

        this.recognition.onstart = () => {
            this.setState("listening");
        };

        this.recognition.onresult = (event: SpeechRecognitionEvent) => {
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

        this.recognition.onspeechstart = () => {
            this.options.onSpeechStart?.();
        };

        this.recognition.onspeechend = () => {
            this.options.onSpeechEnd?.();
        };

        this.recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
            if (event.error === "no-speech" || event.error === "aborted") {
                return;
            }

            this.setState("error");
            this.options.onError?.(new Error(`Ошибка распознавания: ${event.error}`));
        };

        this.recognition.onend = () => {
            if (this.shouldRestart && this.state === "listening") {
                try {
                    this.recognition?.start();
                } catch {
                    // Ignore if already started
                }
            } else {
                this.setState("idle");
            }
        };
    }

    getState(): WebSpeechState {
        return this.state;
    }

    private setState(state: WebSpeechState): void {
        this.state = state;
        this.options.onStateChange?.(state);
    }
}
