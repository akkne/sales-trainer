"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/components/ui/Icon";
import { useSkillTree } from "@/lib/hooks/useSkillTree";
import { useAuthStore } from "@/lib/store/authStore";

const NAV_ITEMS = [
    { href: "/tree", icon: "school", label: "Мастерство" },
    { href: "/league", icon: "trophy", label: "Лиги" },
    { href: "/guidebook", icon: "menu_book", label: "Библиотека" },
    { href: "/dialog", icon: "forum", label: "Диалоги" },
    { href: "/profile", icon: "settings", label: "Настройки" },
] as const;

export function Sidebar() {
    const currentPathname = usePathname();
    const { data: skillTreeData } = useSkillTree();
    const { authenticatedUser } = useAuthStore();

    const displayName = authenticatedUser?.displayName ?? "Пользователь";
    const firstLetter = displayName[0]?.toUpperCase() ?? "?";

    // Find current skill name from tree data
    const currentSkillName = skillTreeData?.skillNodes?.find(
        (s) => s.status !== "locked"
    )?.title;

    return (
        <aside className="hidden md:flex flex-col w-64 min-h-screen bg-surface-container-low border-r border-outline-variant p-4 gap-6">
            {/* User profile block */}
            <div className="flex flex-col items-center gap-2 py-4 px-3 bg-surface-container rounded-2xl">
                <div className="relative">
                    <div className="w-16 h-16 rounded-full bg-primary flex items-center justify-center text-on-primary font-bold text-xl ring-4 ring-primary-container">
                        {firstLetter}
                    </div>
                    {/* Level badge */}
                    {skillTreeData && (
                        <span className="absolute -bottom-1 -right-1 bg-primary text-on-primary text-xs font-semibold px-2 py-0.5 rounded-full">
                            Ур. {Math.floor((skillTreeData.totalXpAmount ?? 0) / 1000) + 1}
                        </span>
                    )}
                </div>
                <p className="text-sm font-semibold text-on-surface truncate max-w-full">
                    {displayName}
                </p>
                {skillTreeData && (
                    <p className="text-xs text-on-surface-variant">
                        {skillTreeData.totalXpAmount ?? 0} XP
                    </p>
                )}
            </div>

            {/* Navigation links */}
            <nav className="flex flex-col gap-1">
                {NAV_ITEMS.map((item) => {
                    const isActive = currentPathname.startsWith(item.href);
                    return (
                        <Link
                            key={item.href}
                            href={item.href}
                            className={`flex items-center gap-3 px-4 py-3 rounded-2xl tonal-transition ${
                                isActive
                                    ? "bg-primary-container text-primary font-semibold"
                                    : "text-on-surface-variant hover:bg-surface-container"
                            }`}
                        >
                            <Icon
                                name={item.icon}
                                variant={isActive ? "filled" : "outlined"}
                                size="md"
                            />
                            <span className="text-sm">{item.label}</span>
                        </Link>
                    );
                })}
            </nav>

            {/* Current path widget */}
            {currentSkillName && (
                <div className="mt-auto p-4 bg-surface-container rounded-2xl">
                    <p className="text-xs text-on-surface-variant font-medium mb-1">
                        Текущий навык
                    </p>
                    <p className="font-semibold text-sm text-on-surface truncate">
                        {currentSkillName}
                    </p>
                    <Link
                        href="/tree"
                        className="mt-3 w-full py-2 bg-primary text-on-primary text-sm font-semibold rounded-full hover:bg-primary-dim tonal-transition flex items-center justify-center gap-1"
                    >
                        Продолжить
                        <Icon name="arrow_forward" size="sm" />
                    </Link>
                </div>
            )}

            {/* App branding footer */}
            <div className="text-center text-xs text-outline font-semibold tracking-widest uppercase">
                Sellevate
            </div>
        </aside>
    );
}
