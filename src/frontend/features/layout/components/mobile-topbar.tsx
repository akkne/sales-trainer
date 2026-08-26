"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/shared/components/icon";
import { Wordmark } from "@/shared/components/wordmark";
import { NotificationBell } from "@/features/notifications/components/notification-bell";

/// Rail-only destinations that have no slot in the bottom nav. Discuss is deliberately
/// absent — see the note on <c>RAIL_ITEMS</c> in <c>NavRail</c>.
const TOPBAR_LINKS = [
    { href: "/guidebook", icon: "book",     label: "Справочник" },
    { href: "/settings",  icon: "settings", label: "Настройки" },
] as const satisfies { href: string; icon: import("@/shared/components/icon").IconName; label: string }[];

export function MobileTopbar() {
    const currentPathname = usePathname();

    return (
        <header className="mobile-topbar" aria-label="Верхняя панель">
            <Link href="/tree" aria-label="На главную">
                <Wordmark size={24} />
            </Link>

            <div className="mobile-topbar-actions">
                {TOPBAR_LINKS.map((topbarLink) => (
                    <Link
                        key={topbarLink.href}
                        href={topbarLink.href}
                        className={`icon-btn${currentPathname.startsWith(topbarLink.href) ? " active" : ""}`}
                        aria-label={topbarLink.label}
                        title={topbarLink.label}
                    >
                        <Icon name={topbarLink.icon} size={18} />
                    </Link>
                ))}
                <NotificationBell />
            </div>
        </header>
    );
}
