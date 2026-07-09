import { Icon } from "@/shared/components/icon";
import { companiesCountLabel } from "@/features/companies/lib/format";

interface CompanyListToolbarProps {
    search: string;
    onSearchChange: (value: string) => void;
    count: number;
}

export function CompanyListToolbar({ search, onSearchChange, count }: CompanyListToolbarProps) {
    return (
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
    );
}
