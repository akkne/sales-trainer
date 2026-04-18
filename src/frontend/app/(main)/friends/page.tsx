"use client";

import { Suspense, useRef, useCallback } from "react";
import { useRouter, useSearchParams, usePathname } from "next/navigation";
import { Icon } from "@/components/ui/Icon";
import { useFriends, useFriendRequests } from "@/lib/hooks/useFriends";
import { useCreateConversation } from "@/lib/hooks/useChat";
import { FriendCard } from "@/components/friends/FriendCard";
import { FriendRequestCard } from "@/components/friends/FriendRequestCard";
import { FriendLeaderboard } from "@/components/friends/FriendLeaderboard";
import { FriendActivityFeed } from "@/components/friends/FriendActivityFeed";
import { UserSearchBar } from "@/components/friends/UserSearchBar";
import { EmptyFriendsState } from "@/components/friends/EmptyFriendsState";
import { ChatsPane } from "@/components/friends/ChatsPane";

type TabKey = "friends" | "requests" | "leaderboard" | "chats";

const TABS: { key: TabKey; label: string; icon: string }[] = [
    { key: "friends", label: "Друзья", icon: "group" },
    { key: "requests", label: "Запросы", icon: "person_add" },
    { key: "leaderboard", label: "Рейтинг", icon: "leaderboard" },
    { key: "chats", label: "Чаты", icon: "chat" },
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
    const createConversationMutation = useCreateConversation();

    const incomingRequestCount =
        requests?.filter((request) => request.direction === "incoming").length ?? 0;

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
        <div className={`${activeTab === "chats" ? "max-w-5xl" : "max-w-2xl"} mx-auto px-4 py-6`}>
            <h1 className="font-headline font-bold text-2xl text-on-surface mb-5">
                Друзья
            </h1>

            {/* Tab bar */}
            <div className="flex gap-2 mb-6 flex-wrap">
                {TABS.map((tab) => (
                    <button
                        key={tab.key}
                        onClick={() => handleTabChange(tab.key)}
                        className={`relative flex items-center gap-1.5 px-4 py-2 rounded-full text-sm font-semibold tonal-transition ${
                            activeTab === tab.key
                                ? "bg-primary text-on-primary"
                                : "bg-surface-container text-on-surface-variant hover:bg-surface-container-high"
                        }`}
                    >
                        <Icon
                            name={tab.icon}
                            size="sm"
                            variant={activeTab === tab.key ? "filled" : "outlined"}
                        />
                        {tab.label}
                        {tab.key === "requests" && incomingRequestCount > 0 && (
                            <span className="min-w-5 h-5 flex items-center justify-center rounded-full bg-error text-on-error text-[10px] font-bold px-1">
                                {incomingRequestCount}
                            </span>
                        )}
                    </button>
                ))}
            </div>

            {/* Friends tab */}
            {activeTab === "friends" && (
                <div className="flex flex-col gap-5">
                    <div ref={searchInputRef}>
                        <UserSearchBar />
                    </div>

                    <FriendActivityFeed />

                    {friendsLoading ? (
                        <div className="flex flex-col gap-3">
                            {[1, 2, 3].map((index) => (
                                <div key={index} className="h-20 rounded-2xl bg-surface-container animate-pulse" />
                            ))}
                        </div>
                    ) : friends && friends.length > 0 ? (
                        <div className="flex flex-col gap-3">
                            <h3 className="font-semibold text-on-surface text-sm">
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
            )}

            {/* Requests tab */}
            {activeTab === "requests" && (
                <div className="flex flex-col gap-5">
                    {incomingRequests.length > 0 && (
                        <div>
                            <h3 className="font-semibold text-on-surface text-sm mb-3">
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
                            <h3 className="font-semibold text-on-surface text-sm mb-3">
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
                        <div className="bg-surface-container rounded-2xl px-5 py-8 text-center">
                            <div className="w-12 h-12 rounded-full bg-surface-container-high flex items-center justify-center mx-auto mb-3">
                                <Icon name="mail" size="lg" className="text-on-surface-variant" />
                            </div>
                            <p className="text-sm font-semibold text-on-surface-variant">
                                Нет запросов в друзья
                            </p>
                            <p className="text-xs text-on-surface-variant mt-1">
                                Все заявки обработаны
                            </p>
                        </div>
                    )}
                </div>
            )}

            {/* Leaderboard tab */}
            {activeTab === "leaderboard" && <FriendLeaderboard />}

            {/* Chats tab */}
            {activeTab === "chats" && (
                <ChatsPane
                    selectedConversationId={selectedConversationId}
                    onSelectConversation={handleSelectConversation}
                />
            )}
        </div>
    );
}
