import { NavRail } from "@/features/layout/components/nav-rail";
import { BottomNav } from "@/features/layout/components/bottom-nav";
import { MobileTopbar } from "@/features/layout/components/mobile-topbar";

export default function MainLayout({
    children,
}: {
    children: React.ReactNode;
}) {
    return (
        <div className="shell">
            {/* Left nav rail — desktop only (hidden on mobile via CSS) */}
            <NavRail />

            {/* Mobile top bar — guidebook/discuss/settings/notifications (rail-only links) */}
            <MobileTopbar />

            {/* Scrollable content area */}
            <main className="shell-content has-bottom-nav">
                {children}
            </main>

            {/* Mobile bottom navigation — hidden on desktop via CSS */}
            <BottomNav />
        </div>
    );
}
