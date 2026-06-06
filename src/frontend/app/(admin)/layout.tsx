"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useAuthStore } from "@/shared/stores/auth-store";
import { clientLogger } from "@/shared/utils/client-logger";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";

const NAV_ICONS: Record<string, IconName> = {
    "/admin/skills": "target",
    "/admin/topics": "folder",
    "/admin/lessons": "book",
    "/admin/reference": "layers",
    "/admin/techniques": "sparkle",
    "/admin/dialog": "message",
    "/admin/open-question": "sparkle",
    "/admin/voice/usage": "mic",
    "/admin/users": "users",
};

export default function AdminLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    const router = useRouter();
    const pathname = usePathname();
    const { authenticatedUser, accessToken } = useAuthStore();
    const [mounted, setMounted] = useState(false);

    useEffect(() => {
        setMounted(true);
    }, []);

    useEffect(() => {
        if (!accessToken) {
            clientLogger.warn("Admin panel access denied — not authenticated", { path: pathname });
            router.replace("/login");
            return;
        }
        if (
            authenticatedUser &&
            authenticatedUser.role !== "Admin" &&
            authenticatedUser.role !== "SuperAdmin"
        ) {
            clientLogger.warn("Admin panel access denied — insufficient role", {
                userId: authenticatedUser.id,
                role: authenticatedUser.role,
                path: pathname,
            });
            router.replace("/tree");
        }
    }, [accessToken, authenticatedUser, router, pathname]);

    useEffect(() => {
        if (
            accessToken &&
            authenticatedUser &&
            (authenticatedUser.role === "Admin" || authenticatedUser.role === "SuperAdmin")
        ) {
            clientLogger.info("Admin panel opened", {
                userId: authenticatedUser.id,
                role: authenticatedUser.role,
                path: pathname,
            });
        }
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [pathname]);

    if (!mounted) return null;

    if (accessToken && !authenticatedUser) {
        return (
            <div className="min-h-screen flex items-center justify-center text-ink-3 text-sm bg-surface">
                Loading...
            </div>
        );
    }

    if (
        !accessToken ||
        !authenticatedUser ||
        (authenticatedUser.role !== "Admin" && authenticatedUser.role !== "SuperAdmin")
    ) {
        return null;
    }

    const isSuperAdmin = authenticatedUser.role === "SuperAdmin";

    const navItems = [
        { href: "/admin/skills", label: "Skills" },
        { href: "/admin/topics", label: "Topics" },
        { href: "/admin/lessons", label: "Lessons" },
        { href: "/admin/reference", label: "Reference" },
        { href: "/admin/techniques", label: "Techniques" },
        { href: "/admin/dialog", label: "Dialog" },
        { href: "/admin/open-question", label: "AI Prompts" },
        { href: "/admin/voice/usage", label: "Voice Usage" },
        ...(isSuperAdmin ? [{ href: "/admin/users", label: "Users" }] : []),
    ];

    return (
        <div className="min-h-screen flex bg-surface">
            <aside className="w-56 shrink-0 bg-surface flex flex-col">
                <div className="px-5 py-4">
                    <span className="font-bold text-ink text-sm">Admin Panel</span>
                    <span className="block text-xs text-ink-3 mt-0.5">
                        {authenticatedUser.role}
                    </span>
                </div>
                <nav className="flex-1 py-2 px-2 space-y-0.5">
                    {navItems.map((item) => {
                        const isActive = pathname.startsWith(item.href);
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className={`flex items-center gap-3 px-3 py-2.5 text-sm rounded-xl transition-colors ${
                                    isActive
                                        ? "bg-indigo-soft text-indigo font-medium"
                                        : "text-ink-3 hover:text-ink hover:bg-bg-2"
                                }`}
                            >
                                <Icon
                                    name={NAV_ICONS[item.href] ?? "folder"}
                                    size="sm"
                                />
                                {item.label}
                            </Link>
                        );
                    })}
                </nav>
                <div className="px-5 py-4">
                    <Link
                        href="/tree"
                        className="flex items-center gap-2 text-xs text-ink-3 hover:text-ink transition-colors"
                    >
                        <Icon name="arrow-left" size="sm" />
                        Back to app
                    </Link>
                </div>
            </aside>
            <main className="flex-1 p-8 overflow-auto">{children}</main>
        </div>
    );
}
