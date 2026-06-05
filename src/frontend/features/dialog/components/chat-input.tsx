"use client";

import { useState, FormEvent } from "react";
import { Icon } from "@/shared/components/icon";

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
                className="flex-1 px-4 py-3 bg-surface-container-low text-on-surface placeholder-on-surface-variant rounded-full border-2 border-transparent focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 disabled:opacity-50 disabled:cursor-not-allowed tonal-transition"
            />
            <button
                type="submit"
                disabled={!canSend}
                className={`w-12 h-12 rounded-full flex items-center justify-center tonal-transition ${
                    canSend
                        ? "bg-primary text-on-primary shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1"
                        : "bg-surface-container-high text-on-surface-variant cursor-not-allowed"
                }`}
            >
                <Icon name="send" size="md" />
            </button>
        </form>
    );
}
