"use client";

import { useState, FormEvent } from "react";
import { Icon } from "@/shared/components/icon";

interface ChatInputProps {
    onSend: (content: string) => void;
    disabled: boolean;
    placeholder?: string;
}

export function ChatInput({ onSend, disabled, placeholder = "Введите сообщение…" }: ChatInputProps) {
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
        <form onSubmit={handleSubmit} className="row gap-3" style={{ width: "100%" }}>
            <input
                type="text"
                value={inputValue}
                onChange={(changeEvent) => setInputValue(changeEvent.target.value)}
                disabled={disabled}
                placeholder={placeholder}
                className="field grow"
                style={disabled ? { opacity: 0.5, cursor: "not-allowed" } : undefined}
            />
            <button
                type="submit"
                disabled={!canSend}
                aria-label="Отправить"
                className={"btn " + (canSend ? "btn-primary" : "btn-soft")}
                style={{ width: 48, padding: 0, flex: "none", ...(canSend ? {} : { opacity: 0.5, cursor: "not-allowed", boxShadow: "none" }) }}
            >
                <Icon name="send" size="md" />
            </button>
        </form>
    );
}
