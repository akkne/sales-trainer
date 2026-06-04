"use client";

import { VoicePipelineState } from "@/lib/hooks/useVoice";
import { Icon } from "@/components/ui/Icon";

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

    // Status text mapping
    const statusText: Record<VoicePipelineState, string> = {
        idle: "Нажмите для голоса",
        initializing: "Инициализация...",
        listening: "Слушаю...",
        speaking: "Говорите...",
        processing: "Обработка...",
        playing: "AI отвечает...",
        error: "Ошибка",
    };

    return (
        <div className="flex flex-col items-center gap-3">
            <button
                onClick={handleClick}
                disabled={isProcessing || isPlaying}
                className={`
                    relative w-20 h-20 rounded-full flex items-center justify-center
                    transition-all duration-200 border
                    ${isActive
                        ? "bg-rust border-rust text-white"
                        : "bg-surface border-line hover:bg-bg-2 text-ink-2"
                    }
                    ${(isProcessing || isPlaying) ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}
                    active:translate-y-px
                `}
                style={{ boxShadow: "var(--sh-2)" }}
            >
                {/* Outer ring animation when speaking */}
                {isSpeaking && (
                    <div className="absolute inset-0 rounded-full animate-ping bg-rust opacity-30" />
                )}

                {/* Listening pulse */}
                {isListening && (
                    <div className="absolute inset-0 rounded-full animate-pulse bg-rust opacity-20" />
                )}

                {/* Icon */}
                {isProcessing ? (
                    <div className="w-7 h-7 border-2 border-current border-t-transparent rounded-full animate-spin" />
                ) : isPlaying ? (
                    <Icon name="bell" size="xl" />
                ) : (
                    <Icon name="mic" size="xl" />
                )}
            </button>

            {/* Status text */}
            <span className={`text-sm font-medium ${isActive ? "text-rust" : "text-ink-3"}`}>
                {statusText[state]}
            </span>
        </div>
    );
}
