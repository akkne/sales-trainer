"use client";

import { Icon } from "@/shared/components/icon";
import {
    QUOTA_STATE_BANNER_COPY,
    type AiUsageQuotaState,
} from "@/features/org-usage/constants/usage-dictionary";

interface QuotaStateBannerProps {
    quotaState: AiUsageQuotaState;
}

const TONE_STYLES: Record<"amber" | "bad", { background: string; foreground: string }> = {
    amber: { background: "var(--amber-soft)", foreground: "var(--amber)" },
    bad: { background: "var(--bad-soft)", foreground: "var(--bad)" },
};

/**
 * The three non-`ok` values of `quotaState` (docs/TENANCY/ADMIN_UI_DESIGN.md O17). `batch_paused`
 * is the one that matters most: it says the background content pipeline stopped while
 * conversations did not, which is the exact distinction an administrator needs to tell a quota wall
 * apart from an outage.
 */
export function QuotaStateBanner({ quotaState }: QuotaStateBannerProps) {
    const copy = QUOTA_STATE_BANNER_COPY[quotaState];
    if (!copy) {
        return null;
    }

    const tone = TONE_STYLES[copy.tone];

    return (
        <div
            role="status"
            className="flex items-start gap-3 rounded-2xl p-4 mb-6"
            style={{ background: tone.background }}
        >
            <Icon
                name={quotaState === "batch_paused" ? "clock" : "warning"}
                size="md"
                style={{ color: tone.foreground, flexShrink: 0, marginTop: 2 }}
            />
            <div>
                <h2 className="font-medium text-sm" style={{ color: tone.foreground }}>
                    {copy.title}
                </h2>
                <p className="mt-1 text-sm text-ink-2">{copy.description}</p>
            </div>
        </div>
    );
}
