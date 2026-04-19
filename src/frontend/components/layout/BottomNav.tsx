"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon, IconName } from "@/components/ui/Icon";

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

export function BottomNav() {
    const currentPathname = usePathname();

    return (
        <nav
            className="md:hidden"
            style={{
                position: "fixed",
                bottom: 0,
                left: 0,
                right: 0,
                zIndex: 50,
                background: "color-mix(in srgb, var(--surface) 85%, transparent)",
                backdropFilter: "blur(10px)",
                WebkitBackdropFilter: "blur(10px)",
                borderTop: "1px solid var(--line)",
                display: "flex",
                paddingBottom: "env(safe-area-inset-bottom)",
            }}
        >
            {NAV_ITEMS.map((item) => {
                const isActive = currentPathname.startsWith(item.href);
                return (
                    <Link
                        key={item.href}
                        href={item.href}
                        style={{
                            flex: 1,
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                            padding: "12px 0",
                            gap: 2,
                            color: isActive ? "var(--indigo)" : "var(--ink-3)",
                            textDecoration: "none",
                            transition: "color 0.15s",
                        }}
                    >
                        <Icon name={item.icon} size="md" />
                        <span
                            style={{
                                fontSize: 10,
                                fontWeight: isActive ? 600 : 500,
                            }}
                        >
                            {item.label}
                        </span>
                    </Link>
                );
            })}
        </nav>
    );
}
