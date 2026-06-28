"use client";

import { useState, type KeyboardEvent } from "react";
import { Icon } from "@/shared/components/icon";

interface ChatInputProps {
    onSend: (content: string) => void;
    disabled?: boolean;
}

/** V2 rail chat composer — token-based, matches design §3.4 composer spec. */
export function RailChatInput({ onSend, disabled }: ChatInputProps) {
    const [value, setValue] = useState("");

    function handleSend() {
        const trimmed = value.trim();
        if (!trimmed) return;
        onSend(trimmed);
        setValue("");
    }

    function handleKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            handleSend();
        }
    }

    return (
        <div className="frd-composer">
            <textarea
                value={value}
                onChange={(e) => setValue(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Write a message…"
                rows={1}
                disabled={disabled}
                className="frd-composer-input"
                aria-label="Message"
            />
            <button
                onClick={handleSend}
                disabled={disabled || !value.trim()}
                className="frd-composer-send"
                aria-label="Send"
            >
                <Icon name="send" size={16} />
            </button>
        </div>
    );
}

// Legacy alias for any existing import of ChatInput
export { RailChatInput as ChatInput };
