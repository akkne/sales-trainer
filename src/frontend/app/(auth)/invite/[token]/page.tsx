"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useAcceptInvite, useValidateInviteToken } from "@/features/auth/hooks/use-auth";
import { ApiError } from "@/shared/api/api-client";
import { Wordmark } from "@/shared/components/wordmark";

/** Placeholder until a real one is configured, matching `awaiting-organization-gate.tsx` —
 * nothing in the repo declares a support address, and the domain is the one docs/DEPLOYMENT.md
 * routes. */
const SUPPORT_EMAIL = "support@sellevate.site";

// X-10: the backend answers in English (`Sellevate.Identity.Features.Invites.Constants
// .InviteConstants`), so `ApiError.message` is never shown as-is — the status code is the
// reliable, localizable signal. Used both for the pre-submit validity check and for whatever the
// accept call itself still rejects (e.g. the token was revoked in the moment between the two).
function describeInviteError(error: unknown): string {
    if (error instanceof ApiError) {
        switch (error.status) {
            case 404:
                return "Приглашение не найдено. Проверьте, что ссылка скопирована полностью, " +
                    "или попросите того, кто вас пригласил, отправить её заново.";
            case 409:
                return "Это приглашение уже принято. Если аккаунт ваш — войдите; если нет, " +
                    "обратитесь к тому, кто вас пригласил.";
            case 410:
                return "Приглашение больше не действует: истекло или было отозвано. Попросите " +
                    "новое у того, кто вас пригласил.";
            default:
                break;
        }
    }
    return "Не удалось проверить приглашение. Попробуйте обновить страницу или зайти позже.";
}

export default function AcceptInvitePage() {
    const params = useParams();
    const token = Array.isArray(params.token) ? params.token[0] : (params.token ?? "");

    const [displayName, setDisplayName] = useState("");
    const [password, setPassword] = useState("");
    // X-10: checked before the form is even shown — a garbage/expired/revoked/already-used token
    // used to only surface after the invitee filled in a name and password and submitted.
    const tokenStatusQuery = useValidateInviteToken(token);
    const acceptInviteMutation = useAcceptInvite(token);

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        acceptInviteMutation.mutate({ displayName, password });
    }

    if (tokenStatusQuery.isLoading) {
        return (
            <div className="auth">
                <div
                    style={{
                        width: 40,
                        height: 40,
                        borderRadius: "50%",
                        border: "4px solid var(--primary)",
                        borderTopColor: "transparent",
                        animation: "spin 0.8s linear infinite",
                    }}
                />
            </div>
        );
    }

    if (tokenStatusQuery.isError) {
        return (
            <div className="auth">
                <div className="auth-card fade-up">
                    <div className="auth-wordmark">
                        <Wordmark size={28} />
                    </div>
                    <h1 className="auth-heading">Приглашение недоступно</h1>
                    <p className="auth-sub">{describeInviteError(tokenStatusQuery.error)}</p>

                    <a href={`mailto:${SUPPORT_EMAIL}`} className="btn btn-dark btn-block btn-lg">
                        Написать в поддержку
                    </a>

                    <p className="auth-footer" style={{ marginTop: 22 }}>
                        Уже есть аккаунт? <Link href="/login">Войти</Link>
                    </p>
                </div>
            </div>
        );
    }

    return (
        <div className="auth">
            <div className="auth-card fade-up">
                <div className="auth-wordmark">
                    <Wordmark size={28} />
                </div>
                <h1 className="auth-heading">Приглашение в Sellevate</h1>
                <p className="auth-sub">
                    Придумай пароль — и можно начинать. Email подтверждать не нужно,
                    приглашение уже это сделало.
                </p>

                <form onSubmit={handleSubmit} className="col gap-3">
                    <input
                        type="text"
                        placeholder="Твоё имя"
                        value={displayName}
                        onChange={(event) => setDisplayName(event.target.value)}
                        required
                        className="field"
                    />
                    <input
                        type="password"
                        placeholder="Пароль"
                        value={password}
                        onChange={(event) => setPassword(event.target.value)}
                        required
                        minLength={8}
                        className="field"
                    />

                    {acceptInviteMutation.isError && (
                        <p className="auth-error">
                            {describeInviteError(acceptInviteMutation.error)}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={acceptInviteMutation.isPending}
                        className="btn btn-dark btn-block btn-lg"
                        style={{ marginTop: 4 }}
                    >
                        {acceptInviteMutation.isPending ? "Принимаем..." : "Принять приглашение"}
                    </button>
                </form>

                <p className="auth-footer" style={{ marginTop: 22 }}>
                    Уже есть аккаунт? <Link href="/login">Войти</Link>
                </p>
            </div>
        </div>
    );
}
