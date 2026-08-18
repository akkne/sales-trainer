"use client";

import { Modal } from "@/shared/components/modal";
import { ErrorState } from "@/shared/components/error-state";
import { Skeleton } from "@/shared/components/skeleton";
import { DIFF_FAILED_TITLE, LOAD_FAILED_MESSAGE } from "../constants/program-dictionary";
import { useProgramDiff } from "../hooks/use-program";
import { ProgramDiffView } from "./program-diff-view";

interface ProgramDiffDialogProps {
    open: boolean;
    title: string;
    /** The version being moved to. `null` closes the query, not just the dialog. */
    targetProgramVersionId: string | null;
    /** The version being moved from. Callers with no baseline must not open the dialog at all. */
    baselineProgramVersionId: string | null;
    onClose: () => void;
}

/**
 * «Что изменилось» — the server's diff, shown as-is. The dialog owns the fetch so that the four
 * places that can open one (a published version against its predecessor, and one behind learner
 * against today's version) do not each repeat the loading and error branches.
 */
export function ProgramDiffDialog({
    open,
    title,
    targetProgramVersionId,
    baselineProgramVersionId,
    onClose,
}: ProgramDiffDialogProps) {
    const diffQuery = useProgramDiff(
        open ? targetProgramVersionId : null,
        open ? baselineProgramVersionId : null
    );

    return (
        <Modal open={open} onClose={onClose} title={title} size="lg">
            {diffQuery.isLoading && (
                <div className="flex flex-col gap-3">
                    <Skeleton height={48} rounded={12} />
                    <Skeleton height={48} rounded={12} />
                    <Skeleton height={48} rounded={12} />
                </div>
            )}

            {diffQuery.isError && (
                <ErrorState
                    title={DIFF_FAILED_TITLE}
                    message={LOAD_FAILED_MESSAGE}
                    onRetry={() => diffQuery.refetch()}
                />
            )}

            {diffQuery.data && <ProgramDiffView diff={diffQuery.data} />}
        </Modal>
    );
}
