"use client";

import type { ChatMessageData } from "@/features/friends/hooks/use-chat";

interface ChatBubbleProps {
    message: ChatMessageData;
}

function formatMessageTime(dateString: string): string {
    return new Date(dateString).toLocaleTimeString("ru-RU", {
        hour: "2-digit",
        minute: "2-digit",
    });
}

/** V2 rail bubble — violet for own messages, #F1F1F4 for theirs. */
export function RailChatBubble({ message }: ChatBubbleProps) {
    return (
        <div className={`frd-bubble-wrap ${message.isOwn ? "own" : "them"}`}>
            <div className={`frd-bubble ${message.isOwn ? "own" : "them"}`}>
                <p style={{ whiteSpace: "pre-wrap", wordBreak: "break-word", margin: 0 }}>
                    {message.content}
                </p>
                <span className="frd-bubble-time">
                    {formatMessageTime(message.sentAt)}
                </span>
            </div>
        </div>
    );
}

// Legacy export alias kept so chats-pane / conversation-card can still import ChatBubble
// if they reference it. The rail view uses RailChatBubble directly.
export { RailChatBubble as ChatBubble };
