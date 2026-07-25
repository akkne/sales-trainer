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
    // True between start() and stop(); stays true across pause()/resume() so the
    // onend handler and resume() know the session is still meant to be listening.
    private isActive = false;

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

        this.isActive = true;
        this.shouldRestartAfterEnd = true;
        this.launchRecognition();
    }

    stop(): void {
        this.isActive = false;
        this.shouldRestartAfterEnd = false;
        this.disposeRecognition();
        this.isRecognitionStarted = false;
        this.setState("idle");
    }

    pause(): void {
        this.shouldRestartAfterEnd = false;
        // Tear the instance down completely rather than just stopping it. Mobile
        // browsers reject start() on a SpeechRecognition that has already ended,
        // so resume() must spin up a fresh instance instead of reusing this one.
        this.disposeRecognition();
        this.isRecognitionStarted = false;
        this.setState("idle");
    }

    resume(): void {
        if (!this.isActive) return;
        this.shouldRestartAfterEnd = true;
        // Already have a live (or starting) recognition — nothing to do.
        if (this.recognition) return;
        this.launchRecognition();
    }

    getState(): WebSpeechState {
        return this.state;
    }

    // Builds a brand-new SpeechRecognition and starts it. A fresh instance every
    // time is what keeps the mic alive across turns on mobile, where reusing an
    // ended instance silently fails.
    private launchRecognition(): void {
        const recognition = this.createRecognition();
        if (!recognition) {
            this.setState("error");
            this.options.onError?.(new Error("Web Speech API is not supported"));
            return;
        }

        this.recognition = recognition;

        try {
            recognition.start();
        } catch {
            // start() can throw InvalidStateError if the previous instance has not
            // fully released the mic yet; onend will retry the relaunch.
        }
    }

    private disposeRecognition(): void {
        if (this.recognition) {
            this.recognition.onend = null;
            try {
                this.recognition.stop();
            } catch {
            }
            this.recognition = null;
        }
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
            // Mobile browsers auto-end recognition after each utterance even in
            // continuous mode. Relaunch a fresh instance (not this ended one) to
            // keep the mic alive between turns.
            if (this.shouldRestartAfterEnd && this.isActive && this.state !== "error") {
                this.recognition = null;
                this.launchRecognition();
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
