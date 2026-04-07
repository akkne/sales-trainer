"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/components/ui/Icon";
import { useAuthStore } from "@/lib/store/authStore";
import { useSkillTree } from "@/lib/hooks/useSkillTree";

const NAV_LINKS = [
    { href: "/tree", icon: "school", label: "Мастерство" },
    { href: "/league", icon: "trophy", label: "Лиги" },
    { href: "/guidebook", icon: "menu_book", label: "Библиотека" },
    { href: "/dialog", icon: "forum", label: "Диалоги" },
] as const;

export function TopAppBar() {
    const currentPathname = usePathname();
    const { authenticatedUser } = useAuthStore();
    const { data: skillTreeData } = useSkillTree();

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
                SalesMastery
            </Link>

            {/* Navigation links */}
            <nav className="flex items-center gap-1">
                {NAV_LINKS.map((link) => {
                    const isActive = currentPathname.startsWith(link.href);
                    return (
                        <Link
                            key={link.href}
                            href={link.href}
                            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium tonal-transition ${
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
                <button
                    className="relative p-2 rounded-full hover:bg-surface-container tonal-transition"
                    aria-label="Уведомления"
                >
                    <Icon name="notifications" size="md" className="text-on-surface-variant" />
                    {/* Unread indicator dot */}
                    <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-error rounded-full" />
                </button>

                {/* Achievements */}
                <Link
                    href="/profile"
                    className="p-2 rounded-full hover:bg-surface-container tonal-transition"
                    aria-label="Достижения"
                >
                    <Icon name="emoji_events" size="md" className="text-primary" />
                </Link>

                {/* User chip */}
                <Link
                    href="/profile"
                    className="flex items-center gap-2 bg-primary-container text-on-primary-container rounded-full px-3 py-1.5 hover:opacity-90 tonal-transition"
                >
                    <Icon name="military_tech" size="sm" />
                    <span className="text-xs font-semibold">Уровень {level}</span>
                </Link>
            </div>
        </header>
    );
}
