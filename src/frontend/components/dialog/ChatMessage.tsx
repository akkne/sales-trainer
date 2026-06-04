import { DialogMessage } from "@/lib/hooks/useDialog";
import { Icon } from "@/components/ui/Icon";

interface ChatMessageProps {
    message: DialogMessage;
}

export function ChatMessage({ message }: ChatMessageProps) {
    const isAssistant = message.role === "assistant";

    return (
        <div className={`flex ${isAssistant ? "justify-start" : "justify-end"}`}>
            {isAssistant && (
                <div className="w-8 h-8 rounded-full bg-indigo-soft flex items-center justify-center mr-2 shrink-0 mt-1">
                    <Icon name="sparkle" size="sm" className="text-indigo" />
                </div>
            )}
            <div
                className={`max-w-[80%] px-4 py-3 rounded-2xl ${
                    isAssistant
                        ? "bg-surface border border-line text-ink rounded-tl-sm"
                        : "bg-ink text-bg rounded-tr-sm"
                }`}
                style={{ boxShadow: "var(--sh-1)" }}
            >
                <p className="whitespace-pre-wrap text-sm leading-relaxed">{message.content}</p>
            </div>
            {!isAssistant && (
                <div className="w-8 h-8 rounded-full bg-rust-soft flex items-center justify-center ml-2 shrink-0 mt-1">
                    <Icon name="user" size="sm" className="text-rust" />
                </div>
            )}
        </div>
    );
}
