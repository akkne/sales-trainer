"use client";

import { useState, type KeyboardEvent } from "react";
import { Icon } from "@/shared/components/icon";

interface ChatInputProps {
    onSend: (content: string) => void;
    disabled?: boolean;
}

export function ChatInput({ onSend, disabled }: ChatInputProps) {
    const [inputValue, setInputValue] = useState("");

    function handleSend() {
        const trimmedValue = inputValue.trim();
        if (!trimmedValue) return;
        onSend(trimmedValue);
        setInputValue("");
    }

    function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            handleSend();
        }
    }

    return (
        <div className="flex items-end gap-2 px-4 py-3 bg-surface-container-low border-t border-outline-variant">
            <textarea
                value={inputValue}
                onChange={(event) => setInputValue(event.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Написать сообщение..."
                rows={1}
                disabled={disabled}
                className="flex-1 resize-none bg-surface-container rounded-2xl px-4 py-3 text-sm text-on-surface placeholder:text-on-surface-variant focus:outline-none focus:ring-2 focus:ring-primary max-h-32 overflow-y-auto"
            />
            <button
                onClick={handleSend}
                disabled={disabled || !inputValue.trim()}
                className="p-3 rounded-full bg-primary text-on-primary disabled:opacity-40 tonal-transition hover:opacity-90 shrink-0"
                aria-label="Отправить"
            >
                <Icon name="send" size="md" />
            </button>
        </div>
    );
}
