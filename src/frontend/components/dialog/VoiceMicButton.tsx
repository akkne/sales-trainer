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
                    transition-all duration-200 shadow-lg
                    ${isActive
                        ? "bg-primary shadow-[0_4px_0_var(--color-primary-dim)]"
                        : "bg-surface-container-high hover:bg-surface-container-highest shadow-[0_4px_0_var(--color-outline-variant)]"
                    }
                    ${(isProcessing || isPlaying) ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}
                    active:translate-y-1 active:shadow-none
                `}
            >
                {/* Outer ring animation when speaking */}
                {isSpeaking && (
                    <div className="absolute inset-0 rounded-full animate-ping bg-primary opacity-30" />
                )}

                {/* Listening pulse */}
                {isListening && (
                    <div className="absolute inset-0 rounded-full animate-pulse bg-primary opacity-20" />
                )}

                {/* Icon */}
                {isProcessing ? (
                    <div className={`w-7 h-7 border-3 border-current border-t-transparent rounded-full animate-spin ${isActive ? "text-on-primary" : "text-on-surface-variant"}`} />
                ) : isPlaying ? (
                    <Icon
                        name="volume_up"
                        size="xl"
                        className={isActive ? "text-on-primary" : "text-on-surface-variant"}
                    />
                ) : (
                    <Icon
                        name={isActive ? "mic" : "mic"}
                        size="xl"
                        variant={isActive ? "filled" : "outlined"}
                        className={isActive ? "text-on-primary" : "text-on-surface-variant"}
                    />
                )}
            </button>

            {/* Status text */}
            <span className={`text-sm font-medium ${isActive ? "text-primary" : "text-on-surface-variant"}`}>
                {statusText[state]}
            </span>
        </div>
    );
}
