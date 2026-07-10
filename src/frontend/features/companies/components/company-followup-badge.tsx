import { getFollowUpTone, getFollowUpToneMeta } from "@/features/companies/lib/company-followup";

interface CompanyFollowUpBadgeProps {
    nextActionAt: string | null | undefined;
    className?: string;
}

/** Renders nothing when there's no follow-up scheduled or it's more than a day out. */
export function CompanyFollowUpBadge({ nextActionAt, className = "" }: CompanyFollowUpBadgeProps) {
    const tone = getFollowUpTone(nextActionAt);
    if (!tone) return null;

    const meta = getFollowUpToneMeta(tone);

    return (
        <span className={`co-followup-badge ${meta.toneClassName} ${className}`.trim()}>
            {meta.label}
        </span>
    );
}
