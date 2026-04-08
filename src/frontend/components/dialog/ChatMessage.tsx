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
                <div className="w-8 h-8 rounded-full bg-secondary-container flex items-center justify-center mr-2 shrink-0 mt-1">
                    <Icon name="psychology" size="sm" className="text-secondary" />
                </div>
            )}
            <div
                className={`max-w-[80%] px-4 py-3 rounded-2xl ${
                    isAssistant
                        ? "bg-surface-container text-on-surface rounded-tl-sm"
                        : "bg-primary text-on-primary rounded-tr-sm"
                }`}
            >
                <p className="whitespace-pre-wrap text-sm leading-relaxed">{message.content}</p>
            </div>
            {!isAssistant && (
                <div className="w-8 h-8 rounded-full bg-primary-container flex items-center justify-center ml-2 shrink-0 mt-1">
                    <Icon name="person" size="sm" className="text-primary" />
                </div>
            )}
        </div>
    );
}
