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
                className={`max-w-[75%] px-4 py-2.5 text-sm leading-relaxed ${
                    message.isOwn
                        ? "bg-primary text-on-primary rounded-2xl rounded-tr-sm"
                        : "bg-surface-container text-on-surface rounded-2xl rounded-tl-sm"
                }`}
            >
                <p className="whitespace-pre-wrap break-words">{message.content}</p>
                <p
                    className={`text-[10px] mt-1 ${
                        message.isOwn ? "text-on-primary/70" : "text-on-surface-variant"
                    }`}
                >
                    {formatMessageTime(message.sentAt)}
                </p>
            </div>
        </div>
    );
}
