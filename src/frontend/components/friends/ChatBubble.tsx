"use client";

import type { ChatMessageData } from "@/lib/hooks/useChat";

interface ChatBubbleProps {
    message: ChatMessageData;
}

function formatMessageTime(dateString: string): string {
    return new Date(dateString).toLocaleTimeString("ru-RU", {
        hour: "2-digit",
        minute: "2-digit",
    });
}

export function ChatBubble({ message }: ChatBubbleProps) {
    return (
        <div className={`flex ${message.isOwn ? "justify-end" : "justify-start"}`}>
            <div
                className={`max-w-[75%] px-4 py-3 text-sm leading-relaxed ${
                    message.isOwn
                        ? "rounded-2xl rounded-tr-sm"
                        : "rounded-2xl rounded-tl-sm"
                }`}
                style={
                    message.isOwn
                        ? { background: "var(--ink)", color: "var(--bg)" }
                        : { background: "var(--bg-2)", color: "var(--ink)" }
                }
            >
                <p className="whitespace-pre-wrap break-words">{message.content}</p>
                <p
                    className="text-[10px] mt-1.5 font-mono"
                    style={{ color: message.isOwn ? "var(--ink-4)" : "var(--ink-4)" }}
                >
                    {formatMessageTime(message.sentAt)}
                </p>
            </div>
        </div>
    );
}
