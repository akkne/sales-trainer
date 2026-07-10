"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useTranscribeAudio } from "@/features/exercise/hooks/use-lesson";

// Phase 39.15 — voice memo recorder for the call-log "Вставить заметки" textarea.
// Mirrors the (unused-until-now) free-text exercise recorder pipeline: MediaRecorder
// captures a webm blob, then the shared `useTranscribeAudio` hook (POST
// /transcription/transcribe via the gateway -> ai-service) turns it into text.
export type VoiceMemoRecorderState =
    | "idle"
    | "requesting-permission"
    | "recording"
    | "transcribing"
    | "error";

export function isVoiceMemoRecordingSupported(): boolean {
    return (
        typeof window !== "undefined" &&
        typeof window.MediaRecorder !== "undefined" &&
        !!navigator.mediaDevices?.getUserMedia
    );
}

interface UseVoiceMemoRecorderOptions {
    onTranscript: (text: string) => void;
}

export function useVoiceMemoRecorder({ onTranscript }: UseVoiceMemoRecorderOptions) {
    const [state, setState] = useState<VoiceMemoRecorderState>("idle");
    const [error, setError] = useState<string | null>(null);
    const mediaRecorderRef = useRef<MediaRecorder | null>(null);
    const chunksRef = useRef<Blob[]>([]);
    const streamRef = useRef<MediaStream | null>(null);
    const transcribeAudio = useTranscribeAudio();

    const isSupported = isVoiceMemoRecordingSupported();

    const stopStream = useCallback(() => {
        streamRef.current?.getTracks().forEach((track) => track.stop());
        streamRef.current = null;
    }, []);

    const startRecording = useCallback(async () => {
        if (!isSupported) {
            setError("Запись голоса не поддерживается в этом браузере");
            setState("error");
            return;
        }

        setError(null);
        setState("requesting-permission");

        let stream: MediaStream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        } catch {
            setError("Доступ к микрофону запрещён");
            setState("error");
            return;
        }

        streamRef.current = stream;
        chunksRef.current = [];

        const recorder = new MediaRecorder(stream);
        mediaRecorderRef.current = recorder;

        recorder.ondataavailable = (event: BlobEvent) => {
            if (event.data.size > 0) chunksRef.current.push(event.data);
        };

        recorder.onstop = () => {
            stopStream();
            const blob = new Blob(chunksRef.current, { type: "audio/webm" });
            chunksRef.current = [];
            setState("transcribing");

            transcribeAudio.mutate(blob, {
                onSuccess: (result) => {
                    setState("idle");
                    onTranscript(result.text);
                },
                onError: (mutationError: Error) => {
                    setState("error");
                    setError(mutationError.message);
                },
            });
        };

        recorder.start();
        setState("recording");
    }, [isSupported, onTranscript, stopStream, transcribeAudio]);

    const stopRecording = useCallback(() => {
        if (mediaRecorderRef.current && mediaRecorderRef.current.state !== "inactive") {
            mediaRecorderRef.current.stop();
        }
    }, []);

    // Release the microphone if the component unmounts mid-recording.
    useEffect(() => stopStream, [stopStream]);

    return {
        state,
        error,
        isSupported,
        startRecording,
        stopRecording,
    };
}
