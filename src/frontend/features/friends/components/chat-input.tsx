"use client";

import { useState, type KeyboardEvent } from "react";
import { Icon } from "@/shared/components/icon";

interface ChatInputProps {
    // Returns whether the send actually succeeded. The composer only clears the draft on
    // `true` — on `false` the typed text stays put so a rejected message is never presented
    // as sent (docs/AUDIT_SILENT_WRITES.md W-1).
    onSend: (content: string) => Promise<boolean>;
    disabled?: boolean;
}

/** V2 rail chat composer — token-based, matches design §3.4 composer spec. */
export function RailChatInput({ onSend, disabled }: ChatInputProps) {
    const [value, setValue] = useState("");

    async function handleSend() {
        const trimmed = value.trim();
        if (!trimmed) return;
        const succeeded = await onSend(trimmed);
        if (succeeded) setValue("");
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
                placeholder="Напиши сообщение…"
                rows={1}
                disabled={disabled}
                className="frd-composer-input"
                aria-label="Сообщение"
            />
            <button
                onClick={handleSend}
                disabled={disabled || !value.trim()}
                className="frd-composer-send"
                aria-label="Отправить"
            >
                <Icon name="send" size={16} />
            </button>
        </div>
    );
}

// Legacy alias for any existing import of ChatInput
export { RailChatInput as ChatInput };
