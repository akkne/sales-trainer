export interface VoiceStreamFrame {
    text: string;
    audio: ArrayBuffer;
    isFinal: boolean;
    isStopSignal: boolean;
}

export class VoiceStreamReader {
    private buffer: Uint8Array = new Uint8Array(0);
    private readonly response: Response;

    constructor(response: Response) {
        this.response = response;
    }

    private async ensureBytesLoaded(count: number, reader: ReadableStreamDefaultReader<Uint8Array>, signal?: AbortSignal): Promise<boolean> {
        while (this.buffer.length < count) {
            if (signal?.aborted) return false;
            const { done, value } = await reader.read();
            if (done) return false;
            if (value) {
                const merged = new Uint8Array(this.buffer.length + value.length);
                merged.set(this.buffer, 0);
                merged.set(value, this.buffer.length);
                this.buffer = merged;
            }
        }
        return true;
    }

    private readUInt32(): number {
        const value = (this.buffer[0] << 24) | (this.buffer[1] << 16) | (this.buffer[2] << 8) | this.buffer[3];
        this.buffer = this.buffer.subarray(4);
        return value >>> 0;
    }

    private readByteRange(count: number): Uint8Array {
        const out = new Uint8Array(count);
        out.set(this.buffer.subarray(0, count));
        this.buffer = this.buffer.subarray(count);
        return out;
    }

    async *read(signal?: AbortSignal): AsyncGenerator<VoiceStreamFrame> {
        if (!this.response.body) {
            throw new Error("Response has no body");
        }

        const reader = this.response.body.getReader();
        const decoder = new TextDecoder("utf-8");

        while (true) {
            if (!(await this.ensureBytesLoaded(4, reader, signal))) return;
            const flags = this.readUInt32();

            if (!(await this.ensureBytesLoaded(4, reader, signal))) return;
            const textLength = this.readUInt32();

            if (textLength > 0 && !(await this.ensureBytesLoaded(textLength, reader, signal))) return;
            const textBytes = textLength > 0 ? this.readByteRange(textLength) : new Uint8Array(0);

            if (!(await this.ensureBytesLoaded(4, reader, signal))) return;
            const audioLength = this.readUInt32();

            if (audioLength > 0 && !(await this.ensureBytesLoaded(audioLength, reader, signal))) return;
            const audioBytes = audioLength > 0 ? this.readByteRange(audioLength) : new Uint8Array(0);

            const audio = new ArrayBuffer(audioBytes.byteLength);
            new Uint8Array(audio).set(audioBytes);

            yield {
                text: decoder.decode(textBytes),
                audio,
                isFinal: (flags & 1) !== 0,
                isStopSignal: (flags & 2) !== 0,
            };
        }
    }
}
