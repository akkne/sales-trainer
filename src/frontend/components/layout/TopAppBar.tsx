"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/components/ui/Icon";
import { NotificationBell } from "@/components/notifications/NotificationBell";
import { useAuthStore } from "@/lib/store/authStore";
import { useSkillTree } from "@/lib/hooks/useSkillTree";
import { useFriendRequests } from "@/lib/hooks/useFriends";

const NAV_LINKS = [
    { href: "/tree", icon: "school", label: "Мастерство" },
    { href: "/league", icon: "trophy", label: "Лиги" },
    { href: "/guidebook", icon: "menu_book", label: "Библиотека" },
    { href: "/dialog", icon: "forum", label: "Диалоги" },
    { href: "/friends", icon: "group", label: "Друзья" },
] as const;

export function TopAppBar() {
    const currentPathname = usePathname();
    const { authenticatedUser } = useAuthStore();
    const { data: skillTreeData } = useSkillTree();
    const { data: friendRequests } = useFriendRequests();

    const incomingRequestCount = friendRequests?.filter(
        (request) => request.direction === "incoming"
    ).length ?? 0;

    const displayName = authenticatedUser?.displayName ?? "?";
    const firstLetter = displayName[0]?.toUpperCase() ?? "?";
    const level = Math.floor((skillTreeData?.totalXpAmount ?? 0) / 1000) + 1;

    return (
        <header className="hidden md:flex sticky top-0 z-40 glass-nav items-center justify-between px-6 h-14 border-b border-outline-variant">
            {/* Brand */}
            <Link
                href="/tree"
                className="flex items-center gap-2 font-headline font-bold text-lg text-primary"
            >
                <Icon name="psychology" size="md" className="text-primary" />
                Sellevate
            </Link>

            {/* Navigation links */}
            <nav className="flex items-center gap-1">
                {NAV_LINKS.map((link) => {
                    const isActive = currentPathname.startsWith(link.href);
                    return (
                        <Link
                            key={link.href}
                            href={link.href}
                            className={`relative flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium tonal-transition ${
                                isActive
                                    ? "text-primary font-semibold"
                                    : "text-on-surface-variant hover:text-primary hover:bg-surface-container"
                            }`}
                        >
                            <Icon
                                name={link.icon}
                                variant={isActive ? "filled" : "outlined"}
                                size="sm"
                            />
                            {link.label}
                            {link.href === "/friends" && incomingRequestCount > 0 && (
                                <span className="absolute -top-1 -right-1 min-w-5 h-5 flex items-center justify-center rounded-full bg-error text-on-error text-[10px] font-bold px-1">
                                    {incomingRequestCount}
                                </span>
                            )}
                        </Link>
                    );
                })}
            </nav>

            {/* Right side: streak, notifications, user */}
            <div className="flex items-center gap-3">
                {/* Streak indicator */}
                {skillTreeData && skillTreeData.currentStreakDayCount > 0 && (
                    <div className="flex items-center gap-1 text-error-container">
                        <Icon name="local_fire_department" size="sm" />
                        <span className="text-xs font-bold">
                            {skillTreeData.currentStreakDayCount}
                        </span>
                    </div>
                )}

                {/* Notifications */}
                <NotificationBell />

                {/* Profile chip with user avatar */}
                <Link
                    href="/profile"
                    className="flex items-center gap-2 bg-primary-container text-on-primary-container rounded-full pl-1 pr-3 py-1 hover:opacity-90 tonal-transition"
                    aria-label={`Профиль (${displayName})`}
                >
                    <span className="flex items-center justify-center w-6 h-6 rounded-full bg-primary text-on-primary text-xs font-bold">
                        {firstLetter}
                    </span>
                    <span className="text-xs font-semibold">Уровень {level}</span>
                </Link>
            </div>
        </header>
    );
}
