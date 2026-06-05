"use client";

import { Button } from "@/shared/components/button";
import {
    useSendFriendRequest,
    useAcceptFriendRequest,
} from "@/features/friends/hooks/use-friends";

interface FriendshipButtonProps {
    userId: string;
    friendshipStatus: "none" | "pending_outgoing" | "pending_incoming" | "friends";
    friendshipId?: string;
    size?: ButtonSize;
    primaryVariant?: ButtonVariant;
    fullWidth?: boolean;
}

export function FriendshipButton({
    userId,
    friendshipStatus,
    friendshipId,
    size = "sm",
    primaryVariant = "primary",
    fullWidth = false,
}: FriendshipButtonProps) {
    const sendRequestMutation = useSendFriendRequest();
    const acceptRequestMutation = useAcceptFriendRequest();

    if (friendshipStatus === "friends") {
        return (
            <Button variant="secondary" size={size} fullWidth={fullWidth} disabled>
                Уже друзья
            </Button>
        );
    }

    if (friendshipStatus === "pending_outgoing") {
        return (
            <Button variant="secondary" size={size} fullWidth={fullWidth} disabled>
                Запрос отправлен
            </Button>
        );
    }

    if (friendshipStatus === "pending_incoming" && friendshipId) {
        return (
            <Button
                variant={primaryVariant}
                size={size}
                fullWidth={fullWidth}
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
            variant={primaryVariant}
            size={size}
            fullWidth={fullWidth}
            iconLeft="user"
            loading={sendRequestMutation.isPending}
            onClick={() => sendRequestMutation.mutate(userId)}
        >
            Добавить
        </Button>
    );
}
