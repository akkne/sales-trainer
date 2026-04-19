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
        <div
            className="bg-surface border border-line rounded-2xl p-4 flex items-center gap-4"
            style={{ boxShadow: "var(--sh-1)" }}
        >
            <Link
                href={`/friends/${request.userId}`}
                className="w-10 h-10 rounded-xl flex items-center justify-center font-medium text-sm shrink-0"
                style={{ background: "var(--clay)", color: "white" }}
            >
                {request.displayName[0]?.toUpperCase()}
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
                    <button
                        onClick={() => acceptMutation.mutate(request.friendshipId)}
                        disabled={acceptMutation.isPending}
                        className="px-4 py-2 rounded-xl text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-50"
                        style={{ background: "var(--olive)", boxShadow: "var(--sh-1)" }}
                    >
                        Принять
                    </button>
                    <button
                        onClick={() => declineMutation.mutate(request.friendshipId)}
                        disabled={declineMutation.isPending}
                        className="px-4 py-2 rounded-xl text-sm font-medium text-ink-3 transition-colors hover:text-ink hover:bg-bg-2 disabled:opacity-50"
                    >
                        Отклонить
                    </button>
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
