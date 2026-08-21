"use client";

import { useState, FormEvent } from "react";
import { Icon } from "@/shared/components/icon";

interface ChatInputProps {
    // Returns whether the send actually succeeded. The composer clears the draft as soon as
    // the send starts (so it doesn't sit on screen next to its own optimistic bubble for the
    // whole round-trip) and restores it on `false` (or a caller that never resolves it) so a
    // rejected send never looks like it went out (docs/AUDIT_SILENT_WRITES.md W-5).
    onSend: (content: string) => Promise<boolean>;
    disabled: boolean;
    placeholder?: string;
}

export function ChatInput({ onSend, disabled, placeholder = "Напиши сообщение…" }: ChatInputProps) {
    const [inputValue, setInputValue] = useState("");

    const handleSubmit = async (submitEvent: FormEvent) => {
        submitEvent.preventDefault();
        const trimmedValue = inputValue.trim();
        if (!trimmedValue || disabled) return;

        setInputValue("");
        const succeeded = await onSend(trimmedValue);
        if (!succeeded) setInputValue(trimmedValue);
    };

    const canSend = !disabled && inputValue.trim().length > 0;

    return (
        <form onSubmit={handleSubmit} className="dc-input-row">
            <input
                type="text"
                value={inputValue}
                onChange={(changeEvent) => setInputValue(changeEvent.target.value)}
                disabled={disabled}
                placeholder={placeholder}
                className="field"
                aria-label="Сообщение"
                style={disabled ? { opacity: 0.5, cursor: "not-allowed" } : undefined}
            />
            <button
                type="submit"
                disabled={!canSend}
                aria-label="Отправить"
                className={"btn " + (canSend ? "btn-primary" : "btn-soft")}
                style={{ width: 44, height: 44, padding: 0, flex: "none", ...(canSend ? {} : { opacity: 0.45, cursor: "not-allowed", boxShadow: "none" }) }}
            >
                <Icon name="send" size="md" />
            </button>
        </form>
    );
}
