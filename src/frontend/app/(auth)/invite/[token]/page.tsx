"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useAcceptInvite } from "@/features/auth/hooks/use-auth";
import { Wordmark } from "@/shared/components/wordmark";

export default function AcceptInvitePage() {
    const params = useParams();
    const token = Array.isArray(params.token) ? params.token[0] : (params.token ?? "");

    const [displayName, setDisplayName] = useState("");
    const [password, setPassword] = useState("");
    const acceptInviteMutation = useAcceptInvite(token);

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        acceptInviteMutation.mutate({ displayName, password });
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
                            {acceptInviteMutation.error?.message ??
                                "Приглашение недействительно или уже использовано"}
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
