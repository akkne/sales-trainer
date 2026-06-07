"use client";

import Link from "next/link";
import { useState } from "react";
import { usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { NotificationBell } from "@/features/notifications/components/notification-bell";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useSkillTree } from "@/features/skills/hooks/use-skill-tree";
import { useFriendRequests } from "@/features/friends/hooks/use-friends";

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
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

    const incomingRequestCount = friendRequests?.filter(
        (request) => request.direction === "incoming"
    ).length ?? 0;

    const displayName = authenticatedUser?.displayName ?? "User";
    const level = Math.floor((skillTreeData?.totalXpAmount ?? 0) / 1000) + 1;
    const streak = skillTreeData?.currentStreakDayCount ?? 0;

    return (
        <>
            <header className="appbar">
                <div className="appbar-inner">
                    {/* Logo */}
                    <Link href="/tree" className="wordmark">
                        <span className="mark">
                            <Icon name="bolt" size="sm" />
                        </span>
                        <span>
                            Sellevate<span className="dotmark">.</span>
                        </span>
                    </Link>

                    {/* Desktop nav items */}
                    <nav className="hidden md:flex" style={{ gap: 2, alignItems: "center" }}>
                        {NAV_ITEMS.map((item) => {
                            const isActive = currentPathname.startsWith(item.href);
                            return (
                                <Link
                                    key={item.href}
                                    href={item.href}
                                    className={`nav-pill${isActive ? " active" : ""}`}
                                    style={{ position: "relative" }}
                                >
                                    <Icon name={item.icon} size="sm" />
                                    {item.label}
                                    {item.href === "/friends" && incomingRequestCount > 0 && (
                                        <span
                                            style={{
                                                position: "absolute",
                                                top: 2,
                                                right: 2,
                                                minWidth: 16,
                                                height: 16,
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                borderRadius: 999,
                                                background: "var(--heart)",
                                                color: "white",
                                                fontSize: 10,
                                                fontWeight: 700,
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
                    <div style={{ display: "flex", alignItems: "center", gap: 12, flex: "none" }}>
                        {/* Streak */}
                        {streak > 0 && (
                            <div className="streak-pill hidden md:inline-flex">
                                <Icon name="flame" size="sm" />
                                <span className="tnum">{streak}</span>
                            </div>
                        )}

                        {/* Notifications */}
                        <NotificationBell />

                        {/* Profile chip - desktop */}
                        <Link
                            href="/profile"
                            className="profile-chip hidden md:inline-flex"
                            aria-label={`Профиль (${displayName})`}
                        >
                            <GeoAvatar seed={displayName} size={34} />
                            <div style={{ fontSize: 11, fontWeight: 700, lineHeight: 1.25 }}>
                                <div
                                    style={{
                                        fontFamily: "var(--font-mono)",
                                        fontSize: 9,
                                        letterSpacing: "0.1em",
                                        color: "var(--ink-4)",
                                        fontWeight: 600,
                                        textTransform: "uppercase",
                                    }}
                                >
                                    Уровень
                                </div>
                                <div>{level}</div>
                            </div>
                        </Link>

                        {/* Mobile menu button */}
                        <button
                            className="icon-btn mobile-menu-btn"
                            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                            aria-label="Открыть меню"
                        >
                            <Icon name={mobileMenuOpen ? "close" : "grid"} size="md" />
                        </button>
                    </div>
                </div>
            </header>

            {/* Mobile menu dropdown */}
            {mobileMenuOpen && (
                <div
                    className="md:hidden"
                    style={{
                        position: "fixed",
                        top: 66,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        background: "var(--bg)",
                        zIndex: 39,
                        padding: 16,
                        overflowY: "auto",
                    }}
                >
                    <nav style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                        {NAV_ITEMS.map((item) => {
                            const isActive = currentPathname.startsWith(item.href);
                            return (
                                <Link
                                    key={item.href}
                                    href={item.href}
                                    onClick={() => setMobileMenuOpen(false)}
                                    style={{
                                        position: "relative",
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 12,
                                        padding: "14px 16px",
                                        background: isActive ? "var(--primary-soft)" : "transparent",
                                        color: isActive ? "var(--primary)" : "var(--ink-2)",
                                        border: "none",
                                        borderRadius: "var(--r-sm)",
                                        fontSize: 16,
                                        fontWeight: 600,
                                        textDecoration: "none",
                                        fontFamily: "var(--font-ui)",
                                    }}
                                >
                                    <Icon name={item.icon} size="md" />
                                    {item.label}
                                    {item.href === "/friends" && incomingRequestCount > 0 && (
                                        <span
                                            style={{
                                                marginLeft: "auto",
                                                minWidth: 20,
                                                height: 20,
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                borderRadius: 999,
                                                background: "var(--heart)",
                                                color: "white",
                                                fontSize: 11,
                                                fontWeight: 700,
                                            }}
                                        >
                                            {incomingRequestCount}
                                        </span>
                                    )}
                                </Link>
                            );
                        })}
                    </nav>

                    {/* Profile card in mobile menu */}
                    <div
                        className="card"
                        style={{
                            marginTop: 24,
                            padding: 16,
                            display: "flex",
                            alignItems: "center",
                            gap: 12,
                        }}
                    >
                        <GeoAvatar seed={displayName} size={48} />
                        <div>
                            <div style={{ fontWeight: 700, fontSize: 16 }}>{displayName}</div>
                            <div style={{ fontSize: 12, color: "var(--ink-3)", fontFamily: "var(--font-mono)" }}>
                                Уровень {level}
                            </div>
                        </div>
                        {streak > 0 && (
                            <div className="streak-pill" style={{ marginLeft: "auto" }}>
                                <Icon name="flame" size="sm" />
                                <span className="tnum">{streak}</span>
                            </div>
                        )}
                    </div>
                </div>
            )}
        </>
    );
}
