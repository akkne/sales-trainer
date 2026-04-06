"use client";

import { useState, FormEvent } from "react";

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

    return (
        <form onSubmit={handleSubmit} className="flex gap-4">
            <input
                type="text"
                value={inputValue}
                onChange={(changeEvent) => setInputValue(changeEvent.target.value)}
                disabled={disabled}
                placeholder={placeholder}
                className="flex-1 px-4 py-3 border-2 border-gray-200 rounded-2xl focus:border-[#58CC02] focus:outline-none disabled:bg-gray-50 disabled:text-gray-400"
            />
            <button
                type="submit"
                disabled={disabled || !inputValue.trim()}
                className="px-8 py-3 bg-[#58CC02] text-white font-bold rounded-2xl hover:bg-[#4CAD02] disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
            >
                →
            </button>
        </form>
    );
}
