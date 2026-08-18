"use client";

import { Chip } from "@/shared/components/chip";
import { describeOverrideState, type OverrideState } from "../constants/override-dictionary";

interface OverrideStateBadgeProps {
    state: OverrideState;
}

/**
 * The one place the four override states are rendered, on the list and on the review screen alike
 * (docs/TENANCY/ADMIN_UI_DESIGN.md O14). Two rows that mean the same thing must not look different
 * on two screens — that is how a review queue teaches people to stop reading it.
 */
export function OverrideStateBadge({ state }: OverrideStateBadgeProps) {
    const copy = describeOverrideState(state);

    return (
        <Chip tone={copy.tone} size="sm">
            {copy.label}
        </Chip>
    );
}
