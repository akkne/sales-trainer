export interface VoiceStreamFrame {
    text: string;
    audio: ArrayBuffer;
    isFinal: boolean;
    isStopSignal: boolean;
}

export async function* readVoiceStream(
    response: Response,
    signal?: AbortSignal,
): AsyncGenerator<VoiceStreamFrame> {
    if (!response.body) {
        throw new Error("Response has no body");
    }

    const reader = response.body.getReader();
    let buffer = new Uint8Array(0);

    const ensureBytes = async (n: number): Promise<boolean> => {
        while (buffer.length < n) {
            if (signal?.aborted) return false;
            const { done, value } = await reader.read();
            if (done) return false;
            if (value) {
                const merged = new Uint8Array(buffer.length + value.length);
                merged.set(buffer, 0);
                merged.set(value, buffer.length);
                buffer = merged;
            }
        }
        return true;
    };

    const readU32 = (): number => {
        const v = (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
        buffer = buffer.subarray(4);
        return v >>> 0;
    };

    const readBytes = (n: number): Uint8Array => {
        const out = new Uint8Array(n);
        out.set(buffer.subarray(0, n));
        buffer = buffer.subarray(n);
        return out;
    };

    const decoder = new TextDecoder("utf-8");

    while (true) {
        if (!(await ensureBytes(4))) return;
        const flags = readU32();

        if (!(await ensureBytes(4))) return;
        const textLength = readU32();

        if (textLength > 0 && !(await ensureBytes(textLength))) return;
        const textBytes = textLength > 0 ? readBytes(textLength) : new Uint8Array(0);

        if (!(await ensureBytes(4))) return;
        const audioLength = readU32();

        if (audioLength > 0 && !(await ensureBytes(audioLength))) return;
        const audioBytes = audioLength > 0 ? readBytes(audioLength) : new Uint8Array(0);

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
