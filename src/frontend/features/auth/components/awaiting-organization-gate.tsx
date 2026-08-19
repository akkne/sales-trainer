"use client";

import { useSyncExternalStore } from "react";
import { Button } from "@/shared/components/button";
import { EmptyState } from "@/shared/components/empty-state";
import { useLogout } from "@/features/auth/hooks/use-auth";
import { isPlatformStaff, useAuthStore } from "@/shared/stores/auth-store";

/**
 * Placeholder until a real one is configured — nothing in the repo declares a support address, and
 * the domain is the one `docs/DEPLOYMENT.md` routes.
 */
const SUPPORT_EMAIL = "support@sellevate.site";

/// The store reads `accessToken` out of `localStorage`, so the first client render has to agree with
/// the server's, which knew nothing. Same hydration flag as `app/(org)/layout.tsx`, expressed the
/// same way: the server snapshot is `false`, the client's is `true`.
const subscribeToNothing = () => () => {};
const readMountedOnClient = () => true;
const readMountedOnServer = () => false;

/**
 * Stands between a signed-in account and the learner app when that account belongs to no
 * organization (Phase 40.37).
 *
 * <p>Registration creates an identity and no membership, so this screen — not the skill tree — is
 * what a self-registered person sees until an organization admin invites their address. It is a
 * waiting room, not an error: nothing went wrong, the invitation simply has not arrived.</p>
 *
 * <p>Two deliberate exemptions. Platform staff pass through, because `Admin`/`SuperAdmin` are
 * Sellevate's own roles and are not bounded by tenancy — they hold no membership anywhere and must
 * still reach the exercises. And an unauthenticated visitor passes through untouched, so this gate
 * cannot become a second, competing login redirect: signing in is still the api-client's 401
 * handler's job, and the demo token — which has no user row at all — keeps working.</p>
 */
export function AwaitingOrganizationGate({ children }: { children: React.ReactNode }) {
    const { authenticatedUser } = useAuthStore();
    const logoutMutation = useLogout();

    const isMounted = useSyncExternalStore(
        subscribeToNothing,
        readMountedOnClient,
        readMountedOnServer
    );

    if (!isMounted) return <>{children}</>;

    const isWaitingForAnInvitation =
        !!authenticatedUser &&
        !authenticatedUser.orgId &&
        !isPlatformStaff(authenticatedUser.role);

    if (!isWaitingForAnInvitation) return <>{children}</>;

    return (
        <div className="mx-auto max-w-xl py-10">
            <EmptyState
                icon="clock"
                title="Ждём приглашение от компании"
                description={
                    "Аккаунт создан, но он пока не привязан ни к одной организации, поэтому " +
                    "тренажёр ещё не доступен. Доступ открывает администратор вашей компании — " +
                    "попросите его отправить приглашение на этот адрес. Если вы сами администратор " +
                    "и это ошибка, напишите нам."
                }
                action={
                    <div className="flex flex-wrap items-center justify-center gap-2">
                        <a href={`mailto:${SUPPORT_EMAIL}`}>
                            <Button variant="primary">Написать в поддержку</Button>
                        </a>
                        {/* The exit has to live here: the gate covers /settings too, and without it
                            a person waiting for an invitation could not even sign out. */}
                        <Button
                            variant="ghost"
                            onClick={() => logoutMutation.mutate()}
                            loading={logoutMutation.isPending}
                        >
                            Выйти
                        </Button>
                    </div>
                }
            />
        </div>
    );
}
