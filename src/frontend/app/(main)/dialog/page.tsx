"use client";

import { useDialogBundles } from "@/features/dialog/hooks/use-dialog";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { Skeleton, GeoAvatar, StatTile, ErrorState } from "@/shared/components";
import Link from "next/link";

export default function DialogPage() {
    const { data: bundles, isLoading, error, refetch } = useDialogBundles();

    if (isLoading) {
        return (
            <div className="page">
                <div className="container">
                    <div className="hero-head">
                        <div className="hh-left">
                            <Skeleton width={220} height={14} />
                            <Skeleton width={420} height={44} style={{ marginTop: 14 }} />
                            <Skeleton width={360} height={16} style={{ marginTop: 12 }} />
                        </div>
                    </div>
                    <div className="bundle-grid">
                        {[1, 2, 3, 4].map((i) => (
                            <Skeleton key={i} height={180} rounded={16} />
                        ))}
                    </div>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="page" style={{ padding: "60px 24px" }}>
                <ErrorState
                    title="Ошибка загрузки"
                    message={error.message}
                    onRetry={() => refetch()}
                />
            </div>
        );
    }

    if (!bundles || bundles.length === 0) {
        return (
            <div className="page container">
                <div className="empty" style={{ paddingTop: 120 }}>
                    <div className="ic">
                        <Icon name="message" size="lg" />
                    </div>
                    <h1 className="h3" style={{ marginBottom: 8 }}>Практика диалогов пока недоступна</h1>
                    <p className="small">Функция находится в разработке или не настроена</p>
                </div>
            </div>
        );
    }

    return (
        <div className="page">
            <div className="container">
                {/* Hero header */}
                <div className="hero-head">
                    <div className="hh-left fade-up">
                        <span className="eyebrow">
                            AI Диалог<span className="dot">·</span>
                            <span>{bundles.length} {bundles.length === 1 ? "модуль" : bundles.length < 5 ? "модуля" : "модулей"}</span>
                        </span>
                        <h1 className="h1 hh-title">
                            Мастерство <span className="grad-text">разговора</span>.
                        </h1>
                        <p className="lead">
                            Интерактивные сценарии для отработки техник продаж. AI-клиент реагирует
                            как настоящий — спорит, сомневается, перебивает.
                        </p>
                    </div>
                    <div className="hero-stats fade-up">
                        <StatTile label="Диалогов" value="—" icon={<Icon name="message" size="xs" />} tone="primary" />
                        <StatTile label="Средний балл" value="—" unit="/10" icon={<Icon name="star" size="xs" />} tone="amber" />
                    </div>
                </div>

                {/* Bundles grid */}
                <div className="bundle-grid">
                    {bundles.map((bundle) => (
                        <Link key={bundle.id} href={`/dialog/${bundle.id}`} style={{ textDecoration: "none", color: "inherit", display: "flex" }}>
                            <div className="card card-pad lift bundle-card" style={{ flex: 1 }}>
                                <span className="itile primary" style={{ width: 56, height: 56, fontSize: 28 }}>
                                    {bundle.iconEmoji || "💬"}
                                </span>
                                <h3 className="h3" style={{ margin: "16px 0 8px" }}>{bundle.title}</h3>
                                <p className="body" style={{ flex: 1 }}>{bundle.description}</p>
                                <div className="row between" style={{ marginTop: 16 }}>
                                    <span className="chip">режимы внутри</span>
                                    <span className="row gap-1" style={{ color: "var(--primary)", fontWeight: 700, fontSize: 14 }}>
                                        Открыть <Icon name="arrow-right" size={18} />
                                    </span>
                                </div>
                            </div>
                        </Link>
                    ))}
                </div>

                {/* NPC Mentor card */}
                <div className="mentor-card">
                    <div className="mentor-tex" />
                    <GeoAvatar seed="sergey" size={84} />
                    <div className="grow" style={{ position: "relative" }}>
                        <h3 className="h3" style={{ color: "#fff" }}>Skeptic Sergey</h3>
                        <span className="eyebrow" style={{ color: "var(--violet)" }}>VP · возражения</span>
                        <p className="body" style={{ color: "#cbd5e1", margin: "10px 0 0", maxWidth: 560 }}>
                            «Хочешь, позвоню и попробую развалить твою лучшую продажу? Пять минут на подготовку.»
                        </p>
                    </div>
                    <Button variant="accent" size="lg" iconRightName="phone">
                        CHALLENGE
                    </Button>
                </div>
                <div style={{ height: 60 }} />
            </div>
        </div>
    );
}
