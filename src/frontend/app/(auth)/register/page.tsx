"use client";

import Link from "next/link";
import { useState } from "react";
import { useRegister } from "@/features/auth/hooks/use-auth";
import { GoogleLoginButton } from "@/shared/components/google-login-button";
import { Wordmark } from "@/shared/components/wordmark";

export default function RegisterPage() {
    const [displayName, setDisplayName] = useState("");
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const registerMutation = useRegister();

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        registerMutation.mutate({ email, password, displayName });
    }

    return (
        <div className="auth">
            <div className="auth-card fade-up">
                <div className="auth-wordmark">
                    <Wordmark size={28} />
                </div>
                <h1 className="auth-heading">Создай аккаунт</h1>
                <p className="auth-sub">Пара секунд — и первый урок твой</p>

                <form onSubmit={handleSubmit} className="col gap-3">
                    <input
                        type="text"
                        placeholder="Ваше имя"
                        value={displayName}
                        onChange={(event) => setDisplayName(event.target.value)}
                        required
                        className="field"
                    />
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
                        minLength={8}
                        className="field"
                    />

                    {registerMutation.isError && (
                        <p className="auth-error">
                            {registerMutation.error?.message ?? "Ошибка регистрации"}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={registerMutation.isPending}
                        className="btn btn-dark btn-block btn-lg"
                        style={{ marginTop: 4 }}
                    >
                        {registerMutation.isPending ? "Создаём..." : "Зарегистрироваться"}
                    </button>
                </form>

                <div className="auth-or">
                    <span>или</span>
                </div>

                <GoogleLoginButton />

                <p className="auth-footer" style={{ marginTop: 22 }}>
                    Уже есть аккаунт?{" "}
                    <Link href="/login">Войти</Link>
                </p>
            </div>
        </div>
    );
}
