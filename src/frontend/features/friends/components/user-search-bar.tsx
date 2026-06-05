"use client";

import { useState, useDeferredValue } from "react";
import { Icon } from "@/shared/components/icon";
import { useUserSearch, useSendFriendRequest } from "@/features/friends/hooks/use-friends";
import { FriendshipButton } from "./FriendshipButton";

export function UserSearchBar() {
    const [searchInput, setSearchInput] = useState("");
    const deferredQuery = useDeferredValue(searchInput);
    const { data: searchResults, isLoading } = useUserSearch(deferredQuery);

    return (
        <div className="relative">
            <div className="relative">
                <Icon
                    name="search"
                    size="md"
                    className="absolute left-4 top-1/2 -translate-y-1/2 text-ink-4"
                />
                <input
                    type="text"
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                    placeholder="Найти пользователя..."
                    className="w-full pl-11 pr-4 py-3 rounded-xl bg-surface border border-line text-ink placeholder:text-ink-4 text-sm focus:outline-none focus:ring-2"
                    style={{ "--tw-ring-color": "var(--indigo)", boxShadow: "var(--sh-1)" } as React.CSSProperties}
                />
            </div>

            {deferredQuery.length >= 2 && (
                <div
                    className="absolute top-full left-0 right-0 mt-2 bg-surface border border-line rounded-xl overflow-hidden max-h-80 overflow-y-auto z-20"
                    style={{ boxShadow: "var(--sh-3)" }}
                >
                    {isLoading ? (
                        <div className="p-4 text-center">
                            <div className="w-6 h-6 rounded-full border-2 border-indigo border-t-transparent animate-spin mx-auto" />
                        </div>
                    ) : searchResults && searchResults.length > 0 ? (
                        <div className="divide-y divide-line">
                            {searchResults.map((result) => (
                                <div
                                    key={result.userId}
                                    className="flex items-center gap-3 px-4 py-3 hover:bg-bg-2 transition-colors"
                                >
                                    <GeoAvatar seed={result.displayName} size={36} />
                                    <div className="flex-1 min-w-0">
                                        <p className="font-medium text-ink text-sm truncate">
                                            {result.displayName}
                                        </p>
                                        {result.persona && (
                                            <p className="text-xs text-ink-4">
                                                {result.persona}
                                            </p>
                                        )}
                                    </div>
                                    <FriendshipButton
                                        userId={result.userId}
                                        friendshipStatus={result.friendshipStatus}
                                    />
                                </div>
                            ))}
                        </div>
                    ) : (
                        <p className="p-4 text-center text-sm text-ink-4">
                            Никого не найдено
                        </p>
                    )}
                </div>
            )}
        </div>
    );
}
