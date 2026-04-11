"use client";

import Link from "next/link";
import { useState } from "react";
import { useLogin } from "@/lib/hooks/useAuth";
import { GoogleLoginButton } from "@/components/ui/GoogleLoginButton";
import { Icon } from "@/components/ui/Icon";

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
                <Icon name="psychology" size="xl" className="text-primary" />
                <span className="font-headline font-bold text-2xl text-primary">Sallevate</span>
            </div>

            <h1 className="font-headline text-3xl font-bold text-on-surface mb-2 text-center">
                Войти
            </h1>
            <p className="text-on-surface-variant text-center mb-8">
                Продолжи развивать навыки продаж
            </p>

            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                <input
                    type="email"
                    placeholder="Email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    required
                    className="px-4 py-3 rounded-2xl bg-surface-container-low text-on-surface placeholder-on-surface-variant outline-none focus:ring-2 focus:ring-primary border-2 border-transparent focus:border-primary tonal-transition"
                />
                <input
                    type="password"
                    placeholder="Пароль"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    required
                    className="px-4 py-3 rounded-2xl bg-surface-container-low text-on-surface placeholder-on-surface-variant outline-none focus:ring-2 focus:ring-primary border-2 border-transparent focus:border-primary tonal-transition"
                />

                {loginMutation.isError && (
                    <p className="text-error text-sm">
                        {loginMutation.error?.message ?? "Ошибка входа"}
                    </p>
                )}

                <button
                    type="submit"
                    disabled={loginMutation.isPending}
                    className="mt-2 py-3 rounded-full bg-primary text-on-primary font-bold shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 transition-transform disabled:opacity-60 flex items-center justify-center gap-2"
                >
                    {loginMutation.isPending ? "Входим..." : "Войти"}
                </button>
            </form>

            <div className="mt-6 flex items-center gap-3">
                <div className="flex-1 h-px bg-outline-variant" />
                <span className="text-xs text-on-surface-variant">или</span>
                <div className="flex-1 h-px bg-outline-variant" />
            </div>

            <div className="mt-4">
                <GoogleLoginButton />
            </div>

            <p className="mt-8 text-center text-sm text-on-surface-variant">
                Нет аккаунта?{" "}
                <Link href="/register" className="text-primary font-semibold hover:underline">
                    Зарегистрироваться
                </Link>
            </p>
        </div>
    );
}
