"use client";

import Link from "next/link";
import { Button } from "@/components/ui/Button";
import {
    useAcceptFriendRequest,
    useDeclineFriendRequest,
    type FriendRequest,
} from "@/lib/hooks/useFriends";

const PERSONA_LABELS: Record<string, string> = {
    sdr: "SDR",
    account_executive: "Account Executive",
    account_manager: "Account Manager",
    founder: "Founder",
    other: "Other",
};

interface FriendRequestCardProps {
    request: FriendRequest;
}

export function FriendRequestCard({ request }: FriendRequestCardProps) {
    const acceptMutation = useAcceptFriendRequest();
    const declineMutation = useDeclineFriendRequest();

    const isIncoming = request.direction === "incoming";

    return (
        <div className="bg-surface-container rounded-2xl p-4 flex items-center gap-4">
            <Link
                href={`/friends/${request.userId}`}
                className="w-10 h-10 rounded-full bg-secondary-container flex items-center justify-center text-secondary font-bold text-sm shrink-0"
            >
                {request.displayName[0]?.toUpperCase()}
            </Link>

            <div className="flex-1 min-w-0">
                <Link
                    href={`/friends/${request.userId}`}
                    className="font-semibold text-on-surface truncate block"
                >
                    {request.displayName}
                </Link>
                {request.persona && (
                    <p className="text-xs text-on-surface-variant">
                        {PERSONA_LABELS[request.persona] ?? request.persona}
                    </p>
                )}
            </div>

            {isIncoming ? (
                <div className="flex items-center gap-2 shrink-0">
                    <Button
                        variant="primary"
                        size="sm"
                        loading={acceptMutation.isPending}
                        onClick={() => acceptMutation.mutate(request.friendshipId)}
                    >
                        Принять
                    </Button>
                    <Button
                        variant="ghost"
                        size="sm"
                        loading={declineMutation.isPending}
                        onClick={() => declineMutation.mutate(request.friendshipId)}
                    >
                        Отклонить
                    </Button>
                </div>
            ) : (
                <span className="text-xs text-on-surface-variant font-medium shrink-0">
                    Ожидание...
                </span>
            )}
        </div>
    );
}
