"use client";

import Link from "next/link";
import { useState } from "react";
import { useLogin } from "@/features/auth/hooks/use-auth";
import { GoogleLoginButton } from "@/shared/components/google-login-button";
import { Icon } from "@/shared/components/icon";

export default function LoginPage() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const loginMutation = useLogin();

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        loginMutation.mutate({ email, password });
    }

    return (
        <div className="w-full max-w-sm px-4">
            {/* Brand header */}
            <div className="flex items-center justify-center gap-2 mb-8">
                <Icon name="sparkle" size="xl" className="text-ink" />
                <span className="font-bold text-2xl text-ink">Sellevate</span>
            </div>

            <h1 className="text-3xl font-bold text-ink mb-2 text-center">
                Войти
            </h1>
            <p className="text-ink-3 text-center mb-8">
                Продолжи развивать навыки продаж
            </p>

            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                <input
                    type="email"
                    placeholder="Email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    required
                    className="px-4 py-3 rounded-2xl bg-surface text-ink placeholder:text-ink-4 outline-none focus:ring-2 focus:ring-indigo/30 border border-line focus:border-indigo transition-colors"
                />
                <input
                    type="password"
                    placeholder="Пароль"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    required
                    className="px-4 py-3 rounded-2xl bg-surface text-ink placeholder:text-ink-4 outline-none focus:ring-2 focus:ring-indigo/30 border border-line focus:border-indigo transition-colors"
                />

                {loginMutation.isError && (
                    <p className="text-bad text-sm">
                        {loginMutation.error?.message ?? "Ошибка входа"}
                    </p>
                )}

                <button
                    type="submit"
                    disabled={loginMutation.isPending}
                    className="mt-2 py-3 rounded-full bg-ink text-bg font-bold active:translate-y-px transition-transform disabled:opacity-60 flex items-center justify-center gap-2"
                >
                    {loginMutation.isPending ? "Входим..." : "Войти"}
                </button>
            </form>

            <div className="mt-6 flex items-center gap-3">
                <div className="flex-1 h-px bg-line" />
                <span className="text-xs text-ink-3">или</span>
                <div className="flex-1 h-px bg-line" />
            </div>

            <div className="mt-4">
                <GoogleLoginButton />
            </div>

            <p className="mt-8 text-center text-sm text-ink-3">
                Нет аккаунта?{" "}
                <Link href="/register" className="text-ink font-semibold hover:underline">
                    Зарегистрироваться
                </Link>
            </p>
        </div>
    );
}
