import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import type { CompanySummary } from "@/features/companies/hooks/use-companies";
import { ava, initials } from "@/features/companies/lib/avatar";
import { relativeTimeRu } from "@/features/companies/lib/format";

interface CompanyRowProps {
    company: CompanySummary;
}

/** One row in the `/companies` list (§2.4 of the design spec). */
export function CompanyRow({ company }: CompanyRowProps) {
    const { from, to } = ava(company.id);
    const abbr = initials(company.name);

    const metaParts: string[] = [];
    if (company.practiceCallCount > 0) {
        metaParts.push(`${company.practiceCallCount} ${company.practiceCallCount === 1 ? "тренировка" : "тренировки"}`);
    }
    if (company.callLogCount > 0) {
        metaParts.push(`${company.callLogCount} ${company.callLogCount === 1 ? "звонок" : "звонка"}`);
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
            <Icon name="chevron-right" size={18} className="co-row-chev" />
        </Link>
    );
}
