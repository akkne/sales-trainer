"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import type { ButtonSize, ButtonVariant } from "@/shared/components/button";
import {
    useSendFriendRequest,
    useAcceptFriendRequest,
    useCancelFriendRequest,
    useRemoveFriend,
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
    const cancelRequestMutation = useCancelFriendRequest();
    const removeFriendMutation = useRemoveFriend();
    const [hovered, setHovered] = useState(false);

    if (friendshipStatus === "friends") {
        return (
            <Button
                variant={hovered ? "destructive" : "secondary"}
                size={size}
                fullWidth={fullWidth}
                iconLeft={hovered ? "close" : "check"}
                loading={removeFriendMutation.isPending}
                onMouseEnter={() => setHovered(true)}
                onMouseLeave={() => setHovered(false)}
                onClick={() => removeFriendMutation.mutate(userId)}
            >
                {hovered ? "Удалить из друзей" : "Друзья"}
            </Button>
        );
    }

    if (friendshipStatus === "pending_outgoing") {
        if (!friendshipId) {
            return (
                <Button variant="secondary" size={size} fullWidth={fullWidth} disabled>
                    Заявка отправлена
                </Button>
            );
        }
        return (
            <Button
                variant={hovered ? "destructive" : "secondary"}
                size={size}
                fullWidth={fullWidth}
                iconLeft={hovered ? "close" : "check"}
                loading={cancelRequestMutation.isPending}
                onMouseEnter={() => setHovered(true)}
                onMouseLeave={() => setHovered(false)}
                onClick={() => cancelRequestMutation.mutate(friendshipId)}
            >
                {hovered ? "Отменить заявку" : "Заявка отправлена"}
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
