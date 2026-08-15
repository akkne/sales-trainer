"use client";

import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/shared/stores/auth-store";
import {
    clearImpersonationSession,
    isImpersonationExpired,
    readSerializedImpersonationSession,
    readServerImpersonationSession,
    subscribeToImpersonationSession,
    type ImpersonationSession,
} from "@/features/admin/lib/impersonation-session";

const EXPIRY_POLL_INTERVAL_MILLISECONDS = 10_000;

/**
 * Shown across the top of the app whenever platform staff are viewing it as another organization
 * (Phase 40.9). It is the only way back out, so it renders on every screen of the main shell
 * rather than only on the admin panel the impersonation started from.
 */
export function ImpersonationBanner() {
    const router = useRouter();
    const { setAccessToken } = useAuthStore();

    const serializedSession = useSyncExternalStore(
        subscribeToImpersonationSession,
        readSerializedImpersonationSession,
        readServerImpersonationSession
    );

    const session = useMemo<ImpersonationSession | null>(
        () => (serializedSession === null ? null : (JSON.parse(serializedSession) as ImpersonationSession)),
        [serializedSession]
    );

    const [expiryPollCount, setExpiryPollCount] = useState(0);

    useEffect(() => {
        if (!session) return;
        const intervalHandle = setInterval(
            () => setExpiryPollCount((previousCount) => previousCount + 1),
            EXPIRY_POLL_INTERVAL_MILLISECONDS
        );
        return () => clearInterval(intervalHandle);
    }, [session]);

    const hasExpired = useMemo(
        () => (session ? isImpersonationExpired(session) : false),
        // expiryPollCount is the clock tick: it carries no value, it only re-evaluates the expiry.
        // eslint-disable-next-line react-hooks/exhaustive-deps
        [session, expiryPollCount]
    );

    if (!session) return null;

    const returnToPlatform = () => {
        setAccessToken(session.platformAccessToken);
        clearImpersonationSession();
        router.push("/admin/organizations");
    };

    return (
        <div
            role="status"
            className="flex flex-wrap items-center justify-between gap-2 px-4 py-2 bg-olive-soft text-olive text-sm"
        >
            <span>
                {hasExpired
                    ? `Сеанс просмотра «${session.organizationName}» истёк`
                    : `Вы смотрите приложение как «${session.organizationName}»`}
            </span>
            <button type="button" onClick={returnToPlatform} className="underline font-medium">
                Вернуться в платформу
            </button>
        </div>
    );
}
