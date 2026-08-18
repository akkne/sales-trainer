"use client";

import { ORGANIZATION_PROFILE_FIELDS, PROFILE_FIELD_LABELS } from "../constants/profile-fields";
import type { OrganizationProfile } from "../types/organization-profile";
import { formatEntryCount } from "../utils/russian-counts";

interface ProfileSummaryProps {
    profile: OrganizationProfile;
}

const EMPTY_VALUE_TEXT = "не заполнено";

function SummaryRow({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <div className="grid grid-cols-1 sm:grid-cols-[180px_1fr] gap-1 sm:gap-4 py-3">
            <dt className="text-xs uppercase tracking-wide text-ink-3">{label}</dt>
            <dd className="text-sm text-ink min-w-0">{children}</dd>
        </div>
    );
}

function TextValue({ value }: { value: string | null }) {
    if (!value) return <span className="text-ink-4">{EMPTY_VALUE_TEXT}</span>;
    return <span>{value}</span>;
}

/**
 * The profile as it stands, read-only. Shown once the interview has nothing left to ask, and above
 * the full form as the thing being edited — a customer who opens this screen a month later needs to
 * see what their lessons are saying, not an empty questionnaire.
 */
export function ProfileSummary({ profile }: ProfileSummaryProps) {
    const glossaryTerms = Object.entries(profile.glossary);

    return (
        <dl className="divide-y divide-line">
            <SummaryRow label={PROFILE_FIELD_LABELS[ORGANIZATION_PROFILE_FIELDS.product]}>
                <TextValue value={profile.product} />
            </SummaryRow>

            <SummaryRow label={PROFILE_FIELD_LABELS[ORGANIZATION_PROFILE_FIELDS.icp]}>
                <TextValue value={profile.icp} />
            </SummaryRow>

            <SummaryRow label={PROFILE_FIELD_LABELS[ORGANIZATION_PROFILE_FIELDS.objections]}>
                {profile.objections.length === 0 ? (
                    <span className="text-ink-4">{EMPTY_VALUE_TEXT}</span>
                ) : (
                    <ul className="space-y-1">
                        {profile.objections.map((objection) => (
                            <li key={objection.text}>
                                {objection.text}
                                {objection.bestResponse && (
                                    <span className="text-ink-3"> — {objection.bestResponse}</span>
                                )}
                            </li>
                        ))}
                    </ul>
                )}
            </SummaryRow>

            <SummaryRow label={PROFILE_FIELD_LABELS[ORGANIZATION_PROFILE_FIELDS.scriptStages]}>
                {profile.scriptStages.length === 0 ? (
                    <span className="text-ink-4">{EMPTY_VALUE_TEXT}</span>
                ) : (
                    <span>{profile.scriptStages.join(" → ")}</span>
                )}
            </SummaryRow>

            <SummaryRow label={PROFILE_FIELD_LABELS[ORGANIZATION_PROFILE_FIELDS.tone]}>
                <TextValue value={profile.tone} />
            </SummaryRow>

            <SummaryRow label={PROFILE_FIELD_LABELS[ORGANIZATION_PROFILE_FIELDS.bannedClaims]}>
                {profile.bannedClaims.length === 0 ? (
                    <span className="text-ink-4">запретов нет</span>
                ) : (
                    <ul className="space-y-1">
                        {profile.bannedClaims.map((claim) => (
                            <li key={claim} className="flex gap-2">
                                <span className="text-bad" aria-hidden>
                                    ✕
                                </span>
                                <span>{claim}</span>
                            </li>
                        ))}
                    </ul>
                )}
            </SummaryRow>

            <SummaryRow label={PROFILE_FIELD_LABELS[ORGANIZATION_PROFILE_FIELDS.glossary]}>
                {glossaryTerms.length === 0 ? (
                    <span className="text-ink-4">{EMPTY_VALUE_TEXT}</span>
                ) : (
                    <span>
                        {formatEntryCount(glossaryTerms.length)}:{" "}
                        {glossaryTerms.map(([term]) => term).join(", ")}
                    </span>
                )}
            </SummaryRow>
        </dl>
    );
}
