"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuthStore } from "@/shared/stores/auth-store";
import { Icon } from "@/shared/components/icon";
import type { IconName } from "@/shared/components/icon";
import { Wordmark } from "@/shared/components/wordmark";

type FeatureTint = "primary" | "success" | "violet" | "amber";

const FEATURE_LIST: {
    icon: IconName;
    tint: FeatureTint;
    title: string;
    description: string;
}[] = [
    {
        icon: "phone",
        tint: "primary",
        title: "Реальные сценарии",
        description: "ИИ-клиент спорит, сомневается и перебивает — совсем как настоящий ЛПР.",
    },
    {
        icon: "mic",
        tint: "success",
        title: "Голосовые звонки",
        description: "Отрабатывай холодные звонки голосом и получай разбор каждой реплики.",
    },
    {
        icon: "zap",
        tint: "violet",
        title: "XP, серии и лиги",
        description: "Игровые механики держат в тонусе и превращают навыки в привычку.",
    },
    {
        icon: "book",
        tint: "amber",
        title: "Справочник техник",
        description: "SPIN, ценовое якорение, работа с возражениями — с примерами.",
    },
];

export default function LandingPage() {
    const router = useRouter();
    const { accessToken } = useAuthStore();

    useEffect(() => {
        if (accessToken) {
            router.replace("/tree");
        }
    }, [accessToken, router]);

    return (
        <div className="landing">
            <div className="app-backdrop" />
            <header className="land-top container">
                <Wordmark size={28} />
                <Link href="/login" className="btn btn-ghost">
                    Войти
                </Link>
            </header>

            <div className="container land-hero">
                <span
                    className="badge"
                    style={{
                        background: "var(--primary-soft)",
                        color: "var(--primary)",
                        padding: "7px 14px",
                        fontSize: 13,
                    }}
                >
                    <Icon name="bolt" size={15} />
                    Тренажёр продаж нового поколения
                </span>

                <h1 className="display land-title">
                    Прокачай свои продажи
                    <br />
                    <span className="grad-text">за 5 минут</span> в день
                </h1>

                <p
                    className="lead"
                    style={{ maxWidth: 560, margin: "0 auto 32px", textWrap: "pretty" }}
                >
                    Учись на реальных диалогах с ИИ, отрабатывай голосовые звонки
                    и поднимайся в лигах вместе с другими продавцами.
                </p>

                <div className="row gap-3 center wrap">
                    <Link href="/register" className="btn btn-dark btn-lg">
                        Начать бесплатно
                        <Icon name="arrow-right" size={18} />
                    </Link>
                </div>

                <div className="land-features">
                    {FEATURE_LIST.map((f) => (
                        <div key={f.title} className="card card-pad lift land-feat">
                            <span className={"itile " + f.tint} style={{ width: 50, height: 50 }}>
                                <Icon name={f.icon} size={26} />
                            </span>
                            <h4 className="h4" style={{ margin: "16px 0 8px" }}>
                                {f.title}
                            </h4>
                            <p className="small" style={{ textWrap: "pretty" }}>
                                {f.description}
                            </p>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
