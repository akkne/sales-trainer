import { COMPANY_STATUS_META, type CompanyStatus } from "@/features/companies/lib/company-status";

interface CompanyStatusBadgeProps {
    status: CompanyStatus;
    className?: string;
}

export function CompanyStatusBadge({ status, className = "" }: CompanyStatusBadgeProps) {
    const meta = COMPANY_STATUS_META[status];

    return (
        <span className={`co-status-badge ${meta.toneClassName} ${className}`.trim()}>
            {meta.label}
        </span>
    );
}
