"use client";

import Link from "next/link";
import { useState } from "react";
import { useRegister } from "@/features/auth/hooks/use-auth";
import { GoogleLoginButton } from "@/shared/components/google-login-button";
import { Icon } from "@/shared/components/icon";

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
        <div className="w-full max-w-sm px-4">
            {/* Brand header */}
            <div className="flex items-center justify-center gap-2 mb-8">
                <Icon name="sparkle" size="xl" className="text-ink" />
                <span className="font-bold text-2xl text-ink">Sellevate</span>
            </div>

            <h1 className="text-3xl font-bold text-ink mb-2 text-center">
                Регистрация
            </h1>
            <p className="text-ink-3 text-center mb-8">
                Начни путь к мастерству продаж
            </p>

            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                <input
                    type="text"
                    placeholder="Ваше имя"
                    value={displayName}
                    onChange={(event) => setDisplayName(event.target.value)}
                    required
                    className="px-4 py-3 rounded-2xl bg-surface text-ink placeholder:text-ink-4 outline-none focus:ring-2 focus:ring-indigo/30 border border-line focus:border-indigo transition-colors"
                />
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
                    minLength={8}
                    className="px-4 py-3 rounded-2xl bg-surface text-ink placeholder:text-ink-4 outline-none focus:ring-2 focus:ring-indigo/30 border border-line focus:border-indigo transition-colors"
                />

                {registerMutation.isError && (
                    <p className="text-bad text-sm">
                        {registerMutation.error?.message ?? "Ошибка регистрации"}
                    </p>
                )}

                <button
                    type="submit"
                    disabled={registerMutation.isPending}
                    className="mt-2 py-3 rounded-full bg-ink text-bg font-bold active:translate-y-px transition-transform disabled:opacity-60 flex items-center justify-center gap-2"
                >
                    {registerMutation.isPending ? "Создаём..." : "Создать аккаунт"}
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
                Уже есть аккаунт?{" "}
                <Link href="/login" className="text-ink font-semibold hover:underline">
                    Войти
                </Link>
            </p>
        </div>
    );
}
