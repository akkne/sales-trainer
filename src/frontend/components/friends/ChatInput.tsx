"use client";

import { useState, type KeyboardEvent } from "react";
import { Icon } from "@/components/ui/Icon";

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
        <div className="flex items-end gap-3 px-4 py-3 bg-surface border-t border-line">
            <textarea
                value={inputValue}
                onChange={(event) => setInputValue(event.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Написать сообщение..."
                rows={1}
                disabled={disabled}
                className="flex-1 resize-none bg-bg-2 rounded-xl px-4 py-3 text-sm text-ink placeholder:text-ink-4 focus:outline-none focus:ring-2 max-h-32 overflow-y-auto border border-line"
                style={{ "--tw-ring-color": "var(--indigo)" } as React.CSSProperties}
            />
            <button
                onClick={handleSend}
                disabled={disabled || !inputValue.trim()}
                className="p-3 rounded-xl text-white disabled:opacity-40 transition-opacity hover:opacity-90 shrink-0"
                style={{ background: "var(--indigo)", boxShadow: "var(--sh-2)" }}
                aria-label="Отправить"
            >
                <Icon name="send" size="md" />
            </button>
        </div>
    );
}
