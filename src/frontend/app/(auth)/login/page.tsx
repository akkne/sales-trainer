"use client";

import Link from "next/link";
import { useState } from "react";
import { useLogin } from "@/features/auth/hooks/use-auth";
import { GoogleLoginButton } from "@/shared/components/google-login-button";
import { Wordmark } from "@/shared/components/wordmark";

export default function LoginPage() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const loginMutation = useLogin();

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        loginMutation.mutate({ email, password });
    }

    return (
        <div className="auth">
            <div className="auth-card fade-up">
                <div className="auth-wordmark">
                    <Wordmark size={28} />
                </div>
                <h1 className="auth-heading">С возвращением</h1>
                <p className="auth-sub">Войди и продолжай прокачивать навыки</p>

                <form onSubmit={handleSubmit} className="col gap-3">
                    <input
                        type="email"
                        placeholder="Email"
                        value={email}
                        onChange={(event) => setEmail(event.target.value)}
                        required
                        className="field"
                    />
                    <input
                        type="password"
                        placeholder="Пароль"
                        value={password}
                        onChange={(event) => setPassword(event.target.value)}
                        required
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

                <div className="auth-or">
                    <span>или</span>
                </div>

                <GoogleLoginButton />

                <p className="auth-footer" style={{ marginTop: 22 }}>
                    Нет аккаунта?{" "}
                    <Link href="/register">Зарегистрироваться</Link>
                </p>
            </div>
        </div>
    );
}
