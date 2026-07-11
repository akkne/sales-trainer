import { Icon } from "@/shared/components/icon";
import { companiesCountLabel } from "@/features/companies/lib/format";
import { COMPANY_STATUS_META, COMPANY_STATUS_ORDER, type CompanyStatus } from "@/features/companies/lib/company-status";

interface CompanyListToolbarProps {
    search: string;
    onSearchChange: (value: string) => void;
    count: number;
    statusFilter: CompanyStatus | null;
    onStatusFilterChange: (status: CompanyStatus | null) => void;
}

export function CompanyListToolbar({
    search,
    onSearchChange,
    count,
    statusFilter,
    onStatusFilterChange,
}: CompanyListToolbarProps) {
    return (
        <div className="co-list-toolbar-wrap">
            <div className="co-list-toolbar">
                <div className="co-search-wrap">
                    <span className="co-search-ic" aria-hidden="true">
                        <Icon name="search" size="sm" />
                    </span>
                    <input
                        className="field-search"
                        type="text"
                        value={search}
                        onChange={(event) => onSearchChange(event.target.value)}
                        placeholder="Поиск по названию…"
                        aria-label="Поиск по названию"
                    />
                </div>
                <span className="co-list-count">{companiesCountLabel(count)}</span>
            </div>
            <div className="co-status-filters" role="group" aria-label="Фильтр по статусу">
                <button
                    type="button"
                    className={`co-status-filter-chip ${statusFilter === null ? "active" : ""}`}
                    onClick={() => onStatusFilterChange(null)}
                >
                    Все
                </button>
                {COMPANY_STATUS_ORDER.map((status) => {
                    const meta = COMPANY_STATUS_META[status];
                    return (
                        <button
                            key={status}
                            type="button"
                            className={`co-status-filter-chip ${meta.toneClassName} ${statusFilter === status ? "active" : ""}`}
                            onClick={() => onStatusFilterChange(statusFilter === status ? null : status)}
                        >
                            {meta.label}
                        </button>
                    );
                })}
            </div>
        </div>
    );
}
