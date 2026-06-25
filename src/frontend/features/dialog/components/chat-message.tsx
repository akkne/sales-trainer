import { DialogMessage } from "@/features/dialog/hooks/use-dialog";
import { Icon } from "@/shared/components/icon";

interface ChatMessageProps {
    message: DialogMessage;
}

function formatBubbleTime(iso: string): string {
    try {
        return new Date(iso).toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" });
    } catch {
        return "";
    }
}

export function ChatMessage({ message }: ChatMessageProps) {
    const isAssistant = message.role === "assistant";
    const timeLabel = message.timestamp ? formatBubbleTime(message.timestamp) : "";

    return (
        <div className={`dc-msg ${isAssistant ? "ai" : "user"}`}>
            {isAssistant && (
                <span
                    className="dc-avatar"
                    aria-hidden="true"
                >
                    <Icon name="sparkle" size="sm" />
                </span>
            )}
            <div className="dc-bubble-wrap">
                <div className="dc-bubble">
                    <p style={{ margin: 0, whiteSpace: "pre-wrap" }}>{message.content}</p>
                </div>
                {timeLabel && (
                    <span className="dc-ts" aria-label={`Отправлено в ${timeLabel}`}>{timeLabel}</span>
                )}
            </div>
        </div>
    );
}
