"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useAuthStore } from "@/lib/store/authStore";
import { clientLogger } from "@/lib/clientLogger";

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
            <div className="min-h-screen flex items-center justify-center text-gray-400 text-sm">
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
        { href: "/admin/lessons", label: "Lessons" },
        { href: "/admin/reference", label: "Reference" },
        { href: "/admin/dialog", label: "Dialog" },
        { href: "/admin/seeder", label: "Skills Seeder" },
        { href: "/admin/content", label: "Content Import" },
        ...(isSuperAdmin ? [{ href: "/admin/users", label: "Users" }] : []),
    ];

    return (
        <div className="min-h-screen flex bg-gray-50">
            <aside className="w-52 shrink-0 bg-white border-r border-gray-200 flex flex-col">
                <div className="px-5 py-4 border-b border-gray-200">
                    <span className="font-semibold text-gray-800 text-sm">Admin Panel</span>
                    <span className="block text-xs text-gray-400 mt-0.5">
                        {authenticatedUser.role}
                    </span>
                </div>
                <nav className="flex-1 py-3">
                    {navItems.map((item) => {
                        const isActive = pathname.startsWith(item.href);
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className={`block px-5 py-2.5 text-sm transition-colors ${
                                    isActive
                                        ? "bg-gray-100 text-gray-900 font-medium"
                                        : "text-gray-500 hover:text-gray-800 hover:bg-gray-50"
                                }`}
                            >
                                {item.label}
                            </Link>
                        );
                    })}
                </nav>
                <div className="px-5 py-4 border-t border-gray-200">
                    <Link
                        href="/tree"
                        className="text-xs text-gray-400 hover:text-gray-600 transition-colors"
                    >
                        ← Back to app
                    </Link>
                </div>
            </aside>
            <main className="flex-1 p-8 overflow-auto">{children}</main>
        </div>
    );
}
