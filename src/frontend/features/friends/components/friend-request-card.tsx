"use client";

import Link from "next/link";
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

    const subLabel = isIncoming
        ? (PERSONA_LABELS[request.persona ?? ""] ?? request.persona ?? "wants to add you as a friend")
        : "friend request sent";

    return (
        <div className="frd-req-card">
            {/* Gradient avatar */}
            <Link
                href={`/friends/${request.userId}`}
                style={{ flexShrink: 0 }}
                aria-label={request.displayName}
            >
                <GeoAvatar seed={request.displayName} size={44} />
            </Link>

            {/* Name + sub label */}
            <div className="frd-req-meta">
                <Link
                    href={`/friends/${request.userId}`}
                    style={{ textDecoration: "none", color: "inherit" }}
                >
                    <p className="frd-req-name">{request.displayName}</p>
                </Link>
                <p className="frd-req-sub">{subLabel}</p>
            </div>

            {/* Actions */}
            <div className="frd-req-actions">
                {isIncoming ? (
                    <>
                        <button
                            className="frd-req-accept"
                            disabled={acceptMutation.isPending}
                            onClick={() => acceptMutation.mutate(request.friendshipId)}
                            aria-label="Accept request"
                        >
                            {acceptMutation.isPending ? "…" : "Accept"}
                        </button>
                        <button
                            className="frd-req-decline"
                            disabled={declineMutation.isPending}
                            onClick={() => declineMutation.mutate(request.friendshipId)}
                            aria-label="Decline request"
                        >
                            {declineMutation.isPending ? "…" : "Decline"}
                        </button>
                    </>
                ) : (
                    <>
                        <span className="frd-req-pending-chip">Pending…</span>
                        <button
                            className="frd-req-decline"
                            disabled={cancelMutation.isPending}
                            onClick={() => cancelMutation.mutate(request.friendshipId)}
                            aria-label="Cancel request"
                        >
                            {cancelMutation.isPending ? "…" : "Cancel"}
                        </button>
                    </>
                )}
            </div>
        </div>
    );
}
