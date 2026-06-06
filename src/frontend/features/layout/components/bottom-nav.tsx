"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";

const NAV_ITEMS = [
    { href: "/tree", icon: "book", label: "Путь" },
    { href: "/league", icon: "trophy", label: "Лига" },
    { href: "/guidebook", icon: "layers", label: "Справочник" },
    { href: "/dialog", icon: "message", label: "Диалог" },
    { href: "/friends", icon: "users", label: "Друзья" },
    { href: "/profile", icon: "user", label: "Профиль" },
] as const satisfies { href: string; icon: import("@/shared/components/icon").IconName; label: string }[];

export function BottomNav() {
    const currentPathname = usePathname();

    return (
        <nav className="md:hidden fixed bottom-0 left-0 right-0 z-50 glass-nav border-t border-outline-variant flex pb-[env(safe-area-inset-bottom)]">
            {NAV_ITEMS.map((navItem) => {
                const isActive = currentPathname.startsWith(navItem.href);
                return (
                    <Link
                        key={navItem.href}
                        href={navItem.href}
                        className={`flex-1 flex flex-col items-center py-3 gap-0.5 tonal-transition ${
                            isActive
                                ? "text-primary"
                                : "text-on-surface-variant hover:text-primary"
                        }`}
                    >
                        <Icon
                            name={navItem.icon}
                            size="md"
                        />
                        <span className={`text-[10px] ${isActive ? "font-semibold" : "font-medium"}`}>
                            {navItem.label}
                        </span>
                    </Link>
                );
            })}
        </nav>
    );
}
