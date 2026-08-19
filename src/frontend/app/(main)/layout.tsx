import { NavRail } from "@/features/layout/components/nav-rail";
import { BottomNav } from "@/features/layout/components/bottom-nav";
import { MobileTopbar } from "@/features/layout/components/mobile-topbar";
import { ImpersonationBanner } from "@/features/admin/components/impersonation-banner";
import { AwaitingOrganizationGate } from "@/features/auth/components/awaiting-organization-gate";

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
                <ImpersonationBanner />
                {/* Inside the chrome rather than around it: someone waiting for an invitation is a
                    signed-in user, not a locked-out one, and stripping the rail would read as an
                    error page. */}
                <AwaitingOrganizationGate>{children}</AwaitingOrganizationGate>
            </main>

            {/* Mobile bottom navigation — hidden on desktop via CSS */}
            <BottomNav />
        </div>
    );
}
