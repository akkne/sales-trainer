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
    // Desired lifecycle flag: while true, the recognizer is restarted from
    // `onend` whenever the browser stops it (silence timeout, pause/resume
    // races, etc.). The native recognizer stops asynchronously, so `onend`
    // is the only reliable place to decide whether to keep listening.
    private shouldRestart = false;
    private isStarted = false;

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

        const recognition = this.createRecognition();
        if (!recognition) {
            this.setState("error");
            this.options.onError?.(new Error("Web Speech API не поддерживается"));
            return;
        }

        this.recognition = recognition;
        this.shouldRestart = true;

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
            this.recognition.onend = null;
            this.recognition.stop();
            this.recognition = null;
        }
        this.isStarted = false;
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
        if (!this.recognition) {
            return;
        }

        this.shouldRestart = true;

        if (this.isStarted) {
            // The recognizer is still shutting down after pause(); `onend`
            // will fire shortly and restart it because shouldRestart is true.
            return;
        }

        try {
            this.recognition.start();
        } catch {
            // Already starting/running — onend will keep it alive.
        }
    }

    getState(): WebSpeechState {
        return this.state;
    }

    private createRecognition(): SpeechRecognition | null {
        const SpeechRecognitionClass = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognitionClass) {
            return null;
        }

        const recognition = new SpeechRecognitionClass();
        recognition.continuous = this.options.continuous ?? true;
        recognition.interimResults = this.options.interimResults ?? true;
        recognition.lang = this.options.language ?? "ru-RU";
        recognition.maxAlternatives = 1;

        recognition.onstart = () => {
            this.isStarted = true;
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
            // "no-speech" and "aborted" are not real errors
            if (event.error === "no-speech" || event.error === "aborted") {
                return;
            }

            this.setState("error");
            this.options.onError?.(new Error(`Ошибка распознавания: ${event.error}`));
        };

        recognition.onend = () => {
            this.isStarted = false;
            // Restart purely on the desired-state flag. Checking `state`
            // here is racy: pause()/resume() flip state synchronously while
            // the native recognizer stops asynchronously, which used to leave
            // the mic permanently dead after the first AI reply.
            if (this.shouldRestart && this.state !== "error") {
                try {
                    this.recognition?.start();
                } catch {
                    // Ignore if already started
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
