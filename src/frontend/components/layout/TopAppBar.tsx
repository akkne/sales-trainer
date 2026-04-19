"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon, IconName } from "@/components/ui/Icon";
import { Wordmark } from "@/components/ui/Wordmark";
import { GeoAvatar } from "@/components/ui/GeoAvatar";
import { NotificationBell } from "@/components/notifications/NotificationBell";
import { useAuthStore } from "@/lib/store/authStore";
import { useSkillTree } from "@/lib/hooks/useSkillTree";
import { useFriendRequests } from "@/lib/hooks/useFriends";

interface NavItem {
    href: string;
    icon: IconName;
    label: string;
}

const NAV_ITEMS: NavItem[] = [
    { href: "/tree", icon: "compass", label: "Путь" },
    { href: "/league", icon: "trophy", label: "Лига" },
    { href: "/guidebook", icon: "book", label: "Справочник" },
    { href: "/dialog", icon: "message", label: "Диалог" },
    { href: "/friends", icon: "users", label: "Друзья" },
    { href: "/profile", icon: "user", label: "Профиль" },
];

export function TopAppBar() {
    const currentPathname = usePathname();
    const { authenticatedUser } = useAuthStore();
    const { data: skillTreeData } = useSkillTree();
    const { data: friendRequests } = useFriendRequests();

    const incomingRequestCount = friendRequests?.filter(
        (request) => request.direction === "incoming"
    ).length ?? 0;

    const displayName = authenticatedUser?.displayName ?? "User";
    const level = Math.floor((skillTreeData?.totalXpAmount ?? 0) / 1000) + 1;
    const streak = skillTreeData?.currentStreakDayCount ?? 0;

    return (
        <header
            style={{
                height: 60,
                background: "var(--surface)",
                borderBottom: "1px solid var(--line)",
                display: "flex",
                alignItems: "center",
                padding: "0 32px",
                gap: 32,
                position: "sticky",
                top: 0,
                zIndex: 20,
            }}
            className="hidden md:flex"
        >
            {/* Logo */}
            <Link href="/tree" style={{ cursor: "pointer" }}>
                <Wordmark size={22} />
            </Link>

            {/* Nav items */}
            <nav style={{ display: "flex", gap: 4, alignItems: "center" }}>
                {NAV_ITEMS.map((item) => {
                    const isActive = currentPathname.startsWith(item.href);
                    return (
                        <Link
                            key={item.href}
                            href={item.href}
                            style={{
                                position: "relative",
                                display: "flex",
                                alignItems: "center",
                                gap: 8,
                                padding: "8px 14px",
                                background: isActive ? "var(--bg-2)" : "transparent",
                                color: isActive ? "var(--ink)" : "var(--ink-3)",
                                border: "none",
                                borderRadius: 10,
                                fontSize: 14,
                                fontWeight: 500,
                                textDecoration: "none",
                                fontFamily: "var(--f-sans)",
                                letterSpacing: "-0.1px",
                                transition: "background 0.15s, color 0.15s",
                            }}
                        >
                            <Icon name={item.icon} size="sm" />
                            {item.label}
                            {item.href === "/friends" && incomingRequestCount > 0 && (
                                <span
                                    style={{
                                        position: "absolute",
                                        top: 4,
                                        right: 4,
                                        minWidth: 16,
                                        height: 16,
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        borderRadius: 999,
                                        background: "var(--bad)",
                                        color: "white",
                                        fontSize: 10,
                                        fontWeight: 600,
                                        padding: "0 4px",
                                    }}
                                >
                                    {incomingRequestCount}
                                </span>
                            )}
                        </Link>
                    );
                })}
            </nav>

            <div style={{ flex: 1 }} />

            {/* Right side */}
            <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
                {/* Streak */}
                {streak > 0 && (
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 6,
                            padding: "6px 12px",
                            borderRadius: 999,
                            background: "var(--rust-soft)",
                            color: "var(--rust-ink)",
                            fontSize: 13,
                            fontWeight: 500,
                        }}
                    >
                        <Icon name="flame" size="sm" />
                        <span className="tnum">{streak}</span>
                    </div>
                )}

                {/* Notifications */}
                <NotificationBell />

                {/* Profile chip */}
                <Link
                    href="/profile"
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                        padding: "4px 10px 4px 4px",
                        borderRadius: 999,
                        background: "var(--bg-2)",
                        textDecoration: "none",
                        color: "var(--ink)",
                    }}
                    aria-label={`Профиль (${displayName})`}
                >
                    <GeoAvatar seed={displayName} size={28} />
                    <div style={{ fontSize: 12, lineHeight: 1.2 }}>
                        <div
                            style={{
                                color: "var(--ink-3)",
                                fontSize: 10,
                                textTransform: "uppercase",
                                letterSpacing: 0.5,
                            }}
                        >
                            Уровень
                        </div>
                        <div style={{ fontWeight: 600, fontFamily: "var(--f-mono)" }}>
                            {level}
                        </div>
                    </div>
                </Link>
            </div>
        </header>
    );
}
