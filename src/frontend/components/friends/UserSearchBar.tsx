"use client";

import { useState, useDeferredValue } from "react";
import { Icon } from "@/components/ui/Icon";
import { useUserSearch, useSendFriendRequest } from "@/lib/hooks/useFriends";
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
                    className="absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant"
                />
                <input
                    type="text"
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                    placeholder="Найти пользователя..."
                    className="w-full pl-10 pr-4 py-3 rounded-2xl bg-surface-container text-on-surface placeholder:text-on-surface-variant text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                />
            </div>

            {deferredQuery.length >= 2 && (
                <div className="absolute top-full left-0 right-0 mt-2 bg-surface-container-low rounded-2xl shadow-lg z-20 overflow-hidden max-h-80 overflow-y-auto">
                    {isLoading ? (
                        <div className="p-4 text-center">
                            <div className="w-6 h-6 rounded-full border-2 border-primary border-t-transparent animate-spin mx-auto" />
                        </div>
                    ) : searchResults && searchResults.length > 0 ? (
                        <div className="divide-y divide-outline-variant">
                            {searchResults.map((result) => (
                                <div
                                    key={result.userId}
                                    className="flex items-center gap-3 px-4 py-3"
                                >
                                    <div className="w-9 h-9 rounded-full bg-secondary-container flex items-center justify-center text-secondary font-bold text-sm shrink-0">
                                        {result.displayName[0]?.toUpperCase()}
                                    </div>
                                    <div className="flex-1 min-w-0">
                                        <p className="font-semibold text-on-surface text-sm truncate">
                                            {result.displayName}
                                        </p>
                                        {result.persona && (
                                            <p className="text-xs text-on-surface-variant">
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
                        <p className="p-4 text-center text-sm text-on-surface-variant">
                            Никого не найдено
                        </p>
                    )}
                </div>
            )}
        </div>
    );
}
