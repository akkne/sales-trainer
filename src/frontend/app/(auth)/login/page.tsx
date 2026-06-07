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
            <div className="app-backdrop" />
            <div className="auth-card card fade-up">
                <div style={{ display: "flex", justifyContent: "center", marginBottom: 6 }}>
                    <Wordmark size={28} />
                </div>
                <h2 className="h2" style={{ textAlign: "center", margin: "16px 0 6px" }}>
                    С возвращением
                </h2>
                <p className="small" style={{ textAlign: "center", marginBottom: 26 }}>
                    Войди и продолжи прокачивать навык
                </p>

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
                        <p className="small" style={{ color: "var(--heart)" }}>
                            {loginMutation.error?.message ?? "Ошибка входа"}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={loginMutation.isPending}
                        className="btn btn-dark btn-block btn-lg"
                        style={{ marginTop: 6 }}
                    >
                        {loginMutation.isPending ? "Входим..." : "Войти"}
                    </button>
                </form>

                <div className="auth-or">
                    <span>или</span>
                </div>

                <GoogleLoginButton />

                <p className="small" style={{ textAlign: "center", marginTop: 22 }}>
                    Нет аккаунта?{" "}
                    <Link href="/register" style={{ color: "var(--primary)", fontWeight: 700 }}>
                        Создать
                    </Link>
                </p>
            </div>
        </div>
    );
}
