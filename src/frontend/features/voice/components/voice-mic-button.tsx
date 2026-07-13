"use client";

import { VoicePipelineState } from "@/features/voice/hooks/use-voice";
import { Icon } from "@/shared/components/icon";

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

    const statusText: Record<VoicePipelineState, string> = {
        idle: "Нажми для голосового ввода",
        initializing: "Инициализация...",
        listening: "Слушаю...",
        speaking: "Говори...",
        processing: "Обработка...",
        playing: "ИИ отвечает...",
        error: "Ошибка",
    };

    return (
        <div className="vmb-wrap">
            <button
                onClick={handleClick}
                disabled={isProcessing || isPlaying}
                className={"vmb" + (isActive ? " vmb-active" : "") + (isProcessing || isPlaying ? " vmb-disabled" : "")}
                aria-label={statusText[state]}
            >
                {isSpeaking && <span className="vmb-ring vmb-ring-ping" aria-hidden="true" />}
                {isListening && <span className="vmb-ring vmb-ring-pulse" aria-hidden="true" />}

                {isProcessing ? (
                    <span className="vmb-spinner" aria-hidden="true" />
                ) : isPlaying ? (
                    <Icon name="bell" size="xl" />
                ) : (
                    <Icon name="mic" size="xl" />
                )}
            </button>

            <span className={"vmb-label" + (isActive ? " vmb-label-active" : "")}>
                {statusText[state]}
            </span>
        </div>
    );
}
