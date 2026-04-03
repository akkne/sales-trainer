"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const NAV_ITEMS = [
    { href: "/tree", icon: "🗺️", label: "Путь" },
    { href: "/league", icon: "🏆", label: "Лига" },
    { href: "/profile", icon: "👤", label: "Профиль" },
];

export function BottomNav() {
    const currentPathname = usePathname();

    return (
        <nav className="fixed bottom-0 left-0 right-0 bg-white border-t border-gray-100 flex pb-[env(safe-area-inset-bottom)]">
            {NAV_ITEMS.map((navItem) => {
                const isActive = currentPathname.startsWith(navItem.href);
                return (
                    <Link
                        key={navItem.href}
                        href={navItem.href}
                        className={`flex-1 flex flex-col items-center py-3 gap-1 transition-colors ${
                            isActive ? "text-[#58CC02]" : "text-gray-400 hover:text-gray-600"
                        }`}
                    >
                        <span className="text-xl">{navItem.icon}</span>
                        <span className="text-xs font-semibold">{navItem.label}</span>
                    </Link>
                );
            })}
        </nav>
    );
}
