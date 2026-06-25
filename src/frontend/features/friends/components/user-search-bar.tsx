"use client";

import { useState, useDeferredValue } from "react";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { useUserSearch } from "@/features/friends/hooks/use-friends";
import { FriendshipButton } from "./friendship-button";

/**
 * Standalone search input that renders inline (no wrapper div with relative
 * positioning — that is handled by .frd-search-wrap in the parent page).
 */
export function FriendSearchBar() {
    const [searchInput, setSearchInput] = useState("");
    const deferredQuery = useDeferredValue(searchInput);
    const { data: searchResults, isLoading } = useUserSearch(deferredQuery);

    const showDropdown = deferredQuery.length >= 2;

    return (
        <div style={{ position: "relative" }}>
            {/* Search field */}
            <span className="frd-search-ic">
                <Icon name="search" size={16} />
            </span>
            <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="Найти людей по имени…"
                className="frd-search"
                aria-label="Поиск пользователей"
                autoComplete="off"
            />

            {/* Results dropdown */}
            {showDropdown && (
                <div className="frd-search-results">
                    {isLoading ? (
                        <div
                            style={{
                                padding: 16,
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                            }}
                        >
                            <div
                                style={{
                                    width: 20,
                                    height: 20,
                                    borderRadius: "50%",
                                    border: "2px solid var(--primary)",
                                    borderTopColor: "transparent",
                                    animation: "spin 0.8s linear infinite",
                                }}
                            />
                        </div>
                    ) : searchResults && searchResults.length > 0 ? (
                        searchResults.map((result) => (
                            <div key={result.userId} className="frd-search-row">
                                <GeoAvatar seed={result.displayName} size={36} />
                                <div style={{ flex: 1, minWidth: 0 }}>
                                    <p
                                        style={{
                                            fontSize: 14,
                                            fontWeight: 700,
                                            color: "var(--ink-heading)",
                                            overflow: "hidden",
                                            textOverflow: "ellipsis",
                                            whiteSpace: "nowrap",
                                        }}
                                    >
                                        {result.displayName}
                                    </p>
                                    {result.persona && (
                                        <p style={{ fontSize: 12, color: "var(--ink-4)", marginTop: 1 }}>
                                            {result.persona}
                                        </p>
                                    )}
                                </div>
                                <FriendshipButton
                                    userId={result.userId}
                                    friendshipStatus={result.friendshipStatus}
                                    friendshipId={result.friendshipId ?? undefined}
                                />
                            </div>
                        ))
                    ) : (
                        <p className="frd-search-empty">Никого не найдено</p>
                    )}
                </div>
            )}
        </div>
    );
}
