"use client";

import ReactMarkdown from "react-markdown";
import { relativeTimeRu } from "@/features/companies/lib/format";
import type { CompanyBriefing } from "@/features/companies/hooks/use-company-briefing";

interface CompanyBriefingCardProps {
    briefing: CompanyBriefing | undefined;
    isLoading: boolean;
    isGenerating: boolean;
    errorMessage: string | null;
    onGenerate: () => void;
}

export function CompanyBriefingCard({
    briefing,
    isLoading,
    isGenerating,
    errorMessage,
    onGenerate,
}: CompanyBriefingCardProps) {
    const hasContent = !!briefing?.content;

    return (
        <div className="co-card">
            <div className="co-card-head">
                <span className="eyebrow">ШПАРГАЛКА К ЗВОНКУ</span>
                {hasContent && (
                    <button className="btn-link" onClick={onGenerate} disabled={isGenerating}>
                        {isGenerating ? "Генерируем…" : "Обновить"}
                    </button>
                )}
            </div>

            {isLoading ? (
                <p className="small">Загрузка…</p>
            ) : hasContent ? (
                <>
                    <div style={{ fontSize: 13.5, color: "var(--ink-2)", lineHeight: 1.6 }}>
                        <ReactMarkdown>{briefing!.content}</ReactMarkdown>
                    </div>
                    {briefing!.generatedAt && (
                        <p className="small" style={{ color: "var(--ink-3)", marginTop: 8 }}>
                            Обновлено {relativeTimeRu(briefing!.generatedAt)}
                        </p>
                    )}
                    {errorMessage && (
                        <p className="small" style={{ color: "var(--heart)", marginTop: 8 }}>{errorMessage}</p>
                    )}
                </>
            ) : (
                <div className="co-desc-empty">
                    <span>
                        Сгенерируйте краткую справку перед звонком: кто они, о чём договаривались,
                        возможные возражения и следующий шаг.
                    </span>
                    <button className="btn btn-soft" onClick={onGenerate} disabled={isGenerating}>
                        {isGenerating ? "Генерируем…" : "Сгенерировать"}
                    </button>
                    {errorMessage && (
                        <p className="small" style={{ color: "var(--heart)", marginTop: 8 }}>{errorMessage}</p>
                    )}
                </div>
            )}
        </div>
    );
}
