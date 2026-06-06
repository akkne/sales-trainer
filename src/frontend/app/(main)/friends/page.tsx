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
        <div style={{ minHeight: "100vh", background: "var(--bg)" }} className="pb-20">
            {/* Hero band — handbook style */}
            <div
                style={{
                    padding: "40px 60px 32px",
                    borderBottom: "1px solid var(--line)",
                    background: "var(--surface-2)",
                }}
            >
                <div
                    style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "flex-end",
                        gap: 32,
                        maxWidth: 1200,
                        margin: "0 auto",
                        flexWrap: "wrap",
                    }}
                >
                    <div style={{ minWidth: 0 }}>
                        <div
                            style={{
                                fontSize: 12,
                                color: "var(--indigo)",
                                letterSpacing: 2,
                                textTransform: "uppercase",
                                fontWeight: 500,
                                marginBottom: 10,
                                fontFamily: "var(--f-mono)",
                            }}
                        >
                            СООБЩЕСТВО · {friendsCount} ДРУЗ{plural(friendsCount)}
                        </div>
                        <h1 style={{ margin: 0, fontSize: 48, letterSpacing: -1.5, fontWeight: 500, lineHeight: 1, color: "var(--ink)" }}>
                            Друзья.
                        </h1>
                        <p style={{ fontSize: 16, color: "var(--ink-3)", marginTop: 10, maxWidth: 520 }}>
                            Твоё боевое окружение. Добавляй напарников, отслеживай активность
                            и соревнуйся за верхнюю строку в своём рейтинге.
                        </p>
                    </div>
                    <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                        <StatTile
                            big
                            label="Друзей"
                            value={friendsCount}
                            icon={<Icon name="users" size="xs" />}
                            tone="olive"
                        />
                        <StatTile
                            big
                            label="Запросов"
                            value={incomingRequestCount}
                            icon={<Icon name="user" size="xs" />}
                            tone="rust"
                        />
                        <StatTile
                            big
                            label="Чатов"
                            value={conversationsCount}
                            icon={<Icon name="message" size="xs" />}
                            tone="indigo"
                        />
                    </div>
                </div>
            </div>

            {/* Tab bar + content */}
            <div style={{ padding: "24px 60px 0", maxWidth: 1200, margin: "0 auto" }}>
                <div className="flex gap-2 mb-6 flex-wrap">
                    {TABS.map((tab) => (
                        <button
                            key={tab.key}
                            onClick={() => handleTabChange(tab.key)}
                            className={`relative flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium transition-all ${
                                activeTab === tab.key
                                    ? "bg-ink text-bg"
                                    : "bg-surface border border-line text-ink-3 hover:text-ink hover:border-line-2"
                            }`}
                            style={activeTab === tab.key ? { boxShadow: "var(--sh-2)" } : { boxShadow: "var(--sh-1)" }}
                        >
                            <Icon name={tab.icon} size="sm" />
                            {tab.label}
                            {tab.key === "requests" && incomingRequestCount > 0 && (
                                <span
                                    className="min-w-5 h-5 flex items-center justify-center rounded-full text-[10px] font-bold px-1"
                                    style={{ background: "var(--bad)", color: "white" }}
                                >
                                    {incomingRequestCount}
                                </span>
                            )}
                        </button>
                    ))}
                </div>

                {/* Friends tab */}
                {activeTab === "friends" && (
                    <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
                        <div className="flex flex-col gap-5 min-w-0">
                            <div ref={searchInputRef}>
                                <UserSearchBar />
                            </div>

                            {friendsLoading ? (
                                <div className="flex flex-col gap-3">
                                    {[1, 2, 3].map((index) => (
                                        <div key={index} className="h-20 rounded-2xl bg-surface animate-pulse" />
                                    ))}
                                </div>
                            ) : friends && friends.length > 0 ? (
                                <div className="flex flex-col gap-2">
                                    <h3 className="text-xs font-mono tracking-[1px] uppercase text-ink-4 mb-2">
                                        Мои друзья ({friends.length})
                                    </h3>
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
                        <aside className="min-w-0">
                            <FriendActivityFeed />
                        </aside>
                    </div>
                )}

                {/* Requests tab */}
                {activeTab === "requests" && (
                    <div className="flex flex-col gap-5 max-w-2xl">
                        {incomingRequests.length > 0 && (
                            <div>
                                <h3 className="text-xs font-mono tracking-[1px] uppercase text-ink-4 mb-3">
                                    Входящие ({incomingRequests.length})
                                </h3>
                                <div className="flex flex-col gap-2">
                                    {incomingRequests.map((request) => (
                                        <FriendRequestCard key={request.friendshipId} request={request} />
                                    ))}
                                </div>
                            </div>
                        )}

                        {outgoingRequests.length > 0 && (
                            <div>
                                <h3 className="text-xs font-mono tracking-[1px] uppercase text-ink-4 mb-3">
                                    Исходящие ({outgoingRequests.length})
                                </h3>
                                <div className="flex flex-col gap-2">
                                    {outgoingRequests.map((request) => (
                                        <FriendRequestCard key={request.friendshipId} request={request} />
                                    ))}
                                </div>
                            </div>
                        )}

                        {incomingRequests.length === 0 && outgoingRequests.length === 0 && (
                            <div
                                className="bg-surface border border-line rounded-2xl px-5 py-8 text-left"
                                style={{ boxShadow: "var(--sh-1)" }}
                            >
                                <div className="w-12 h-12 rounded-xl bg-bg-2 flex items-center justify-center mb-3">
                                    <Icon name="send" size="lg" className="text-ink-4" />
                                </div>
                                <p className="text-sm font-medium text-ink">
                                    Нет запросов в друзья
                                </p>
                                <p className="text-xs text-ink-4 mt-1">
                                    Все заявки обработаны
                                </p>
                            </div>
                        )}
                    </div>
                )}

                {/* Leaderboard tab */}
                {activeTab === "leaderboard" && (
                    <div className="max-w-2xl">
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

function plural(count: number): string {
    const mod10 = count % 10;
    const mod100 = count % 100;
    if (mod10 === 1 && mod100 !== 11) return "Ь";
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "Я";
    return "ЕЙ";
}
