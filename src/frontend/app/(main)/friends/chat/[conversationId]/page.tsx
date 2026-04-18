import { redirect } from "next/navigation";

interface ChatRedirectProps {
    params: Promise<{ conversationId: string }>;
}

export default async function ChatViewRedirect({ params }: ChatRedirectProps) {
    const { conversationId } = await params;
    redirect(`/friends?tab=chats&conv=${conversationId}`);
}
