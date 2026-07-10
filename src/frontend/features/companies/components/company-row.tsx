import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import type { CompanySummary } from "@/features/companies/hooks/use-companies";
import { ava, initials } from "@/features/companies/lib/avatar";
import { relativeTimeRu, pluralizeRu } from "@/features/companies/lib/format";
import { CompanyStatusBadge } from "@/features/companies/components/company-status-badge";
import { CompanyFollowUpBadge } from "@/features/companies/components/company-followup-badge";

interface CompanyRowProps {
    company: CompanySummary;
}

export function CompanyRow({ company }: CompanyRowProps) {
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
            `${company.callLogCount} ${pluralizeRu(company.callLogCount, ["звонок", "звонка", "звонков"])}`
        );
    }
    metaParts.push(`обновлено ${relativeTimeRu(company.updatedAt)}`);

    return (
        <Link
            href={`/companies/${company.id}`}
            className="co-row"
            aria-label={company.name}
        >
            <div
                className="co-row-av"
                style={{ background: `linear-gradient(135deg, ${from}, ${to})` }}
                aria-hidden="true"
            >
                {abbr}
            </div>
            <div className="co-row-body">
                <p className="co-row-name">{company.name}</p>
                <p className="co-row-meta">{metaParts.join(" · ")}</p>
            </div>
            <CompanyFollowUpBadge nextActionAt={company.nextActionAt} />
            <CompanyStatusBadge status={company.status} className="co-row-status" />
            <Icon name="chevron-right" size={18} className="co-row-chev" />
        </Link>
    );
}
