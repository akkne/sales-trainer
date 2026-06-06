"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useDialogBundles, useDialogModes } from "@/features/dialog/hooks/use-dialog";
import { Icon } from "@/shared/components/icon";

export default function BundleModesPage() {
    const params = useParams();
    const router = useRouter();
    const bundleId = params.bundleId as string;

    const { data: bundles } = useDialogBundles();
    const { data: modes, isLoading, error } = useDialogModes(bundleId);

    const currentBundle = bundles?.find((bundle) => bundle.id === bundleId);

    if (isLoading) {
        return (
            <div className="row center" style={{ minHeight: "60vh" }}>
                <div style={{ width: 40, height: 40, borderRadius: "50%", border: "4px solid var(--primary)", borderTopColor: "transparent", animation: "spin 0.8s linear infinite" }} />
            </div>
        );
    }

    if (error) {
        return (
            <div className="page container">
                <div className="empty" style={{ paddingTop: 100 }}>
                    <div className="ic" style={{ background: "var(--heart-soft)", color: "var(--heart)" }}>
                        <Icon name="close" size="lg" />
                    </div>
                    <h1 className="h3" style={{ marginBottom: 8 }}>Ошибка загрузки</h1>
                    <p className="small">{error.message}</p>
                </div>
            </div>
        );
    }

    return (
        <div className="page">
            <div className="container">
                <button className="back-link" onClick={() => router.push("/dialog")}>
                    <Icon name="chevron-left" size={20} />
                    Назад к диалогам
                </button>

                <div className="row gap-4 wrap" style={{ padding: "24px 0 12px" }}>
                    <span className="itile primary" style={{ width: 72, height: 72, fontSize: 36 }}>
                        {currentBundle?.iconEmoji || "💬"}
                    </span>
                    <div>
                        <span className="eyebrow">Выбери режим</span>
                        <h1 className="h1" style={{ margin: "8px 0 6px", fontSize: "clamp(28px, 3.6vw, 44px)" }}>
                            {currentBundle?.title || "Режимы практики"}
                        </h1>
                        {currentBundle?.description && (
                            <p className="lead">{currentBundle.description}</p>
                        )}
                    </div>
                </div>

                {(!modes || modes.length === 0) ? (
                    <div className="empty">
                        <div className="ic">
                            <Icon name="message" size="lg" />
                        </div>
                        <p className="h4" style={{ marginBottom: 4 }}>Режимы пока не добавлены</p>
                        <p className="small">Администратор ещё не настроил сценарии</p>
                    </div>
                ) : (
                    <div className="mode-grid">
                        {modes.map((mode) => (
                            <div key={mode.id} className="card card-pad mode-card">
                                <h3 className="h3">{mode.title}</h3>
                                <p className="body" style={{ margin: "8px 0 20px", flex: 1 }}>
                                    {mode.description}
                                </p>
                                <div className="row gap-3">
                                    <Link href={`/dialog/${bundleId}/${mode.id}`} className="btn btn-outline">
                                        <Icon name="message" size={18} />
                                        Чат
                                    </Link>
                                    {mode.voiceEnabled && (
                                        <Link href={`/dialog/${bundleId}/${mode.id}/voice`} className="btn btn-success grow">
                                            <Icon name="phone" size={18} />
                                            Позвонить
                                        </Link>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                )}
                <div style={{ height: 60 }} />
            </div>
        </div>
    );
}
