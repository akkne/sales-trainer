"use client";

import { Suspense, useRef, useCallback } from "react";
import { useRouter, useSearchParams, usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { useFriends, useFriendRequests } from "@/features/friends/hooks/use-friends";
import { useCreateConversation, useConversations } from "@/features/friends/hooks/use-chat";
import { FriendCard } from "@/features/friends/components/friend-card";
import { FriendRequestCard } from "@/features/friends/components/friend-request-card";
import { FriendActivityFeed } from "@/features/friends/components/friend-activity-feed";
import { FriendSearchBar } from "@/features/friends/components/user-search-bar";
import { RailChatView } from "@/features/friends/components/chat-window";

// FriendLeaderboard import is kept (component file not deleted) — just not rendered in this hub.
// import { FriendLeaderboard } from "@/features/friends/components/friend-leaderboard";

export default function FriendsPage() {
    return (
        <Suspense fallback={null}>
            <FriendsPageContent />
        </Suspense>
    );
}

function FriendsPageContent() {
    const searchParams = useSearchParams();
    const pathname = usePathname();
    const router = useRouter();
    const searchInputRef = useRef<HTMLDivElement>(null);

    // Rail chat: derive open conversation from URL param so back/forward works automatically
    const railConvId = searchParams.get("conv") ?? null;

    const { data: friends, isLoading: friendsLoading } = useFriends();
    const { data: requests } = useFriendRequests();
    const { data: conversations } = useConversations();
    const createConversationMutation = useCreateConversation();

    const incomingRequests = requests?.filter((r) => r.direction === "incoming") ?? [];
    const outgoingRequests = requests?.filter((r) => r.direction === "outgoing") ?? [];
    const incomingRequestCount = incomingRequests.length;

    // Update URL to persist rail conv selection (for deep links)
    const syncConvParam = useCallback(
        (convId: string | null) => {
            const nextParams = new URLSearchParams(searchParams.toString());
            if (convId) nextParams.set("conv", convId);
            else nextParams.delete("conv");
            const query = nextParams.toString();
            router.replace(query ? `${pathname}?${query}` : pathname);
        },
        [pathname, router, searchParams],
    );

    function handleChatClick(friendUserId: string) {
        // First check if conversation already exists
        const existing = conversations?.find((c) => c.friendUserId === friendUserId);
        if (existing) {
            syncConvParam(existing.conversationId);
            return;
        }
        createConversationMutation.mutate(friendUserId, {
            onSuccess: (conversation) => {
                syncConvParam(conversation.conversationId);
            },
        });
    }

    function handleCloseChat() {
        syncConvParam(null);
    }

    return (
        <div className="frd-screen">
            {/* ── Header ── */}
            <div className="frd-header">
                <h1 className="frd-title">Друзья</h1>
                <div className="frd-search-wrap" ref={searchInputRef}>
                    <FriendSearchBar />
                </div>
            </div>

            {/* ── Two-column body ── */}
            <div className="frd-body">
                {/* ── Main scroll column ── */}
                <div className="frd-main">
                    {/* Requests section */}
                    {(incomingRequests.length > 0 || outgoingRequests.length > 0) && (
                        <div>
                            <p className="frd-section-label">
                                Заявки
                                {incomingRequestCount > 0 && (
                                    <span
                                        style={{
                                            marginLeft: 6,
                                            background: "var(--primary)",
                                            color: "#fff",
                                            borderRadius: 5,
                                            fontSize: 10,
                                            fontWeight: 700,
                                            padding: "1px 6px",
                                        }}
                                    >
                                        {incomingRequestCount}
                                    </span>
                                )}
                            </p>

                            {incomingRequests.map((request) => (
                                <FriendRequestCard key={request.friendshipId} request={request} />
                            ))}

                            {outgoingRequests.length > 0 && (
                                <>
                                    <p className="frd-section-label" style={{ marginTop: 16 }}>
                                        Исходящие
                                    </p>
                                    {outgoingRequests.map((request) => (
                                        <FriendRequestCard key={request.friendshipId} request={request} />
                                    ))}
                                </>
                            )}
                        </div>
                    )}

                    {/* All friends section */}
                    <p className="frd-section-label">Все друзья</p>

                    {friendsLoading ? (
                        <div className="frd-grid">
                            {[1, 2, 3, 4].map((i) => (
                                <div key={i} className="frd-skeleton" style={{ height: 100, borderRadius: 14 }} />
                            ))}
                        </div>
                    ) : friends && friends.length > 0 ? (
                        <div className="frd-grid">
                            {friends.map((friend) => (
                                <FriendCard
                                    key={friend.userId}
                                    friend={friend}
                                    onChatClick={handleChatClick}
                                />
                            ))}
                        </div>
                    ) : (
                        <div className="frd-empty">
                            <div className="frd-empty-icon">
                                <Icon name="users" size={20} />
                            </div>
                            <p className="frd-empty-title">Найди своего первого напарника!</p>
                            <p className="frd-empty-sub">
                                Используй поиск выше, чтобы найти коллег
                            </p>
                        </div>
                    )}
                </div>

                {/* ── Right rail: activity feed or chat ── */}
                <aside className="frd-rail">
                    {railConvId ? (
                        <RailChatView
                            conversationId={railConvId}
                            onBack={handleCloseChat}
                        />
                    ) : (
                        <FriendActivityFeed />
                    )}
                </aside>
            </div>
        </div>
    );
}
