"use client";

import Link from "next/link";
import { useState } from "react";
import { useLogin } from "@/lib/hooks/useAuth";
import { GoogleLoginButton } from "@/components/ui/GoogleLoginButton";

export default function LoginPage() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const loginMutation = useLogin();

    function handleSubmit(event: React.FormEvent) {
        event.preventDefault();
        loginMutation.mutate({ email, password });
    }

    return (
        <div className="w-full max-w-sm">
            <h1 className="font-[var(--font-space-grotesk)] text-3xl font-bold text-gray-900 mb-8">
                Войти
            </h1>

            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                <input
                    type="email"
                    placeholder="Email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    required
                    className="px-4 py-3 rounded-2xl bg-[#F7F7F7] text-gray-900 placeholder-gray-400 outline-none focus:ring-2 focus:ring-[#58CC02]"
                />
                <input
                    type="password"
                    placeholder="Пароль"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    required
                    className="px-4 py-3 rounded-2xl bg-[#F7F7F7] text-gray-900 placeholder-gray-400 outline-none focus:ring-2 focus:ring-[#58CC02]"
                />

                {loginMutation.isError && (
                    <p className="text-red-500 text-sm">
                        {loginMutation.error?.message ?? "Ошибка входа"}
                    </p>
                )}

                <button
                    type="submit"
                    disabled={loginMutation.isPending}
                    className="mt-2 py-3 rounded-2xl bg-[#58CC02] text-white font-bold shadow-[0_4px_0_#4CAD00] active:shadow-none active:translate-y-1 transition-transform disabled:opacity-60"
                >
                    {loginMutation.isPending ? "Входим..." : "Войти"}
                </button>
            </form>

            <div className="mt-4 flex items-center gap-3">
                <div className="flex-1 h-px bg-gray-200" />
                <span className="text-xs text-gray-400">или</span>
                <div className="flex-1 h-px bg-gray-200" />
            </div>

            <div className="mt-4">
                <GoogleLoginButton />
            </div>

            <p className="mt-6 text-center text-sm text-gray-500">
                Нет аккаунта?{" "}
                <Link href="/register" className="text-[#58CC02] font-semibold">
                    Зарегистрироваться
                </Link>
            </p>
        </div>
    );
}
