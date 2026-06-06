export class AudioPcmConverter {
    static ConvertFloatArrayToPcm(float32Array: Float32Array): Int16Array {
        const int16Array = new Int16Array(float32Array.length);
        for (let sampleIndex = 0; sampleIndex < float32Array.length; sampleIndex++) {
            const clampedSample = Math.max(-1, Math.min(1, float32Array[sampleIndex]));
            int16Array[sampleIndex] = clampedSample < 0 ? clampedSample * 0x8000 : clampedSample * 0x7fff;
        }
        return int16Array;
    }
}
