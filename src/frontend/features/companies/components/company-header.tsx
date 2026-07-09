"use client";

import { Icon } from "@/shared/components/icon";
import type { CompanyDetail } from "@/features/companies/hooks/use-companies";
import { ava, initials } from "@/features/companies/lib/avatar";
import { pluralizeRu } from "@/features/companies/lib/format";

interface CompanyHeaderProps {
    company: CompanyDetail;
    onEdit: () => void;
    onDelete: () => void;
}

/** Identity header — avatar, name, meta, edit/delete actions (§3.2 of the design spec). */
export function CompanyHeader({ company, onEdit, onDelete }: CompanyHeaderProps) {
    const { from, to } = ava(company.id);
    const abbr = initials(company.name);

    const metaParts: string[] = [];
    if (company.practiceCallCount > 0) {
        metaParts.push(
            `${company.practiceCallCount} ${pluralizeRu(company.practiceCallCount, ["тренировка", "тренировки", "тренировок"])}`
        );
    }
    if (company.callLogCount > 0) {
        metaParts.push(
            `${company.callLogCount} ${pluralizeRu(company.callLogCount, ["реальный звонок", "реальных звонка", "реальных звонков"])}`
        );
    }

    return (
        <div className="co-header">
            <div
                className="co-header-av"
                style={{ background: `linear-gradient(135deg, ${from}, ${to})` }}
                aria-hidden="true"
            >
                {abbr}
            </div>
            <div className="co-header-body">
                <div className="co-header-name">
                    <h1 className="h3">{company.name}</h1>
                </div>
                {metaParts.length > 0 && <p className="co-header-meta">{metaParts.join(" · ")}</p>}
            </div>
            <div className="co-header-actions">
                <button className="icon-btn" onClick={onEdit} aria-label="Редактировать компанию">
                    <Icon name="edit" size="md" />
                </button>
                <button className="icon-btn" onClick={onDelete} aria-label="Удалить компанию">
                    <Icon name="delete" size="md" />
                </button>
            </div>
        </div>
    );
}
