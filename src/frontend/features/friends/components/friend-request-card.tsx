"use client";

import Link from "next/link";
import { Button } from "@/shared/components/button";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import {
    useAcceptFriendRequest,
    useDeclineFriendRequest,
    useCancelFriendRequest,
    type FriendRequest,
} from "@/features/friends/hooks/use-friends";

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
    const cancelMutation = useCancelFriendRequest();

    const isIncoming = request.direction === "incoming";

    return (
        <div className="card card-pad friend-row">
            <Link
                href={`/friends/${request.userId}`}
                style={{ flexShrink: 0 }}
                aria-label={request.displayName}
            >
                <GeoAvatar seed={request.displayName} size={48} />
            </Link>

            <div className="grow" style={{ minWidth: 0 }}>
                <Link
                    href={`/friends/${request.userId}`}
                    className="h4"
                    style={{
                        display: "block",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                        textDecoration: "none",
                        color: "inherit",
                    }}
                >
                    {request.displayName}
                </Link>
                <div className="small">
                    {isIncoming
                        ? PERSONA_LABELS[request.persona ?? ""] ?? request.persona ?? "хочет добавить тебя в друзья"
                        : "отправлен запрос в друзья"}
                </div>
            </div>

            {isIncoming ? (
                <div className="row gap-2" style={{ flexShrink: 0 }}>
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
                <div className="row gap-2 items-center" style={{ flexShrink: 0 }}>
                    <span className="chip">Ожидание…</span>
                    <Button
                        variant="ghost"
                        size="sm"
                        loading={cancelMutation.isPending}
                        onClick={() => cancelMutation.mutate(request.friendshipId)}
                    >
                        Отменить
                    </Button>
                </div>
            )}
        </div>
    );
}
