"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useFriendRequests } from "@/features/friends/hooks/use-friends";
import { NotificationBell } from "@/features/notifications/components/notification-bell";

interface RailItem {
    href: string;
    icon: IconName;
    label: string;
}

const RAIL_ITEMS: RailItem[] = [
    { href: "/tree",      icon: "compass", label: "Путь" },
    { href: "/dialog",    icon: "message", label: "Практика" },
    { href: "/guidebook", icon: "book",    label: "Справочник" },
    { href: "/friends",   icon: "users",   label: "Друзья" },
    { href: "/discuss",   icon: "forum",   label: "Обсуждения" },
];

function getInitials(name: string): string {
    return name
        .split(" ")
        .map((part) => part[0] ?? "")
        .join("")
        .toUpperCase()
        .slice(0, 2);
}

export function NavRail() {
    const pathname = usePathname();
    const { authenticatedUser } = useAuthStore();
    const { data: friendRequests } = useFriendRequests();

    const displayName = authenticatedUser?.displayName ?? "U";
    const initials = getInitials(displayName);

    const incomingRequestCount =
        friendRequests?.filter((r) => r.direction === "incoming").length ?? 0;

    const onProfile = pathname.startsWith("/profile");

    return (
        <aside className="rail" aria-label="Навигация">
            {/* Avatar → /profile */}
            <Link
                href="/profile"
                className="rail-avatar"
                data-active={onProfile}
                title="Профиль"
                aria-label="Профиль"
            >
                <span className="rail-avatar-inner">{initials}</span>
            </Link>

            {/* Divider */}
            <span className="rail-divider" aria-hidden="true" />

            {/* Main nav items */}
            {RAIL_ITEMS.map((item) => {
                const isActive = pathname.startsWith(item.href);
                const isFriends = item.href === "/friends";
                return (
                    <Link
                        key={item.href}
                        href={item.href}
                        className={`rail-item${isActive ? " active" : ""}`}
                        title={item.label}
                        aria-label={item.label}
                        aria-current={isActive ? "page" : undefined}
                    >
                        <Icon name={item.icon} size={20} />
                        {isFriends && incomingRequestCount > 0 && (
                            <span className="rail-badge" aria-label={`${incomingRequestCount} запросов в друзья`}>
                                {incomingRequestCount > 9 ? "9+" : incomingRequestCount}
                            </span>
                        )}
                    </Link>
                );
            })}

            {/* Spacer */}
            <span className="rail-spacer" aria-hidden="true" />

            {/* Notifications bell (preserved, above settings) */}
            <span className="rail-bell" title="Уведомления">
                <NotificationBell />
            </span>

            {/* Settings pinned at bottom */}
            <Link
                href="/settings"
                className={`rail-item${pathname.startsWith("/settings") ? " active" : ""}`}
                title="Настройки"
                aria-label="Настройки"
                aria-current={pathname.startsWith("/settings") ? "page" : undefined}
            >
                <Icon name="settings" size={20} />
            </Link>
        </aside>
    );
}
