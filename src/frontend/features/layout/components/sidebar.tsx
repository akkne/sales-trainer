"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { useSkillTree } from "@/features/skills/hooks/use-skill-tree";
import { useAuthStore } from "@/shared/stores/auth-store";

const NAV_ITEMS = [
    { href: "/tree", icon: "target", label: "Мастерство" },
    { href: "/league", icon: "trophy", label: "Лиги" },
    { href: "/guidebook", icon: "book", label: "Библиотека" },
    { href: "/dialog", icon: "message", label: "Диалоги" },
    { href: "/profile", icon: "settings", label: "Настройки" },
] as const;

export function Sidebar() {
    const currentPathname = usePathname();
    const { data: skillTreeData } = useSkillTree();
    const { authenticatedUser } = useAuthStore();

    const displayName = authenticatedUser?.displayName ?? "Пользователь";
    const firstLetter = displayName[0]?.toUpperCase() ?? "?";

    const currentSkillName = skillTreeData?.skillNodes?.find(
        (s) => s.status !== "locked"
    )?.title;

    return (
        <aside className="hidden md:flex flex-col w-64 min-h-screen bg-surface border-r border-line p-4 gap-6">
            {/* User profile block */}
            <div className="flex flex-col items-center gap-2 py-4 px-3 bg-bg-2 rounded-2xl">
                <div className="relative">
                    <div className="w-16 h-16 rounded-full bg-ink flex items-center justify-center text-bg font-bold text-xl ring-4 ring-indigo-soft">
                        {firstLetter}
                    </div>
                    {/* Level badge */}
                    {skillTreeData && (
                        <span className="absolute -bottom-1 -right-1 bg-ink text-bg text-xs font-semibold px-2 py-0.5 rounded-full">
                            Ур. {Math.floor((skillTreeData.totalXpAmount ?? 0) / 1000) + 1}
                        </span>
                    )}
                </div>
                <p className="text-sm font-semibold text-ink truncate max-w-full">
                    {displayName}
                </p>
                {skillTreeData && (
                    <p className="text-xs text-ink-3">
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
                            className={`flex items-center gap-3 px-4 py-3 rounded-2xl transition-colors ${
                                isActive
                                    ? "bg-indigo-soft text-indigo font-semibold"
                                    : "text-ink-3 hover:bg-bg-2"
                            }`}
                        >
                            <Icon
                                name={item.icon}
                                size="md"
                            />
                            <span className="text-sm">{item.label}</span>
                        </Link>
                    );
                })}
            </nav>

            {/* Current path widget */}
            {currentSkillName && (
                <div className="mt-auto p-4 bg-bg-2 rounded-2xl">
                    <p className="text-xs text-ink-3 font-medium mb-1">
                        Текущий навык
                    </p>
                    <p className="font-semibold text-sm text-ink truncate">
                        {currentSkillName}
                    </p>
                    <Link
                        href="/tree"
                        className="mt-3 w-full py-2 bg-ink text-bg text-sm font-semibold rounded-full hover:opacity-90 transition-colors flex items-center justify-center gap-1"
                    >
                        Продолжить
                        <Icon name="arrow-right" size="sm" />
                    </Link>
                </div>
            )}

            {/* App branding footer */}
            <div className="text-center text-xs text-ink-4 font-semibold tracking-widest uppercase">
                Sellevate
            </div>
        </aside>
    );
}
