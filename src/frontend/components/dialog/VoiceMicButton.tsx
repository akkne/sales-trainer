"use client";

import { VoicePipelineState } from "@/lib/hooks/useVoice";

interface VoiceMicButtonProps {
    state: VoicePipelineState;
    isAvailable: boolean;
    onStart: () => void;
    onStop: () => void;
}

export function VoiceMicButton({ state, isAvailable, onStart, onStop }: VoiceMicButtonProps) {
    if (!isAvailable) {
        return null;
    }

    const isActive = state !== "idle" && state !== "error";
    const isSpeaking = state === "speaking";
    const isProcessing = state === "processing";
    const isPlaying = state === "playing";
    const isListening = state === "listening";

    const handleClick = () => {
        if (isActive) {
            onStop();
        } else {
            onStart();
        }
    };

    return (
        <div className="flex flex-col items-center gap-2">
            <button
                onClick={handleClick}
                disabled={isProcessing || isPlaying}
                className={`
                    relative w-16 h-16 rounded-full flex items-center justify-center
                    transition-all duration-200
                    ${isActive ? "bg-[#58CC02]" : "bg-gray-200 hover:bg-gray-300"}
                    ${(isProcessing || isPlaying) ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}
                `}
            >
                {/* Outer ring animation when speaking */}
                {isSpeaking && (
                    <div className="absolute inset-0 rounded-full animate-ping bg-[#58CC02] opacity-30" />
                )}

                {/* Listening pulse */}
                {isListening && (
                    <div className="absolute inset-0 rounded-full animate-pulse bg-[#58CC02] opacity-20" />
                )}

                {/* Icon */}
                <div className={`text-2xl ${isActive ? "text-white" : "text-gray-600"}`}>
                    {isProcessing ? (
                        <LoadingSpinner />
                    ) : isPlaying ? (
                        <SpeakerIcon />
                    ) : (
                        <MicIcon />
                    )}
                </div>
            </button>

            {/* Status text */}
            <span className="text-xs text-gray-500">
                {state === "idle" && "Нажмите для голоса"}
                {state === "initializing" && "Инициализация..."}
                {state === "listening" && "Слушаю..."}
                {state === "speaking" && "Говорите..."}
                {state === "processing" && "Обработка..."}
                {state === "playing" && "Отвечает..."}
                {state === "error" && "Ошибка"}
            </span>
        </div>
    );
}

function MicIcon() {
    return (
        <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={2}
            stroke="currentColor"
            className="w-6 h-6"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 18.75a6 6 0 006-6v-1.5m-6 7.5a6 6 0 01-6-6v-1.5m6 7.5v3.75m-3.75 0h7.5M12 15.75a3 3 0 01-3-3V4.5a3 3 0 116 0v8.25a3 3 0 01-3 3z"
            />
        </svg>
    );
}

function SpeakerIcon() {
    return (
        <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={2}
            stroke="currentColor"
            className="w-6 h-6"
        >
            <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M19.114 5.636a9 9 0 010 12.728M16.463 8.288a5.25 5.25 0 010 7.424M6.75 8.25l4.72-4.72a.75.75 0 011.28.53v15.88a.75.75 0 01-1.28.53l-4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.01 9.01 0 012.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75z"
            />
        </svg>
    );
}

function LoadingSpinner() {
    return (
        <svg
            className="animate-spin w-6 h-6"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
        >
            <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
            />
            <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
        </svg>
    );
}
