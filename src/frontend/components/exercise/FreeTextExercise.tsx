"use client";

import { useCallback, useRef, useState } from "react";
import { useTranscribeAudio } from "@/lib/hooks/useLesson";

interface FreeTextContent {
    situation: string;
    prompt: string;
    evaluationCriteria: string;
}

interface FreeTextExerciseProps {
    content: FreeTextContent;
    onSubmit: (answer: { text: string }) => void;
    isSubmitting: boolean;
}

type RecordingState = "idle" | "recording" | "transcribing";

export function FreeTextExercise({
    content,
    onSubmit,
    isSubmitting,
}: FreeTextExerciseProps) {
    const [responseText, setResponseText] = useState("");
    const [recordingState, setRecordingState] = useState<RecordingState>("idle");
    const [recordingError, setRecordingError] = useState<string | null>(null);

    const mediaRecorderRef = useRef<MediaRecorder | null>(null);
    const chunksRef = useRef<Blob[]>([]);

    const transcribe = useTranscribeAudio();

    const startRecording = useCallback(async () => {
        setRecordingError(null);
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            const recorder = new MediaRecorder(stream, { mimeType: "audio/webm" });
            chunksRef.current = [];

            recorder.ondataavailable = (e) => {
                if (e.data.size > 0) chunksRef.current.push(e.data);
            };

            recorder.onstop = async () => {
                stream.getTracks().forEach((t) => t.stop());
                const blob = new Blob(chunksRef.current, { type: "audio/webm" });
                setRecordingState("transcribing");
                try {
                    const result = await transcribe.mutateAsync(blob);
                    if (result.text.trim()) {
                        setResponseText((prev) =>
                            prev ? `${prev} ${result.text.trim()}` : result.text.trim()
                        );
                    }
                } catch {
                    setRecordingError("Не удалось распознать речь. Попробуй ещё раз.");
                } finally {
                    setRecordingState("idle");
                }
            };

            mediaRecorderRef.current = recorder;
            recorder.start();
            setRecordingState("recording");
        } catch {
            setRecordingError("Нет доступа к микрофону.");
            setRecordingState("idle");
        }
    }, [transcribe]);

    const stopRecording = useCallback(() => {
        mediaRecorderRef.current?.stop();
        mediaRecorderRef.current = null;
    }, []);

    const isBusy = isSubmitting || recordingState !== "idle";

    return (
        <div className="flex flex-col gap-6">
            <div className="bg-[#F7F7F7] rounded-2xl p-4">
                <p className="text-sm text-gray-500 mb-1">Ситуация</p>
                <p className="text-gray-900 font-medium">{content.situation}</p>
            </div>

            <p className="font-[var(--font-space-grotesk)] text-xl font-bold text-gray-900">
                {content.prompt}
            </p>

            <div className="relative">
                <textarea
                    value={responseText}
                    onChange={(e) => setResponseText(e.target.value)}
                    placeholder="Напиши свой ответ или используй микрофон..."
                    rows={5}
                    disabled={recordingState === "transcribing"}
                    className="w-full px-4 py-3 rounded-2xl bg-[#F7F7F7] text-gray-900 placeholder-gray-400 outline-none focus:ring-2 focus:ring-[#58CC02] resize-none disabled:opacity-60"
                />

                {/* Mic button */}
                <button
                    type="button"
                    onClick={recordingState === "recording" ? stopRecording : startRecording}
                    disabled={recordingState === "transcribing" || isSubmitting}
                    title={recordingState === "recording" ? "Остановить запись" : "Записать голос"}
                    className={`absolute bottom-3 right-3 w-9 h-9 rounded-full flex items-center justify-center transition-colors disabled:opacity-40 ${
                        recordingState === "recording"
                            ? "bg-red-500 text-white animate-pulse"
                            : "bg-gray-200 text-gray-600 hover:bg-gray-300"
                    }`}
                >
                    {recordingState === "transcribing" ? (
                        <span className="w-4 h-4 border-2 border-gray-400 border-t-transparent rounded-full animate-spin" />
                    ) : recordingState === "recording" ? (
                        <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                            <rect x="6" y="6" width="12" height="12" rx="1" />
                        </svg>
                    ) : (
                        <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                            <path d="M12 2a3 3 0 0 1 3 3v6a3 3 0 0 1-6 0V5a3 3 0 0 1 3-3z" />
                            <path d="M19 10a7 7 0 0 1-14 0H3a9 9 0 0 0 8 8.94V21h2v-2.06A9 9 0 0 0 21 10h-2z" />
                        </svg>
                    )}
                </button>
            </div>

            {recordingError && (
                <p className="text-sm text-red-500 -mt-3">{recordingError}</p>
            )}

            {recordingState === "recording" && (
                <p className="text-sm text-red-500 -mt-3 flex items-center gap-1.5">
                    <span className="inline-block w-2 h-2 rounded-full bg-red-500 animate-pulse" />
                    Запись идёт… нажми стоп, когда закончишь
                </p>
            )}

            <button
                onClick={() => {
                    if (responseText.trim()) onSubmit({ text: responseText.trim() });
                }}
                disabled={!responseText.trim() || isBusy}
                className="py-4 rounded-2xl bg-[#58CC02] text-white font-bold shadow-[0_4px_0_#4CAD00] active:shadow-none active:translate-y-1 transition-transform disabled:opacity-40"
            >
                {isSubmitting ? "AI оценивает..." : "Отправить"}
            </button>
        </div>
    );
}
