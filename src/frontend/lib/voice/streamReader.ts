/**
 * Decoder for the framed chunk stream emitted by POST /dialog/sessions/{id}/voice/stream.
 *
 * Frame layout (big-endian):
 *   u32 flags        bit 0 = isFinal, bit 1 = isStopSignal
 *   u32 textLength
 *   u8[textLength]   utf-8 text
 *   u32 audioLength
 *   u8[audioLength]  mp3 bytes
 */

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
        const v =
            (buffer[0] << 24) |
            (buffer[1] << 16) |
            (buffer[2] << 8) |
            buffer[3];
        buffer = buffer.subarray(4);
        return v >>> 0;
    };

    const readBytes = (n: number): Uint8Array => {
        const slice = buffer.subarray(0, n);
        // Copy so subsequent buffer mutation doesn't affect the returned slice.
        const out = new Uint8Array(n);
        out.set(slice);
        buffer = buffer.subarray(n);
        return out;
    };

    const decoder = new TextDecoder("utf-8");

    while (true) {
        if (!(await ensureBytes(4))) return;
        const flags = readU32();

        if (!(await ensureBytes(4))) return;
        const textLength = readU32();

        if (textLength > 0) {
            if (!(await ensureBytes(textLength))) return;
        }
        const textBytes = textLength > 0 ? readBytes(textLength) : new Uint8Array(0);

        if (!(await ensureBytes(4))) return;
        const audioLength = readU32();

        if (audioLength > 0) {
            if (!(await ensureBytes(audioLength))) return;
        }
        const audioBytes = audioLength > 0 ? readBytes(audioLength) : new Uint8Array(0);

        yield {
            text: decoder.decode(textBytes),
            // ArrayBuffer copy — the underlying buffer is a slab we keep mutating.
            audio: (() => { const ab = new ArrayBuffer(audioBytes.byteLength); new Uint8Array(ab).set(audioBytes); return ab; })(),
            isFinal: (flags & 1) !== 0,
            isStopSignal: (flags & 2) !== 0,
        };
    }
}
