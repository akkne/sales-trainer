"use client";

export type ProgressTone = "rust" | "olive" | "indigo" | "ink";

interface ProgressProps {
    value: number;
    max?: number;
    tone?: ProgressTone;
    height?: number;
    showLabel?: boolean;
    className?: string;
}

const TONE_COLORS: Record<ProgressTone, string> = {
    rust: "var(--flame)",
    olive: "var(--success)",
    indigo: "linear-gradient(90deg, var(--primary), var(--violet))",
    ink: "var(--ink)",
};

/* Stroke colors for SVG circular progress (gradients not supported there) */
const TONE_STROKE: Record<ProgressTone, string> = {
    rust: "var(--flame)",
    olive: "var(--success)",
    indigo: "var(--primary)",
    ink: "var(--ink)",
};

export function Progress({
    value,
    max = 100,
    tone = "indigo",
    height = 6,
    showLabel = false,
    className = "",
}: ProgressProps) {
    const pct = Math.min(100, Math.max(0, (value / max) * 100));

    return (
        <div className={className} style={{ width: "100%" }}>
            <div
                style={{
                    width: "100%",
                    height: `${height}px`,
                    background: "var(--bg-2)",
                    borderRadius: height,
                    overflow: "hidden",
                    position: "relative",
                }}
                role="progressbar"
                aria-valuenow={value}
                aria-valuemin={0}
                aria-valuemax={max}
            >
                <div
                    style={{
                        width: `${pct}%`,
                        height: "100%",
                        background: TONE_COLORS[tone],
                        borderRadius: height,
                        transition: "width 0.6s cubic-bezier(.2,.8,.2,1)",
                    }}
                />
            </div>
            {showLabel && (
                <div
                    style={{
                        marginTop: "4px",
                        fontSize: "11px",
                        color: "var(--ink-3)",
                        fontFamily: "var(--f-mono)",
                    }}
                >
                    {value}/{max}
                </div>
            )}
        </div>
    );
}

interface CircularProgressProps {
    value: number;
    max?: number;
    size?: number;
    strokeWidth?: number;
    tone?: ProgressTone;
    showLabel?: boolean;
    label?: string;
    className?: string;
}

export function CircularProgress({
    value,
    max = 100,
    size = 48,
    strokeWidth = 4,
    tone = "indigo",
    showLabel = false,
    label,
    className = "",
}: CircularProgressProps) {
    const percentage = Math.min(100, Math.max(0, (value / max) * 100));
    const radius = (size - strokeWidth) / 2;
    const circumference = radius * 2 * Math.PI;
    const strokeDashoffset = circumference - (percentage / 100) * circumference;
    const displayLabel = label ?? `${Math.round(percentage)}%`;

    return (
        <div
            className={className}
            style={{
                position: "relative",
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                width: size,
                height: size,
            }}
        >
            <svg
                width={size}
                height={size}
                style={{ transform: "rotate(-90deg)" }}
                role="progressbar"
                aria-valuenow={value}
                aria-valuemin={0}
                aria-valuemax={max}
            >
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    stroke="var(--bg-2)"
                    strokeWidth={strokeWidth}
                />
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    stroke={TONE_STROKE[tone]}
                    strokeWidth={strokeWidth}
                    strokeLinecap="round"
                    strokeDasharray={circumference}
                    strokeDashoffset={strokeDashoffset}
                    style={{ transition: "stroke-dashoffset 0.5s ease" }}
                />
            </svg>
            {showLabel && (
                <span
                    style={{
                        position: "absolute",
                        fontSize: "12px",
                        fontWeight: 600,
                        color: "var(--ink)",
                        fontFamily: "var(--f-mono)",
                    }}
                >
                    {displayLabel}
                </span>
            )}
        </div>
    );
}

interface StepProgressProps {
    currentStep: number;
    totalSteps: number;
    className?: string;
}

export function StepProgress({
    currentStep,
    totalSteps,
    className = "",
}: StepProgressProps) {
    return (
        <div
            className={className}
            style={{ display: "flex", alignItems: "center", gap: "6px" }}
        >
            {Array.from({ length: totalSteps }).map((_, i) => (
                <div
                    key={i}
                    style={{
                        width: i === currentStep - 1 ? 28 : 8,
                        height: 8,
                        borderRadius: 4,
                        background: i < currentStep ? "var(--ink)" : "var(--line-2)",
                        transition: "width 0.3s cubic-bezier(.2,.8,.2,1), background 0.3s",
                    }}
                />
            ))}
            <span
                style={{
                    marginLeft: 12,
                    fontFamily: "var(--f-mono)",
                    fontSize: 12,
                    color: "var(--ink-3)",
                }}
            >
                {String(currentStep).padStart(2, "0")} / {String(totalSteps).padStart(2, "0")}
            </span>
        </div>
    );
}
