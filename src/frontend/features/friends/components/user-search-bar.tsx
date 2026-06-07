"use client";

import { useState, useDeferredValue } from "react";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { useUserSearch, useSendFriendRequest } from "@/features/friends/hooks/use-friends";
import { FriendshipButton } from "./friendship-button";

export function UserSearchBar() {
    const [searchInput, setSearchInput] = useState("");
    const deferredQuery = useDeferredValue(searchInput);
    const { data: searchResults, isLoading } = useUserSearch(deferredQuery);

    return (
        <div style={{ position: "relative" }}>
            <div className="field-wrap has-ic">
                <Icon name="search" className="lead-ic" />
                <input
                    type="text"
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                    placeholder="Найти пользователя…"
                    className="field"
                />
            </div>

            {deferredQuery.length >= 2 && (
                <div
                    className="card"
                    style={{
                        position: "absolute",
                        top: "100%",
                        left: 0,
                        right: 0,
                        marginTop: 8,
                        overflow: "hidden",
                        maxHeight: 320,
                        overflowY: "auto",
                        zIndex: 20,
                        boxShadow: "var(--sh-3)",
                    }}
                >
                    {isLoading ? (
                        <div style={{ padding: 16, textAlign: "center" }}>
                            <div
                                style={{
                                    width: 24,
                                    height: 24,
                                    borderRadius: "50%",
                                    border: "2px solid var(--primary)",
                                    borderTopColor: "transparent",
                                    margin: "0 auto",
                                    animation: "spin 0.8s linear infinite",
                                }}
                            />
                        </div>
                    ) : searchResults && searchResults.length > 0 ? (
                        <div>
                            {searchResults.map((result) => (
                                <div
                                    key={result.userId}
                                    className="row gap-3"
                                    style={{ padding: "12px 16px", borderTop: "1px solid var(--line)" }}
                                >
                                    <GeoAvatar seed={result.displayName} size={36} />
                                    <div className="grow" style={{ minWidth: 0 }}>
                                        <p
                                            className="h4"
                                            style={{
                                                fontSize: 14,
                                                overflow: "hidden",
                                                textOverflow: "ellipsis",
                                                whiteSpace: "nowrap",
                                            }}
                                        >
                                            {result.displayName}
                                        </p>
                                        {result.persona && (
                                            <p className="small" style={{ color: "var(--ink-4)" }}>
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
                        <p className="small" style={{ padding: 16, textAlign: "center", color: "var(--ink-4)" }}>
                            Никого не найдено
                        </p>
                    )}
                </div>
            )}
        </div>
    );
}
