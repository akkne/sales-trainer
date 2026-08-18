"use client";

import { Button } from "@/shared/components/button";
import { Icon } from "@/shared/components/icon";
import {
    UNPUBLISHED_DRAFT_TITLE,
    describeUnpublishedDraft,
} from "../constants/override-dictionary";

interface UnpublishedDraftBannerProps {
    publishedVersionNumber: number | null;
    onPublish: () => void;
    isPublishing: boolean;
}

/**
 * Decision 8 of the design, and the hole 40.16 left open: editing without publishing binds the
 * team's answers to the previous snapshot, silently. The banner is sticky because the fact stays
 * true while you scroll through the exercises you are editing.
 */
export function UnpublishedDraftBanner({
    publishedVersionNumber,
    onPublish,
    isPublishing,
}: UnpublishedDraftBannerProps) {
    return (
        <div
            role="status"
            className="sticky top-0 z-20 mb-6 flex flex-wrap items-start gap-3 rounded-2xl p-4"
            style={{ background: "var(--amber-soft)" }}
        >
            <Icon
                name="warning"
                size="md"
                style={{ color: "var(--amber)", flexShrink: 0, marginTop: 2 }}
            />
            <div className="min-w-0 flex-1">
                <h2 className="text-sm font-medium" style={{ color: "var(--amber)" }}>
                    {UNPUBLISHED_DRAFT_TITLE}
                </h2>
                <p className="mt-1 text-sm text-ink-2">
                    {describeUnpublishedDraft(publishedVersionNumber)}
                </p>
            </div>
            <Button variant="primary" onClick={onPublish} disabled={isPublishing}>
                {isPublishing ? "Публикуем…" : "Опубликовать"}
            </Button>
        </div>
    );
}
