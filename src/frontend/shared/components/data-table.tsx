"use client";

import { ReactNode } from "react";
import { Icon } from "./icon";
import { Skeleton } from "./skeleton";

export type SortDirection = "asc" | "desc";

export interface TableSort {
    key: string;
    direction: SortDirection;
}

export interface Column<TRow> {
    key: string;
    header: ReactNode;
    render: (row: TRow) => ReactNode;
    align?: "left" | "right" | "center";
    width?: string;
    sortable?: boolean;
}

interface DataTableProps<TRow> {
    columns: Column<TRow>[];
    rows: TRow[];
    rowKey: (row: TRow) => string;
    onRowClick?: (row: TRow) => void;
    /** Shown instead of the table body when there is nothing to list — usually an `EmptyState`. */
    empty: ReactNode;
    isLoading?: boolean;
    sort?: TableSort;
    onSortChange?: (sort: TableSort) => void;
    className?: string;
}

const LOADING_ROW_COUNT = 5;

function nextDirection(column: Column<unknown>, sort: TableSort | undefined): SortDirection {
    if (sort?.key !== column.key) return "asc";
    return sort.direction === "asc" ? "desc" : "asc";
}

/**
 * Eight of the organization panel's screens are lists, and each hand-written `<table>` before this
 * one re-decided horizontal overflow, the loading skeleton and the empty state — the three things
 * a table gets wrong. Sorting is controlled by the caller: what the column means is the caller's
 * business, and most of these lists are sorted by the server anyway.
 */
export function DataTable<TRow>({
    columns,
    rows,
    rowKey,
    onRowClick,
    empty,
    isLoading = false,
    sort,
    onSortChange,
    className = "",
}: DataTableProps<TRow>) {
    if (isLoading) {
        return (
            <div className={`flex flex-col gap-2 ${className}`} aria-label="Загрузка...">
                {Array.from({ length: LOADING_ROW_COUNT }, (_, rowIndex) => (
                    <Skeleton key={rowIndex} height={44} rounded={12} />
                ))}
            </div>
        );
    }

    if (rows.length === 0) {
        return <div className={className}>{empty}</div>;
    }

    return (
        <div className={`w-full overflow-x-auto ${className}`}>
            <table className="w-full border-collapse text-sm">
                <thead>
                    <tr style={{ borderBottom: "1px solid var(--line)" }}>
                        {columns.map((column) => {
                            const isSorted = sort?.key === column.key;
                            const canSort = column.sortable === true && onSortChange !== undefined;
                            return (
                                <th
                                    key={column.key}
                                    scope="col"
                                    style={{ width: column.width, textAlign: column.align ?? "left" }}
                                    className="px-3 py-2.5 text-xs font-medium text-ink-3 whitespace-nowrap"
                                    aria-sort={
                                        isSorted
                                            ? sort.direction === "asc"
                                                ? "ascending"
                                                : "descending"
                                            : undefined
                                    }
                                >
                                    {canSort ? (
                                        <button
                                            type="button"
                                            onClick={() =>
                                                onSortChange({
                                                    key: column.key,
                                                    direction: nextDirection(
                                                        column as Column<unknown>,
                                                        sort
                                                    ),
                                                })
                                            }
                                            className="inline-flex items-center gap-1 hover:text-ink transition-colors"
                                        >
                                            {column.header}
                                            <Icon
                                                name={
                                                    isSorted && sort.direction === "desc"
                                                        ? "chevron-down"
                                                        : "chevron-up"
                                                }
                                                size="sm"
                                                className={isSorted ? "text-ink" : "text-ink-4"}
                                            />
                                        </button>
                                    ) : (
                                        column.header
                                    )}
                                </th>
                            );
                        })}
                    </tr>
                </thead>
                <tbody>
                    {rows.map((row) => (
                        <tr
                            key={rowKey(row)}
                            onClick={onRowClick ? () => onRowClick(row) : undefined}
                            className={
                                onRowClick ? "cursor-pointer hover:bg-bg-2 transition-colors" : ""
                            }
                            style={{ borderBottom: "1px solid var(--line)" }}
                        >
                            {columns.map((column) => (
                                <td
                                    key={column.key}
                                    style={{ textAlign: column.align ?? "left" }}
                                    className="px-3 py-3 text-ink-2 align-middle"
                                >
                                    {column.render(row)}
                                </td>
                            ))}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}
