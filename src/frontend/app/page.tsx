"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuthStore } from "@/stores/auth-store";
import { apiClient } from "@/shared/api/api-client";

const FEATURE_LIST = [
    {
        emoji: "🎯",
        title: "Реальные сценарии",
        description: "Упражнения на основе настоящих возражений и ситуаций из продаж",
    },
    {
        emoji: "🤖",
        title: "AI-оценка",
        description: "GPT-4 анализирует твои ответы и даёт персональный фидбек",
    },
    {
        emoji: "🔥",
        title: "Стрики и лиги",
        description: "Соревнуйся с другими, набирай XP и поднимайся по лигам",
    },
    {
        emoji: "📚",
        title: "Справочник техник",
        description: "Все ключевые техники продаж — под рукой, с примерами",
    },
];

export default function LandingPage() {
    const router = useRouter();
    const { accessToken, setAccessToken, setAuthenticatedUser } = useAuthStore();

    useEffect(() => {
        if (accessToken) {
            router.replace("/tree");
        }
    }, [accessToken, router]);

    async function startDemoSession() {
        const response = await apiClient.post<{
            accessToken: string;
            expiresInSeconds: number;
        }>("/demo/token", {});

        setAccessToken(response.accessToken);
        setAuthenticatedUser({
            id: "demo",
            email: "demo@sallevate.app",
            displayName: "Demo User",
            isOnboardingCompleted: false,
            role: "User",
        });

        router.push("/onboarding");
    }

    return (
        <div className="min-h-screen bg-bg text-ink">
            <header className="max-w-4xl mx-auto px-4 py-6 flex items-center justify-between">
                <Wordmark size={22} />
                <Link
                    href="/login"
                    className="text-sm font-semibold text-ink-3 hover:text-ink transition-colors"
                >
                    Войти
                </Link>
            </header>

            <main className="max-w-4xl mx-auto px-4">
                <div className="text-center py-16">
                    <div className="text-6xl mb-6">🚀</div>
                    <h1 className="text-4xl font-bold text-ink mb-4 leading-tight tracking-tight">
                        Прокачай продажи
                        <br />
                        <span className="text-rust">за 5 минут в день</span>
                    </h1>
                    <p className="text-lg text-ink-3 mb-10 max-w-md mx-auto">
                        Тренажёр навыков в стиле Duolingo. Реальные сценарии,
                        AI-фидбек и соревнования с коллегами.
                    </p>

                    <div className="flex flex-col sm:flex-row gap-4 justify-center">
                        <Link
                            href="/register"
                            className="px-8 py-4 rounded-2xl bg-ink text-bg font-bold text-lg active:translate-y-px transition-transform"
                            style={{ boxShadow: "var(--sh-2)" }}
                        >
                            Начать бесплатно
                        </Link>
                        <button
                            onClick={startDemoSession}
                            className="px-8 py-4 rounded-2xl bg-surface border border-line text-ink-2 font-bold text-lg hover:bg-bg-2 transition-colors"
                            style={{ boxShadow: "var(--sh-1)" }}
                        >
                            Попробовать без регистрации
                        </button>
                    </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pb-16">
                    {FEATURE_LIST.map((featureItem) => (
                        <div
                            key={featureItem.title}
                            className="bg-surface border border-line rounded-2xl p-6"
                            style={{ boxShadow: "var(--sh-1)" }}
                        >
                            <span className="text-3xl mb-3 block">{featureItem.emoji}</span>
                            <h3 className="font-bold text-ink mb-2">
                                {featureItem.title}
                            </h3>
                            <p className="text-sm text-ink-3">{featureItem.description}</p>
                        </div>
                    ))}
                </div>
            </main>
        </div>
    );
}
