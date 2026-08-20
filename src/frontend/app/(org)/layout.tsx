"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import {
    isOrganizationStaff,
    isPlatformStaff,
    useAuthStore,
} from "@/shared/stores/auth-store";
import { clientLogger } from "@/shared/utils/client-logger";
import { Icon } from "@/shared/components/icon";
import { ImpersonationBanner } from "@/features/admin/components/impersonation-banner";
import {
    readSerializedImpersonationSession,
    readServerImpersonationSession,
    subscribeToImpersonationSession,
    type ImpersonationSession,
} from "@/features/admin/lib/impersonation-session";
import { OrgSidebar } from "@/features/org-shell/components/org-sidebar";
import { NoOrganizationState } from "@/features/org-shell/components/no-organization-state";
import { useOrganizationNavigationBadges } from "@/features/org-shell/hooks/use-org-nav-badges";

const FALLBACK_ORGANIZATION_NAME = "Ваша компания";

/// The gate reads `accessToken` out of `localStorage`, so the first client render has to agree
/// with the server's — which knew nothing. This is the hydration flag, expressed without an
/// effect: the server snapshot is `false` and the client's is `true`.
const subscribeToNothing = () => () => {};
const readMountedOnClient = () => true;
const readMountedOnServer = () => false;

/**
 * The organization panel's shell (docs/TENANCY/ADMIN_UI_DESIGN.md §1.1–§1.3).
 *
 * A second tree beside `/admin/*` rather than a branch inside it: the platform panel is an
 * internal tool in English, this one is a product surface the customer pays for and reads in
 * Russian, and nesting them would have given the two one layout, one language and one gate.
 *
 * The gate admits the organization's own administrators and, separately, platform staff — who
 * hold no membership and therefore land in state O0 until they enter a company through
 * impersonation from the registry.
 */
export default function OrganizationLayout({ children }: { children: React.ReactNode }) {
    const router = useRouter();
    const pathname = usePathname();
    const { authenticatedUser, accessToken } = useAuthStore();
    const [isSidebarOpen, setIsSidebarOpen] = useState(false);

    const isMounted = useSyncExternalStore(
        subscribeToNothing,
        readMountedOnClient,
        readMountedOnServer
    );

    const serializedImpersonationSession = useSyncExternalStore(
        subscribeToImpersonationSession,
        readSerializedImpersonationSession,
        readServerImpersonationSession
    );

    const impersonatedOrganizationName = useMemo<string | null>(() => {
        if (serializedImpersonationSession === null) return null;
        return (JSON.parse(serializedImpersonationSession) as ImpersonationSession)
            .organizationName;
    }, [serializedImpersonationSession]);

    const isAdmittedToPanel =
        !!authenticatedUser &&
        (isOrganizationStaff(authenticatedUser.orgRole) ||
            isPlatformStaff(authenticatedUser.role));

    useEffect(() => {
        if (!accessToken) {
            clientLogger.warn("Organization panel access denied — not authenticated", {
                path: pathname,
            });
            router.replace("/login");
            return;
        }
        if (authenticatedUser && !isAdmittedToPanel) {
            clientLogger.warn("Organization panel access denied — insufficient role", {
                userId: authenticatedUser.id,
                role: authenticatedUser.role,
                orgRole: authenticatedUser.orgRole ?? null,
                path: pathname,
            });
            router.replace("/tree");
        }
    }, [accessToken, authenticatedUser, isAdmittedToPanel, router, pathname]);

    const hasOrganizationContext = !!authenticatedUser?.orgId;

    // O-4 (docs/AUDIT_PROD.md): `GET /auth/me` has carried `orgName` since Phase 40.20, but the
    // panel kept showing the fallback for every non-impersonated admin. Impersonation still wins
    // when present — that session's name is the platform staff member's own point of truth.
    const organizationName =
        impersonatedOrganizationName ?? authenticatedUser?.orgName ?? FALLBACK_ORGANIZATION_NAME;

    const badges = useOrganizationNavigationBadges(isAdmittedToPanel && hasOrganizationContext);

    if (!isMounted) return null;

    if (accessToken && !authenticatedUser) {
        return (
            <div className="min-h-screen flex items-center justify-center text-ink-3 text-sm bg-surface">
                Загрузка...
            </div>
        );
    }

    if (!accessToken || !authenticatedUser || !isAdmittedToPanel) {
        return null;
    }

    if (!hasOrganizationContext) {
        return (
            <div className="min-h-screen bg-surface p-4 md:p-8">
                <ImpersonationBanner />
                <NoOrganizationState />
            </div>
        );
    }

    return (
        <div className="min-h-screen md:flex bg-surface">
            <div className="md:hidden sticky top-0 z-30 flex items-center gap-3 h-14 px-4 bg-surface border-b border-line">
                <button
                    type="button"
                    onClick={() => setIsSidebarOpen(true)}
                    aria-label="Открыть меню"
                    className="grid place-items-center w-9 h-9 rounded-xl border border-line text-ink-2"
                >
                    <Icon name="grid" size="md" />
                </button>
                <span className="font-bold text-ink text-sm truncate">
                    {organizationName}
                </span>
            </div>

            {isSidebarOpen && (
                <div
                    className="md:hidden fixed inset-0 z-40 bg-black/40"
                    onClick={() => setIsSidebarOpen(false)}
                    aria-hidden
                />
            )}

            <OrgSidebar
                organizationName={organizationName}
                isOpen={isSidebarOpen}
                onClose={() => setIsSidebarOpen(false)}
                badges={badges}
            />

            <main className="flex-1 min-w-0 p-4 md:p-8 overflow-auto">
                <ImpersonationBanner />
                {children}
            </main>
        </div>
    );
}
