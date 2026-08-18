"use client";

import { Modal } from "@/shared/components/modal";
import { ErrorState } from "@/shared/components/error-state";
import { Skeleton } from "@/shared/components/skeleton";
import {
    LOAD_FAILED_MESSAGE,
    LOAD_FAILED_TITLE,
    UNKNOWN_LESSON_TITLE,
} from "../constants/program-dictionary";
import { describeLessonCount } from "../lib/format-program-text";
import { useProgramVersion } from "../hooks/use-program";

interface VersionItemsDialogProps {
    open: boolean;
    title: string;
    programVersionId: string | null;
    onClose: () => void;
}

/**
 * «Посмотреть» — the ordered contents of one version, each lesson with the snapshot it is pinned to.
 * A lesson whose snapshot is no longer visible shows «Урок недоступен» rather than the live title:
 * the live title is precisely what a pinned programme is not.
 */
export function VersionItemsDialog({
    open,
    title,
    programVersionId,
    onClose,
}: VersionItemsDialogProps) {
    const versionQuery = useProgramVersion(open ? programVersionId : null);

    return (
        <Modal open={open} onClose={onClose} title={title} size="lg">
            {versionQuery.isLoading && (
                <div className="flex flex-col gap-2">
                    <Skeleton height={36} rounded={12} />
                    <Skeleton height={36} rounded={12} />
                    <Skeleton height={36} rounded={12} />
                </div>
            )}

            {versionQuery.isError && (
                <ErrorState
                    title={LOAD_FAILED_TITLE}
                    message={LOAD_FAILED_MESSAGE}
                    onRetry={() => versionQuery.refetch()}
                />
            )}

            {versionQuery.data && (
                <>
                    <p className="text-sm text-ink-3 mb-3">
                        {describeLessonCount(versionQuery.data.items.length)} в том порядке, в котором
                        их пройдёт зачисленный человек.
                    </p>
                    <ol className="flex flex-col gap-1">
                        {versionQuery.data.items.map((item, position) => (
                            <li
                                key={item.id}
                                className="flex items-baseline justify-between gap-3 rounded-xl px-3 py-2 text-sm"
                                style={{ background: "var(--bg-2)" }}
                            >
                                <span className="flex items-baseline gap-3">
                                    <span
                                        className="text-ink-3 tnum"
                                        style={{ fontFamily: "var(--font-mono)" }}
                                    >
                                        {position + 1}
                                    </span>
                                    <span className="text-ink">
                                        {item.lessonTitle ?? UNKNOWN_LESSON_TITLE}
                                    </span>
                                </span>
                                <span
                                    className="text-ink-3 tnum"
                                    style={{ fontFamily: "var(--font-mono)" }}
                                >
                                    {item.lessonVersionNumber === null
                                        ? "—"
                                        : `v${item.lessonVersionNumber}`}
                                </span>
                            </li>
                        ))}
                    </ol>
                </>
            )}
        </Modal>
    );
}
