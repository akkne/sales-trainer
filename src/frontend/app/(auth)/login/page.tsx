"use client";

import { useState } from "react";
import {
    useLogin,
    useLoginStart,
    type LoginMethod,
} from "@/features/auth/hooks/use-auth";
import { GoogleLoginButton } from "@/shared/components/google-login-button";
import { Wordmark } from "@/shared/components/wordmark";

/**
 * Phase 40.8 — two-stage login. The screen asks for the address first and only then for the
 * credential the server named, because the login method is a per-organization setting
 * (docs/TENANCY/TENANCY.md §4.5). Today the answer is always "password", and that is the point:
 * when a customer switches to their own directory, this screen already has the shape for it.
 */
export default function LoginPage() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [loginMethod, setLoginMethod] = useState<LoginMethod | null>(null);
    const loginStartMutation = useLoginStart();
    const loginMutation = useLogin();

    function handleEmailSubmit(event: React.FormEvent) {
        event.preventDefault();
        loginStartMutation.mutate(email, {
            onSuccess: (response) => setLoginMethod(response.method),
        });
    }

    function handlePasswordSubmit(event: React.FormEvent) {
        event.preventDefault();
        loginMutation.mutate({ email, password });
    }

    function handleChangeEmail() {
        setLoginMethod(null);
        setPassword("");
        loginMutation.reset();
    }

    return (
        <div className="auth">
            <div className="auth-card fade-up">
                <div className="auth-wordmark">
                    <Wordmark size={28} />
                </div>
                <h1 className="auth-heading">С возвращением</h1>
                <p className="auth-sub">Войди и продолжай прокачивать навыки</p>

                {loginMethod === null ? (
                    <>
                        <form onSubmit={handleEmailSubmit} className="col gap-3">
                            <input
                                type="email"
                                placeholder="Email"
                                value={email}
                                onChange={(event) => setEmail(event.target.value)}
                                required
                                autoFocus
                                className="field"
                            />

                            {loginStartMutation.isError && (
                                <p className="auth-error">
                                    {loginStartMutation.error?.message ?? "Не удалось продолжить"}
                                </p>
                            )}

                            <button
                                type="submit"
                                disabled={loginStartMutation.isPending}
                                className="btn btn-dark btn-block btn-lg"
                                style={{ marginTop: 4 }}
                            >
                                {loginStartMutation.isPending ? "Проверяем..." : "Продолжить"}
                            </button>
                        </form>

                        <div className="auth-or">
                            <span>или</span>
                        </div>

                        <GoogleLoginButton />
                    </>
                ) : (
                    <>
                        <p className="auth-sub" style={{ marginTop: 0 }}>
                            {email}{" "}
                            <button
                                type="button"
                                onClick={handleChangeEmail}
                                className="auth-link"
                            >
                                Изменить
                            </button>
                        </p>

                        {loginMethod === "password" ? (
                            <form onSubmit={handlePasswordSubmit} className="col gap-3">
                                <input
                                    type="password"
                                    placeholder="Пароль"
                                    value={password}
                                    onChange={(event) => setPassword(event.target.value)}
                                    required
                                    autoFocus
                                    className="field"
                                />

                                {loginMutation.isError && (
                                    <p className="auth-error">
                                        {loginMutation.error?.message ?? "Не удалось войти"}
                                    </p>
                                )}

                                <button
                                    type="submit"
                                    disabled={loginMutation.isPending}
                                    className="btn btn-dark btn-block btn-lg"
                                    style={{ marginTop: 4 }}
                                >
                                    {loginMutation.isPending ? "Выполняется вход..." : "Войти"}
                                </button>
                            </form>
                        ) : (
                            // The seam is deliberately visible rather than silently falling back to
                            // a password form: an organization configured for SSO has password
                            // login disabled on the server too, so offering the field would only
                            // produce a confusing 401.
                            <p className="auth-error">
                                Твоя компания использует корпоративный вход (SSO). Он ещё не
                                подключён — напиши администратору компании.
                            </p>
                        )}
                    </>
                )}

                <p className="auth-footer" style={{ marginTop: 22 }}>
                    Доступ в Sellevate — только по приглашению от твоей компании.
                </p>
            </div>
        </div>
    );
}
