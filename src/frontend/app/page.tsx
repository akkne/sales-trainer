"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuthStore } from "@/lib/store/authStore";
import { apiClient } from "@/lib/api/apiClient";

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
        <div className="min-h-screen bg-white">
            <header className="max-w-4xl mx-auto px-4 py-6 flex items-center justify-between">
                <span className="font-[var(--font-space-grotesk)] font-bold text-xl text-gray-900">
                    Sallevate
                </span>
                <Link
                    href="/login"
                    className="text-sm font-semibold text-gray-600 hover:text-gray-900"
                >
                    Войти
                </Link>
            </header>

            <main className="max-w-4xl mx-auto px-4">
                <div className="text-center py-16">
                    <div className="text-6xl mb-6">🚀</div>
                    <h1 className="font-[var(--font-space-grotesk)] text-4xl font-bold text-gray-900 mb-4 leading-tight">
                        Прокачай продажи
                        <br />
                        <span className="text-[#58CC02]">за 5 минут в день</span>
                    </h1>
                    <p className="text-lg text-gray-500 mb-10 max-w-md mx-auto">
                        Тренажёр навыков в стиле Duolingo. Реальные сценарии,
                        AI-фидбек и соревнования с коллегами.
                    </p>

                    <div className="flex flex-col sm:flex-row gap-4 justify-center">
                        <Link
                            href="/register"
                            className="px-8 py-4 rounded-2xl bg-[#58CC02] text-white font-bold text-lg shadow-[0_4px_0_#4CAD00] active:shadow-none active:translate-y-1 transition-transform"
                        >
                            Начать бесплатно
                        </Link>
                        <button
                            onClick={startDemoSession}
                            className="px-8 py-4 rounded-2xl bg-[#F7F7F7] text-gray-700 font-bold text-lg hover:bg-[#E8F9D6] transition-colors"
                        >
                            Попробовать без регистрации
                        </button>
                    </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pb-16">
                    {FEATURE_LIST.map((featureItem) => (
                        <div
                            key={featureItem.title}
                            className="bg-[#F7F7F7] rounded-2xl p-6"
                        >
                            <span className="text-3xl mb-3 block">{featureItem.emoji}</span>
                            <h3 className="font-[var(--font-space-grotesk)] font-bold text-gray-900 mb-2">
                                {featureItem.title}
                            </h3>
                            <p className="text-sm text-gray-500">{featureItem.description}</p>
                        </div>
                    ))}
                </div>
            </main>
        </div>
    );
}
