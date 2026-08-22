"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { isPlatformStaff, useAuthStore } from "@/shared/stores/auth-store";
import { clientLogger } from "@/shared/utils/client-logger";
import { Icon } from "@/shared/components/icon";
import { resolveLegacyAdminRedirect } from "@/features/org-shell/lib/legacy-admin-redirects";
import { PLATFORM_NAVIGATION_ITEMS } from "@/features/admin/constants/navigation";

export default function AdminLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    const router = useRouter();
    const pathname = usePathname();
    const { authenticatedUser, accessToken } = useAuthStore();
    const [mounted, setMounted] = useState(false);
    const [sidebarOpen, setSidebarOpen] = useState(false);

    useEffect(() => {
        setMounted(true);
    }, []);

    // Close the mobile drawer whenever the route changes
    useEffect(() => {
        setSidebarOpen(false);
    }, [pathname]);

    /// Runs ahead of the role gate below and for every visitor, platform staff included: the
    /// screens these paths name were never built under /admin/*, and the notification actionUrls
    /// baked into rows already in the store point straight at them
    /// (docs/TENANCY/ADMIN_UI_DESIGN.md §1.5). `replace` so that Back leaves the panel instead of
    /// bouncing off the redirect.
    const legacyRedirectTarget = resolveLegacyAdminRedirect(pathname);

    useEffect(() => {
        if (!legacyRedirectTarget) return;
        const search = typeof window === "undefined" ? "" : window.location.search;
        router.replace(`${legacyRedirectTarget}${search}`);
    }, [legacyRedirectTarget, router]);

    useEffect(() => {
        if (legacyRedirectTarget) return;
        if (!accessToken) {
            clientLogger.warn("Admin panel access denied — not authenticated", { path: pathname });
            router.replace("/login");
            return;
        }
        if (authenticatedUser && !isPlatformStaff(authenticatedUser.role)) {
            clientLogger.warn("Admin panel access denied — insufficient role", {
                userId: authenticatedUser.id,
                role: authenticatedUser.role,
                path: pathname,
            });
            router.replace("/tree");
        }
    }, [accessToken, authenticatedUser, router, pathname, legacyRedirectTarget]);

    useEffect(() => {
        if (accessToken && authenticatedUser && isPlatformStaff(authenticatedUser.role)) {
            clientLogger.info("Admin panel opened", {
                userId: authenticatedUser.id,
                role: authenticatedUser.role,
                path: pathname,
            });
        }
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [pathname]);

    if (!mounted) return null;

    if (legacyRedirectTarget) return null;

    if (accessToken && !authenticatedUser) {
        return (
            <div className="min-h-screen flex items-center justify-center text-ink-3 text-sm bg-surface">
                Loading...
            </div>
        );
    }

    if (!accessToken || !authenticatedUser || !isPlatformStaff(authenticatedUser.role)) {
        return null;
    }

    // The nav list itself lives in `features/admin/constants/navigation` — module level, so it is
    // one testable value instead of an array literal buried in a client component, and so the
    // reason "Leagues"/"Gamification" are absent (Q-5) is written where the list is. The separate
    // organization-scoped admin panel (for TenancyAdmin/TenancySuperAdmin) is roadmap block 40.20.
    const navItems = PLATFORM_NAVIGATION_ITEMS;

    return (
        <div className="min-h-screen md:flex bg-surface">
            {/* Mobile top bar with hamburger */}
            <div className="md:hidden sticky top-0 z-30 flex items-center gap-3 h-14 px-4 bg-surface border-b border-line">
                <button
                    type="button"
                    onClick={() => setSidebarOpen(true)}
                    aria-label="Open admin menu"
                    className="grid place-items-center w-9 h-9 rounded-xl border border-line text-ink-2"
                >
                    <Icon name="grid" size="md" />
                </button>
                <span className="font-bold text-ink text-sm">Admin Panel</span>
            </div>

            {/* Drawer backdrop (mobile only) */}
            {sidebarOpen && (
                <div
                    className="md:hidden fixed inset-0 z-40 bg-black/40"
                    onClick={() => setSidebarOpen(false)}
                    aria-hidden
                />
            )}

            <aside
                className={`w-56 shrink-0 bg-surface flex flex-col fixed md:static inset-y-0 left-0 z-50 border-r border-line md:border-r-0 transition-transform duration-200 md:translate-x-0 ${
                    sidebarOpen ? "translate-x-0" : "-translate-x-full"
                }`}
            >
                <div className="px-5 py-4 flex items-center justify-between gap-2">
                    <div>
                        <span className="font-bold text-ink text-sm">Admin Panel</span>
                        <span className="block text-xs text-ink-3 mt-0.5">
                            {authenticatedUser.role}
                        </span>
                    </div>
                    <button
                        type="button"
                        onClick={() => setSidebarOpen(false)}
                        aria-label="Close admin menu"
                        className="md:hidden grid place-items-center w-8 h-8 rounded-lg text-ink-3 hover:text-ink"
                    >
                        <Icon name="close" size="sm" />
                    </button>
                </div>
                {/* min-h-0 is required for overflow-y-auto to take effect: a flex item's
                    auto minimum size otherwise refuses to shrink below its content height.
                    Without this the ~760px of nav links overflowed the fixed inset-y-0
                    aside on short viewports and the last entries were unreachable. */}
                <nav className="flex-1 min-h-0 overflow-y-auto py-2 px-2 space-y-0.5">
                    {navItems.map((item) => {
                        const isActive = pathname.startsWith(item.href);
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className={`flex items-center gap-3 px-3 py-2.5 text-sm rounded-xl transition-colors ${
                                    isActive
                                        ? "bg-indigo-soft text-indigo-ink font-medium"
                                        : "text-ink-3 hover:text-ink hover:bg-bg-2"
                                }`}
                            >
                                <Icon name={item.icon} size="sm" />
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
            <main className="flex-1 min-w-0 p-4 md:p-8 overflow-auto">{children}</main>
        </div>
    );
}
