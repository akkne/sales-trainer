"use client";

import { relativeTimeRu } from "@/features/companies/lib/format";
import type { CompanyReadiness } from "@/features/companies/hooks/use-company-readiness";

interface CompanyReadinessCardProps {
    readiness: CompanyReadiness | undefined;
    isLoading: boolean;
    errorMessage: string | null;
    onRefresh: () => void;
}

const RING_SIZE = 84;
const RING_STROKE = 8;
const RING_RADIUS = (RING_SIZE - RING_STROKE) / 2;
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS;

function scoreColor(score: number): string {
    if (score >= 70) return "var(--success)";
    if (score >= 40) return "var(--warning, #d97706)";
    return "var(--heart)";
}

function ReadinessRing({ score }: { score: number }) {
    const clamped = Math.max(0, Math.min(100, score));
    const offset = RING_CIRCUMFERENCE * (1 - clamped / 100);
    const color = scoreColor(clamped);

    return (
        <svg
            width={RING_SIZE}
            height={RING_SIZE}
            viewBox={`0 0 ${RING_SIZE} ${RING_SIZE}`}
            role="img"
            aria-label={`Готовность к звонку: ${clamped} из 100`}
        >
            <circle
                cx={RING_SIZE / 2}
                cy={RING_SIZE / 2}
                r={RING_RADIUS}
                fill="none"
                stroke="var(--line)"
                strokeWidth={RING_STROKE}
            />
            <circle
                cx={RING_SIZE / 2}
                cy={RING_SIZE / 2}
                r={RING_RADIUS}
                fill="none"
                stroke={color}
                strokeWidth={RING_STROKE}
                strokeLinecap="round"
                strokeDasharray={RING_CIRCUMFERENCE}
                strokeDashoffset={offset}
                transform={`rotate(-90 ${RING_SIZE / 2} ${RING_SIZE / 2})`}
            />
            <text
                x="50%"
                y="50%"
                textAnchor="middle"
                dominantBaseline="central"
                fontSize="20"
                fontWeight="800"
                fill="var(--ink-heading)"
            >
                {clamped}
            </text>
        </svg>
    );
}

export function CompanyReadinessCard({
    readiness,
    isLoading,
    errorMessage,
    onRefresh,
}: CompanyReadinessCardProps) {
    const hasScore = readiness?.score !== null && readiness?.score !== undefined;

    return (
        <div className="co-card">
            <div className="co-card-head">
                <span className="eyebrow">ГОТОВНОСТЬ К ЗВОНКУ</span>
                {hasScore && (
                    <button className="btn-link" onClick={onRefresh}>
                        Обновить
                    </button>
                )}
            </div>

            {isLoading ? (
                <p className="small">Загрузка…</p>
            ) : hasScore ? (
                <>
                    <div className="co-readiness-body">
                        <ReadinessRing score={readiness!.score!} />
                        <div className="co-readiness-details">
                            {!!readiness!.strengths?.length && (
                                <div className="co-readiness-group">
                                    <p className="co-readiness-label">Сильные стороны</p>
                                    <ul className="co-readiness-list">
                                        {readiness!.strengths!.map((strength, index) => (
                                            <li key={index}>{strength}</li>
                                        ))}
                                    </ul>
                                </div>
                            )}
                            {!!readiness!.gaps?.length && (
                                <div className="co-readiness-group">
                                    <p className="co-readiness-label">Что подтянуть</p>
                                    <ul className="co-readiness-list">
                                        {readiness!.gaps!.map((gap, index) => (
                                            <li key={index}>{gap}</li>
                                        ))}
                                    </ul>
                                </div>
                            )}
                            {readiness!.recommendation && (
                                <p className="small" style={{ color: "var(--ink-2)" }}>
                                    {readiness!.recommendation}
                                </p>
                            )}
                        </div>
                    </div>
                    {readiness!.generatedAt && (
                        <p className="small" style={{ color: "var(--ink-3)", marginTop: 8 }}>
                            Обновлено {relativeTimeRu(readiness!.generatedAt)}
                        </p>
                    )}
                    {errorMessage && (
                        <p className="small" style={{ color: "var(--heart)", marginTop: 8 }}>{errorMessage}</p>
                    )}
                </>
            ) : (
                <div className="co-desc-empty">
                    <span>Проведите тренировку, чтобы получить оценку готовности.</span>
                    {errorMessage && (
                        <p className="small" style={{ color: "var(--heart)", marginTop: 8 }}>{errorMessage}</p>
                    )}
                </div>
            )}
        </div>
    );
}
