"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";

const NAV_ITEMS = [
    { href: "/tree",      icon: "compass", label: "Путь" },
    { href: "/dialog",    icon: "message", label: "Практика" },
    { href: "/guidebook", icon: "book",    label: "Справочник" },
    { href: "/friends",   icon: "users",   label: "Друзья" },
    { href: "/profile",   icon: "user",    label: "Профиль" },
] as const satisfies { href: string; icon: import("@/shared/components/icon").IconName; label: string }[];

export function BottomNav() {
    const currentPathname = usePathname();

    return (
        <nav className="bottom-nav md:hidden">
            {NAV_ITEMS.map((navItem) => {
                const isActive = currentPathname.startsWith(navItem.href);
                return (
                    <Link
                        key={navItem.href}
                        href={navItem.href}
                        className={isActive ? "active" : undefined}
                    >
                        <Icon
                            name={navItem.icon}
                            size="md"
                        />
                        <span>{navItem.label}</span>
                    </Link>
                );
            })}
        </nav>
    );
}
