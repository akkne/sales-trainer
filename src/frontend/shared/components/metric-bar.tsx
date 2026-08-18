"use client";

export type MetricBarTone = "neutral" | "success" | "amber" | "flame" | "violet" | "info";

interface MetricBarProps {
    value: number;
    limit: number;
    label: string;
    tone?: MetricBarTone;
    /** Renders both numbers; defaults to plain integers. */
    formatter?: (value: number) => string;
    className?: string;
}

const TONE_FILLS: Record<MetricBarTone, { fill: string; text: string }> = {
    neutral: { fill: "var(--ink-3)", text: "var(--ink-2)" },
    success: { fill: "var(--success)", text: "var(--success)" },
    amber: { fill: "var(--amber)", text: "var(--amber)" },
    flame: { fill: "var(--flame)", text: "var(--flame)" },
    violet: { fill: "var(--violet)", text: "var(--violet)" },
    info: { fill: "var(--info)", text: "var(--info)" },
};

function formatWholeNumber(value: number): string {
    return String(Math.round(value));
}

/**
 * One consumption against one ceiling: the three AI quotas of O17 and the micro-funnel on an
 * assignment row in O2.
 *
 * The fill never runs past the track even when the value exceeds the limit — an over-quota
 * organization is told so by the numbers, not by a bar drawn outside its own box. A limit of zero
 * means "no ceiling configured" and draws an empty track rather than a division by zero.
 */
export function MetricBar({
    value,
    limit,
    label,
    tone = "neutral",
    formatter = formatWholeNumber,
    className = "",
}: MetricBarProps) {
    const filledPercent = limit > 0 ? Math.min(100, Math.max(0, (value / limit) * 100)) : 0;
    const toneStyle = TONE_FILLS[tone];

    return (
        <div className={className} style={{ width: "100%" }}>
            <div className="flex items-baseline justify-between gap-3 mb-1.5">
                <span className="text-xs text-ink-3">{label}</span>
                <span
                    className="tnum text-xs font-semibold"
                    style={{ fontFamily: "var(--font-mono)", color: toneStyle.text }}
                >
                    {formatter(value)}
                    <span className="text-ink-3"> / {limit > 0 ? formatter(limit) : "—"}</span>
                </span>
            </div>
            <div
                role="progressbar"
                aria-label={label}
                aria-valuenow={value}
                aria-valuemin={0}
                aria-valuemax={limit}
                style={{
                    width: "100%",
                    height: "6px",
                    background: "var(--bg-2)",
                    borderRadius: "6px",
                    overflow: "hidden",
                }}
            >
                <div
                    style={{
                        width: `${filledPercent}%`,
                        height: "100%",
                        background: toneStyle.fill,
                        borderRadius: "6px",
                        transition: "width 0.4s cubic-bezier(.2,.8,.2,1)",
                    }}
                />
            </div>
        </div>
    );
}
