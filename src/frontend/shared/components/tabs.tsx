"use client";

export interface TabItem {
    key: string;
    label: string;
    badge?: number | string;
}

interface TabsProps {
    items: TabItem[];
    activeKey: string;
    onChange: (key: string) => void;
    className?: string;
}

/**
 * One row of sibling views — the two kinds of review note (O7), the queues of an adaptation
 * package (O12, O13), the waves of an assignment (O4).
 *
 * A badge on a tab counts what is waiting behind it and is omitted rather than drawn as `0`:
 * a zero is the answer to a question nobody asked.
 */
export function Tabs({ items, activeKey, onChange, className = "" }: TabsProps) {
    return (
        <div
            role="tablist"
            className={`flex items-center gap-1 overflow-x-auto ${className}`}
            style={{ borderBottom: "1px solid var(--line)" }}
        >
            {items.map((item) => {
                const isActive = item.key === activeKey;
                return (
                    <button
                        key={item.key}
                        type="button"
                        role="tab"
                        aria-selected={isActive}
                        onClick={() => onChange(item.key)}
                        className={`inline-flex items-center gap-2 whitespace-nowrap px-3 py-2.5 text-sm transition-colors ${
                            isActive ? "text-ink font-medium" : "text-ink-3 hover:text-ink"
                        }`}
                        style={{
                            borderBottom: `2px solid ${isActive ? "var(--primary)" : "transparent"}`,
                            marginBottom: "-1px",
                        }}
                    >
                        {item.label}
                        {item.badge !== undefined && item.badge !== 0 && (
                            <span
                                className="tnum inline-flex items-center justify-center min-w-5 h-5 px-1.5 rounded-full text-[11px] font-semibold"
                                style={{
                                    background: isActive ? "var(--primary-soft)" : "var(--bg-2)",
                                    color: isActive ? "var(--primary-ink)" : "var(--ink-3)",
                                }}
                            >
                                {item.badge}
                            </span>
                        )}
                    </button>
                );
            })}
        </div>
    );
}
