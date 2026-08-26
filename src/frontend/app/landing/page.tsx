// Deliberately not auth-aware: `/landing` is the public marketing page and stays reachable for
// everyone, signed in or not (a signed-in РОП still needs to show it to their team). The
// "already signed in → go to the app" hop lives on the default path instead — see `app/page.tsx`.
import Link from "next/link";
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
        title: "Разбор каждой попытки",
        description: "ИИ объясняет, что сработало, а что стоило сказать иначе.",
    },
    {
        icon: "book",
        tint: "amber",
        title: "Справочник техник",
        description: "SPIN, ценовое якорение, работа с возражениями — с примерами.",
    },
];

export default function LandingPage() {
    return (
        <div className="landing">
            <div className="app-backdrop" />
            <header className="land-top container">
                <Wordmark size={28} />
                <div className="row gap-2">
                    <Link href="/login" className="btn btn-ghost">
                        Войти
                    </Link>
                    <Link href="/demo" className="btn btn-primary btn-sm">
                        Запросить демо
                    </Link>
                </div>
            </header>

            <div className="container land-hero">
                <span
                    className="badge"
                    style={{
                        background: "var(--primary-soft)",
                        color: "var(--primary-ink)",
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
                    и получай разбор каждой реплики.
                </p>

                <div className="row gap-3 center wrap">
                    <Link href="/demo" className="btn btn-primary btn-lg">
                        Запросить демо
                        <Icon name="arrow-right" size={18} />
                    </Link>
                    <Link href="/login" className="btn btn-outline btn-lg">
                        Войти
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

                <div className="card card-pad land-cta-band">
                    <h2 className="h4" style={{ fontSize: 22, margin: "0 0 8px" }}>
                        Готовы показать команде, на что способен Sellevate?
                    </h2>
                    <p className="small" style={{ marginBottom: 20 }}>
                        Оставьте заявку — подберём удобное время и разберём ваши сценарии продаж.
                    </p>
                    <Link href="/demo" className="btn btn-primary btn-lg">
                        Запросить демо
                        <Icon name="arrow-right" size={18} />
                    </Link>
                </div>
            </div>
        </div>
    );
}
