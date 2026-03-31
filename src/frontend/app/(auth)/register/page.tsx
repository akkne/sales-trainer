"use client";

import Link from "next/link";
import { useState } from "react";
import { useRegister } from "@/lib/hooks/useAuth";

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
        <div className="w-full max-w-sm">
            <h1 className="font-[var(--font-space-grotesk)] text-3xl font-bold text-gray-900 mb-8">
                Регистрация
            </h1>

            <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                <input
                    type="text"
                    placeholder="Ваше имя"
                    value={displayName}
                    onChange={(event) => setDisplayName(event.target.value)}
                    required
                    className="px-4 py-3 rounded-2xl bg-[#F7F7F7] text-gray-900 placeholder-gray-400 outline-none focus:ring-2 focus:ring-[#58CC02]"
                />
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
                    minLength={8}
                    className="px-4 py-3 rounded-2xl bg-[#F7F7F7] text-gray-900 placeholder-gray-400 outline-none focus:ring-2 focus:ring-[#58CC02]"
                />

                {registerMutation.isError && (
                    <p className="text-red-500 text-sm">
                        {registerMutation.error?.message ?? "Ошибка регистрации"}
                    </p>
                )}

                <button
                    type="submit"
                    disabled={registerMutation.isPending}
                    className="mt-2 py-3 rounded-2xl bg-[#58CC02] text-white font-bold shadow-[0_4px_0_#4CAD00] active:shadow-none active:translate-y-1 transition-transform disabled:opacity-60"
                >
                    {registerMutation.isPending ? "Создаём..." : "Создать аккаунт"}
                </button>
            </form>

            <p className="mt-6 text-center text-sm text-gray-500">
                Уже есть аккаунт?{" "}
                <Link href="/login" className="text-[#58CC02] font-semibold">
                    Войти
                </Link>
            </p>
        </div>
    );
}
