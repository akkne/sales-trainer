"use client";

import { useMemo } from "react";
import { alignComparisonBlocks } from "../utils/comparison-blocks";

export interface CompareColumn {
    key: string;
    title: string;
    subtitle: string;
    document: unknown;
}

interface ThreeWayCompareProps {
    columns: CompareColumn[];
    /** Rendered under the columns when the leftmost one is missing — techniques, справки, режимы. */
    missingBaseAtForkNotice?: string;
}

const EMPTY_CELL_LABEL = "— нет в этой версии";

/**
 * Three columns — база на момент копирования, ваша версия, база сейчас — or two, when the family
 * has no version history to point back at (docs/TENANCY/ADMIN_UI_DESIGN.md O15).
 *
 * **No diff is computed and none is displayed.** The highlight is block-level and comes from
 * `alignComparisonBlocks`, which only asks whether a block's text is identical across the columns.
 * There is deliberately no per-hunk marker and no button beside a highlighted block: the moment
 * either exists, «применить непротиворечивые куски» becomes the obvious next feature, and that is
 * the merge this whole model refuses to build.
 */
export function ThreeWayCompare({ columns, missingBaseAtForkNotice }: ThreeWayCompareProps) {
    const rows = useMemo(
        () => alignComparisonBlocks(columns.map((column) => column.document)),
        [columns]
    );

    if (rows.length === 0) {
        return (
            <p className="text-sm text-ink-3">
                Тексты для сравнения не пришли — сервер вернул пустые документы.
            </p>
        );
    }

    return (
        <div className="overflow-x-auto">
            <div className="min-w-[720px]">
                <div
                    className="grid gap-3 pb-3 border-b border-line"
                    style={{ gridTemplateColumns: `repeat(${columns.length}, minmax(0, 1fr))` }}
                >
                    {columns.map((column) => (
                        <div key={column.key}>
                            <h3 className="text-xs font-semibold uppercase tracking-wide text-ink-3">
                                {column.title}
                            </h3>
                            <p className="mt-0.5 text-xs text-ink-3">{column.subtitle}</p>
                        </div>
                    ))}
                </div>

                {rows.map((row) => (
                    <section key={row.key} className="border-b border-line py-3">
                        <div className="flex items-center gap-2 mb-2">
                            <h4 className="text-sm font-medium text-ink">{row.label}</h4>
                            {row.differs && (
                                <span
                                    className="text-xs rounded-full px-2 py-0.5"
                                    style={{ background: "var(--accent-soft)", color: "var(--accent-ink)" }}
                                >
                                    блок отличается
                                </span>
                            )}
                        </div>
                        <div
                            className="grid gap-3"
                            style={{ gridTemplateColumns: `repeat(${columns.length}, minmax(0, 1fr))` }}
                        >
                            {row.cells.map((cell, cellIndex) => (
                                <pre
                                    key={columns[cellIndex]?.key ?? cellIndex}
                                    className="whitespace-pre-wrap break-words rounded-xl bg-bg-2 p-3 text-xs text-ink-2 font-mono"
                                >
                                    {cell ?? EMPTY_CELL_LABEL}
                                </pre>
                            ))}
                        </div>
                    </section>
                ))}

                {missingBaseAtForkNotice && (
                    <p className="pt-3 text-xs text-ink-3">{missingBaseAtForkNotice}</p>
                )}
            </div>
        </div>
    );
}
