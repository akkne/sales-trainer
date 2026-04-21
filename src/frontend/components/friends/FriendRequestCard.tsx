"use client";

import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { GeoAvatar } from "@/components/ui/GeoAvatar";
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
        <div
            className="bg-surface border border-line rounded-2xl p-4 flex items-center gap-4"
            style={{ boxShadow: "var(--sh-1)" }}
        >
            <Link
                href={`/friends/${request.userId}`}
                className="shrink-0"
                aria-label={request.displayName}
            >
                <GeoAvatar seed={request.displayName} size={40} />
            </Link>

            <div className="flex-1 min-w-0">
                <Link
                    href={`/friends/${request.userId}`}
                    className="font-medium text-ink truncate block text-sm"
                >
                    {request.displayName}
                </Link>
                {request.persona && (
                    <p className="text-xs text-ink-4">
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
                <span
                    className="text-xs font-mono shrink-0 px-3 py-1.5 rounded-full"
                    style={{ background: "var(--bg-2)", color: "var(--ink-4)" }}
                >
                    Ожидание...
                </span>
            )}
        </div>
    );
}
