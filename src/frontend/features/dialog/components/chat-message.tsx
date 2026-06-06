import { DialogMessage } from "@/features/dialog/hooks/use-dialog";
import { Icon } from "@/shared/components/icon";

interface ChatMessageProps {
    message: DialogMessage;
}

export function ChatMessage({ message }: ChatMessageProps) {
    const isAssistant = message.role === "assistant";

    return (
        <div className={`dc-msg ${isAssistant ? "ai" : "user"}`}>
            {isAssistant && (
                <span
                    className="itile primary"
                    style={{ width: 34, height: 34, borderRadius: "50%" }}
                >
                    <Icon name="sparkle" size="sm" />
                </span>
            )}
            <div className="dc-bubble">
                <p style={{ margin: 0, whiteSpace: "pre-wrap" }}>{message.content}</p>
            </div>
        </div>
    );
}
