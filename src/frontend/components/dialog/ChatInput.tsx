"use client";

import { useState, FormEvent } from "react";
import { Icon } from "@/components/ui/Icon";

interface ChatInputProps {
    onSend: (content: string) => void;
    disabled: boolean;
    placeholder?: string;
}

export function ChatInput({ onSend, disabled, placeholder = "Напишите сообщение..." }: ChatInputProps) {
    const [inputValue, setInputValue] = useState("");

    const handleSubmit = (submitEvent: FormEvent) => {
        submitEvent.preventDefault();
        const trimmedValue = inputValue.trim();
        if (!trimmedValue || disabled) return;

        onSend(trimmedValue);
        setInputValue("");
    };

    const canSend = !disabled && inputValue.trim().length > 0;

    return (
        <form onSubmit={handleSubmit} className="flex gap-3">
            <input
                type="text"
                value={inputValue}
                onChange={(changeEvent) => setInputValue(changeEvent.target.value)}
                disabled={disabled}
                placeholder={placeholder}
                className="flex-1 px-4 py-3 bg-surface text-ink placeholder:text-ink-4 rounded-full border border-line focus:border-indigo focus:outline-none focus:ring-2 focus:ring-indigo/20 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            />
            <button
                type="submit"
                disabled={!canSend}
                className={`w-12 h-12 rounded-full flex items-center justify-center transition-colors ${
                    canSend
                        ? "bg-ink text-bg active:translate-y-px"
                        : "bg-surface-2 text-ink-4 cursor-not-allowed"
                }`}
                style={canSend ? { boxShadow: "var(--sh-2)" } : undefined}
            >
                <Icon name="send" size="md" />
            </button>
        </form>
    );
}
