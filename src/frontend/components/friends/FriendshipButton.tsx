"use client";

import { Button } from "@/components/ui/Button";
import {
    useSendFriendRequest,
    useAcceptFriendRequest,
} from "@/lib/hooks/useFriends";

interface FriendshipButtonProps {
    userId: string;
    friendshipStatus: "none" | "pending_outgoing" | "pending_incoming" | "friends";
    friendshipId?: string;
}

export function FriendshipButton({
    userId,
    friendshipStatus,
    friendshipId,
}: FriendshipButtonProps) {
    const sendRequestMutation = useSendFriendRequest();
    const acceptRequestMutation = useAcceptFriendRequest();

    if (friendshipStatus === "friends") {
        return (
            <Button variant="secondary" size="sm" disabled>
                Уже друзья
            </Button>
        );
    }

    if (friendshipStatus === "pending_outgoing") {
        return (
            <Button variant="secondary" size="sm" disabled>
                Запрос отправлен
            </Button>
        );
    }

    if (friendshipStatus === "pending_incoming" && friendshipId) {
        return (
            <Button
                variant="primary"
                size="sm"
                iconLeft="check"
                loading={acceptRequestMutation.isPending}
                onClick={() => acceptRequestMutation.mutate(friendshipId)}
            >
                Принять
            </Button>
        );
    }

    return (
        <Button
            variant="primary"
            size="sm"
            iconLeft="user"
            loading={sendRequestMutation.isPending}
            onClick={() => sendRequestMutation.mutate(userId)}
        >
            Добавить
        </Button>
    );
}
