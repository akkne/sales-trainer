"use client";

import { Suspense, useRef, useCallback } from "react";
import { useRouter, useSearchParams, usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";
import { StatTile } from "@/shared/components/stat-tile";
import { useFriends, useFriendRequests } from "@/features/friends/hooks/use-friends";
import { useCreateConversation, useConversations } from "@/features/friends/hooks/use-chat";
import { FriendCard } from "@/features/friends/components/friend-card";
import { FriendRequestCard } from "@/features/friends/components/friend-request-card";
import { FriendLeaderboard } from "@/features/friends/components/friend-leaderboard";
import { FriendActivityFeed } from "@/features/friends/components/friend-activity-feed";
import { UserSearchBar } from "@/features/friends/components/user-search-bar";
import { EmptyFriendsState } from "@/features/friends/components/empty-friends-state";
import { ChatsPane } from "@/features/friends/components/chats-pane";

type TabKey = "friends" | "requests" | "leaderboard" | "chats";

const TABS: { key: TabKey; label: string; icon: IconName }[] = [
    { key: "friends", label: "Друзья", icon: "users" },
    { key: "requests", label: "Запросы", icon: "user" },
    { key: "leaderboard", label: "Рейтинг", icon: "trophy" },
    { key: "chats", label: "Чаты", icon: "message" },
];

const VALID_TABS: readonly TabKey[] = ["friends", "requests", "leaderboard", "chats"];

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

    const tabParam = searchParams.get("tab");
    const activeTab: TabKey = VALID_TABS.includes(tabParam as TabKey)
        ? (tabParam as TabKey)
        : "friends";
    const selectedConversationId = searchParams.get("conv");

    const { data: friends, isLoading: friendsLoading } = useFriends();
    const { data: requests } = useFriendRequests();
    const { data: conversations } = useConversations();
    const createConversationMutation = useCreateConversation();

    const friendsCount = friends?.length ?? 0;
    const incomingRequestCount =
        requests?.filter((request) => request.direction === "incoming").length ?? 0;
    const conversationsCount = conversations?.length ?? 0;

    const updateSearchParams = useCallback(
        (updates: { tab?: TabKey; conv?: string | null }) => {
            const nextParams = new URLSearchParams(searchParams.toString());
            if (updates.tab !== undefined) nextParams.set("tab", updates.tab);
            if (updates.conv !== undefined) {
                if (updates.conv === null) nextParams.delete("conv");
                else nextParams.set("conv", updates.conv);
            }
            const query = nextParams.toString();
            router.replace(query ? `${pathname}?${query}` : pathname);
        },
        [pathname, router, searchParams],
    );

    function handleTabChange(tab: TabKey) {
        updateSearchParams({ tab, conv: tab === "chats" ? undefined : null });
    }

    function handleSelectConversation(conversationId: string | null) {
        updateSearchParams({ conv: conversationId });
    }

    function handleChatClick(friendUserId: string) {
        createConversationMutation.mutate(friendUserId, {
            onSuccess: (conversation) => {
                updateSearchParams({ tab: "chats", conv: conversation.conversationId });
            },
        });
    }

    function handleSearchFocus() {
        searchInputRef.current?.scrollIntoView({ behavior: "smooth" });
        const input = searchInputRef.current?.querySelector("input");
        input?.focus();
    }

    const incomingRequests = requests?.filter((request) => request.direction === "incoming") ?? [];
    const outgoingRequests = requests?.filter((request) => request.direction === "outgoing") ?? [];

    return (
        <div className="page">
            <div className="container">
                {/* Hero header */}
                <div className="hero-head">
                    <div className="hh-left fade-up">
                        <span className="eyebrow">
                            Сообщество<span className="dot">·</span>
                            <span>{friendsCount} {pluralFriends(friendsCount)}</span>
                        </span>
                        <h1 className="h1 hh-title">Друзья.</h1>
                        <p className="lead">
                            Твоё боевое окружение. Добавляй напарников, отслеживай активность
                            и соревнуйся за верхнюю строку в своём рейтинге.
                        </p>
                    </div>
                    <div className="hero-stats fade-up">
                        <StatTile
                            label="Друзей"
                            value={friendsCount}
                            icon={<Icon name="users" size="xs" />}
                            tone="primary"
                        />
                        <StatTile
                            label="Запросов"
                            value={incomingRequestCount}
                            icon={<Icon name="user" size="xs" />}
                            tone="flame"
                        />
                        <StatTile
                            label="Чатов"
                            value={conversationsCount}
                            icon={<Icon name="message" size="xs" />}
                            tone="violet"
                        />
                    </div>
                </div>

                {/* Tab bar */}
                <div className="tabbar">
                    {TABS.map((tab) => (
                        <button
                            key={tab.key}
                            onClick={() => handleTabChange(tab.key)}
                            className={`tab${activeTab === tab.key ? " active" : ""}`}
                        >
                            <Icon name={tab.icon} size={18} />
                            {tab.label}
                            {tab.key === "requests" && incomingRequestCount > 0 && (
                                <span
                                    className="num"
                                    style={{
                                        minWidth: 20,
                                        height: 20,
                                        display: "inline-flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        borderRadius: 999,
                                        fontSize: 10,
                                        fontWeight: 700,
                                        padding: "0 5px",
                                        background: "var(--flame)",
                                        color: "#fff",
                                    }}
                                >
                                    {incomingRequestCount}
                                </span>
                            )}
                        </button>
                    ))}
                </div>

                {/* Friends tab */}
                {activeTab === "friends" && (
                    <div className="friends-grid">
                        <div className="col gap-4" style={{ minWidth: 0 }}>
                            <div ref={searchInputRef}>
                                <UserSearchBar />
                            </div>

                            {friendsLoading ? (
                                <div className="col gap-3">
                                    {[1, 2, 3].map((index) => (
                                        <div key={index} className="card" style={{ height: 80 }} />
                                    ))}
                                </div>
                            ) : friends && friends.length > 0 ? (
                                <div className="col gap-2">
                                    <span className="eyebrow muted">Мои друзья · {friends.length}</span>
                                    {friends.map((friend) => (
                                        <FriendCard
                                            key={friend.userId}
                                            friend={friend}
                                            onChatClick={handleChatClick}
                                        />
                                    ))}
                                </div>
                            ) : (
                                <EmptyFriendsState onSearchFocus={handleSearchFocus} />
                            )}
                        </div>
                        <aside className="card card-pad activity" style={{ minWidth: 0 }}>
                            <FriendActivityFeed />
                        </aside>
                    </div>
                )}

                {/* Requests tab */}
                {activeTab === "requests" && (
                    <div className="col gap-5" style={{ maxWidth: 620 }}>
                        {incomingRequests.length > 0 && (
                            <div className="col gap-2">
                                <span className="eyebrow muted">Входящие · {incomingRequests.length}</span>
                                {incomingRequests.map((request) => (
                                    <FriendRequestCard key={request.friendshipId} request={request} />
                                ))}
                            </div>
                        )}

                        {outgoingRequests.length > 0 && (
                            <div className="col gap-2">
                                <span className="eyebrow muted">Исходящие · {outgoingRequests.length}</span>
                                {outgoingRequests.map((request) => (
                                    <FriendRequestCard key={request.friendshipId} request={request} />
                                ))}
                            </div>
                        )}

                        {incomingRequests.length === 0 && outgoingRequests.length === 0 && (
                            <div className="empty">
                                <div className="ic">
                                    <Icon name="send" size="lg" />
                                </div>
                                <p className="h4" style={{ marginBottom: 6 }}>Нет запросов в друзья</p>
                                <p className="small">Все заявки обработаны</p>
                            </div>
                        )}
                    </div>
                )}

                {/* Leaderboard tab */}
                {activeTab === "leaderboard" && (
                    <div style={{ maxWidth: 620 }}>
                        <FriendLeaderboard />
                    </div>
                )}

                {/* Chats tab */}
                {activeTab === "chats" && (
                    <ChatsPane
                        selectedConversationId={selectedConversationId}
                        onSelectConversation={handleSelectConversation}
                    />
                )}
            </div>
        </div>
    );
}

function pluralFriends(count: number): string {
    const mod10 = count % 10;
    const mod100 = count % 100;
    if (mod10 === 1 && mod100 !== 11) return "друг";
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "друга";
    return "друзей";
}
