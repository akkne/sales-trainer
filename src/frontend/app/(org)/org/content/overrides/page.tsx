"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { Chip } from "@/shared/components/chip";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { Icon } from "@/shared/components/icon";
import { PageHeader } from "@/shared/components/page-header";
import { OverrideStateBadge } from "@/features/org-content-overrides/components/override-state-badge";
import { describeOverrideKind } from "@/features/org-content-overrides/constants/override-dictionary";
import {
    useContentOverrides,
    useDialogModeOverrides,
} from "@/features/org-content-overrides/hooks/use-content-overrides";
import {
    mergeOverrideRows,
    selectStaleRows,
    type OverrideRow,
} from "@/features/org-content-overrides/utils/override-rows";

type OverridesFilter = "stale" | "all";

const SUBTITLE =
    "Когда вы редактируете материал из общей библиотеки, у вас появляется своя копия. Если Sellevate обновит оригинал, копия помечается устаревшей — автоматически они не сливаются.";

/**
 * O14 «Свои версии материалов» (docs/TENANCY/ADMIN_UI_DESIGN.md O14).
 *
 * Two services, one table. Splitting lessons/техники/справки from dialog prompts into tabs was
 * refused in the design: to the customer it is one question — «что я поменял под себя и что из
 * этого отстало от базы» — and which service stores the row is our internal geography. The two
 * reads stay independent so an ai-service outage costs the prompt rows and nothing else.
 *
 * There is deliberately **no «создать копию» button here**. A copy is made only by pressing
 * «редактировать» on a specific material, which is the whole of docs/TENANCY/CONTENT_MODEL.md §1.
 */
export default function OrganizationContentOverridesPage() {
    const [filter, setFilter] = useState<OverridesFilter>("stale");

    const learningQuery = useContentOverrides();
    const dialogModesQuery = useDialogModeOverrides();

    const rows = useMemo(
        () => mergeOverrideRows(learningQuery.data ?? [], dialogModesQuery.data ?? []),
        [learningQuery.data, dialogModesQuery.data]
    );

    const staleRows = useMemo(() => selectStaleRows(rows), [rows]);
    const visibleRows = filter === "stale" ? staleRows : rows;

    const columns: Column<OverrideRow>[] = [
        {
            key: "title",
            header: "Материал",
            render: (row) => (
                <Link href={row.href} className="text-sm text-ink hover:underline">
                    {row.title}
                </Link>
            ),
        },
        {
            key: "kind",
            header: "Тип",
            width: "160px",
            render: (row) => <span className="text-sm text-ink-3">{describeOverrideKind(row.kind)}</span>,
        },
        {
            key: "state",
            header: "Состояние",
            width: "200px",
            render: (row) => <OverrideStateBadge state={row.state} />,
        },
        {
            key: "action",
            header: "",
            width: "140px",
            align: "right",
            render: (row) => (
                <Link href={row.href} className="text-sm text-ink-3 hover:text-ink">
                    {row.isStale ? "Разобрать" : "Открыть"}
                </Link>
            ),
        },
    ];

    // The learning list is the screen; the prompt list is an addition to it. Losing the first is an
    // error state, losing the second is a stripe over a table that still answers most of the question.
    if (learningQuery.isError) {
        return (
            <>
                <PageHeader
                    title="Свои версии материалов"
                    subtitle={SUBTITLE}
                    backHref="/org/content"
                    backLabel="Контент"
                />
                <ErrorState
                    message="Не удалось прочитать список ваших копий."
                    onRetry={() => {
                        void learningQuery.refetch();
                        void dialogModesQuery.refetch();
                    }}
                />
            </>
        );
    }

    const isLoading = learningQuery.isLoading || dialogModesQuery.isLoading;

    return (
        <>
            <PageHeader
                title="Свои версии материалов"
                subtitle={SUBTITLE}
                backHref="/org/content"
                backLabel="Контент"
            />

            {dialogModesQuery.isError && (
                <div
                    role="status"
                    className="mb-4 flex items-start gap-2 rounded-xl p-3 text-sm text-ink-2"
                    style={{ background: "var(--bg-2)" }}
                >
                    <Icon name="info" size="sm" style={{ flexShrink: 0, marginTop: 2 }} />
                    <span>
                        Режимы диалога сейчас недоступны — показаны уроки, техники и справки. Ваши
                        правки промптов никуда не делись.
                    </span>
                </div>
            )}

            {!isLoading && rows.length > 0 && (
                <div className="mb-4 flex flex-wrap gap-2">
                    <Chip
                        tone="neutral"
                        active={filter === "stale"}
                        onClick={() => setFilter("stale")}
                    >
                        Только устаревшие {staleRows.length}
                    </Chip>
                    <Chip tone="neutral" active={filter === "all"} onClick={() => setFilter("all")}>
                        Все {rows.length}
                    </Chip>
                </div>
            )}

            <DataTable
                columns={columns}
                rows={visibleRows}
                rowKey={(row) => row.rowId}
                isLoading={isLoading}
                empty={
                    rows.length === 0 ? (
                        <EmptyState
                            icon="layers"
                            title="Своих версий нет"
                            description="Вы читаете общую библиотеку целиком, и все её улучшения приходят к вам автоматически. Это нормальное и лучшее состояние: копия нужна, только когда текст надо поменять под себя."
                        />
                    ) : (
                        <EmptyState
                            icon="check"
                            title="Устаревших копий нет"
                            description="Все ваши версии сделаны от текущих оригиналов. Разбирать нечего."
                        />
                    )
                }
            />
        </>
    );
}
