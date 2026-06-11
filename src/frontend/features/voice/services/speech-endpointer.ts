export interface SpeechEndpointerOptions {
    /** Silence window (ms) after a final recognition result before the utterance is committed. */
    silenceMs: number;
    /**
     * Extra wait (ms) added to the silence window when only interim results have been seen.
     * Interim results lag real speech less than browser finalization, but they also pause
     * between words, so the window must be wider to avoid cutting the speaker off mid-sentence.
     */
    interimGraceMs?: number;
    onUtterance: (text: string) => void;
}

const defaultInterimGraceMilliseconds = 250;

/**
 * Decides when the user has finished speaking.
 *
 * The Web Speech API takes 0.5–1.5s after speech ends to deliver a final result.
 * Waiting only for finals adds that delay to every dialog turn, so the endpointer
 * also arms a (slightly wider) silence timer on interim results: if recognition
 * stops updating, the latest interim text is committed without waiting for the
 * browser to finalize it.
 */
export class SpeechEndpointer {
    private committedText = "";
    private interimText = "";
    private timer: ReturnType<typeof setTimeout> | null = null;
    private readonly endpointerOptions: SpeechEndpointerOptions;

    constructor(endpointerOptions: SpeechEndpointerOptions) {
        this.endpointerOptions = endpointerOptions;
    }

    /** Combined text of the utterance accumulated so far (committed finals + latest interim). */
    get currentText(): string {
        return [this.committedText, this.interimText].filter(Boolean).join(" ");
    }

    handleResult(transcript: string, isFinal: boolean): void {
        this.clearTimer();

        if (isFinal) {
            this.committedText += (this.committedText ? " " : "") + transcript;
            this.interimText = "";
            this.armTimer(this.endpointerOptions.silenceMs);
        } else {
            this.interimText = transcript;
            const graceMilliseconds = this.endpointerOptions.interimGraceMs ?? defaultInterimGraceMilliseconds;
            this.armTimer(this.endpointerOptions.silenceMs + graceMilliseconds);
        }
    }

    /** Cancels the pending timer and clears accumulated text (e.g. on stop or barge-in). */
    reset(): void {
        this.clearTimer();
        this.committedText = "";
        this.interimText = "";
    }

    private armTimer(delayMilliseconds: number): void {
        this.timer = setTimeout(() => {
            this.timer = null;
            const utterance = this.currentText.trim();
            this.committedText = "";
            this.interimText = "";
            if (utterance) {
                this.endpointerOptions.onUtterance(utterance);
            }
        }, delayMilliseconds);
    }

    private clearTimer(): void {
        if (this.timer) {
            clearTimeout(this.timer);
            this.timer = null;
        }
    }
}
