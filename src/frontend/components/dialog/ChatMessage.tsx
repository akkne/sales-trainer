import { DialogMessage } from "@/lib/hooks/useDialog";

interface ChatMessageProps {
    message: DialogMessage;
}

export function ChatMessage({ message }: ChatMessageProps) {
    const isAssistant = message.role === "assistant";

    return (
        <div className={`flex ${isAssistant ? "justify-start" : "justify-end"}`}>
            <div
                className={`max-w-[80%] px-4 py-3 rounded-2xl ${
                    isAssistant
                        ? "bg-gray-100 text-gray-800 rounded-tl-sm"
                        : "bg-[#58CC02] text-white rounded-tr-sm"
                }`}
            >
                <p className="whitespace-pre-wrap">{message.content}</p>
            </div>
        </div>
    );
}
