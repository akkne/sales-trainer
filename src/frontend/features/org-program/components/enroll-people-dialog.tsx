"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { EmptyState } from "@/shared/components/empty-state";
import { Modal } from "@/shared/components/modal";
import { Skeleton } from "@/shared/components/skeleton";
import type { ProgramRosterMember } from "../types/program-roster";
import {
    ENROLL_BUTTON_LABEL,
    ENROLL_HINT,
    ROSTER_UNAVAILABLE_NOTE,
} from "../constants/program-dictionary";
import { formatVersionLabel } from "../lib/format-program-text";
import { describeEnrollFailure, useEnrollInProgram } from "../hooks/use-program";
import type { ProgramVersionSummary } from "../types/program";

interface EnrollPeopleDialogProps {
    open: boolean;
    enrollableMembers: ProgramRosterMember[];
    currentPublishedVersion: ProgramVersionSummary;
    isRosterLoading: boolean;
    isRosterKnown: boolean;
    onClose: () => void;
}

/**
 * «Зачислить ещё» — one person per click, because the API has one route and it takes one id.
 *
 * The list only offers people who hold no pin, so the dialog physically cannot be used to move
 * anybody: an already-enrolled person is not in it, and the idempotent endpoint would return their
 * existing pin unchanged even if they were.
 */
export function EnrollPeopleDialog({
    open,
    enrollableMembers,
    currentPublishedVersion,
    isRosterLoading,
    isRosterKnown,
    onClose,
}: EnrollPeopleDialogProps) {
    const enrollMutation = useEnrollInProgram();
    const [pendingUserId, setPendingUserId] = useState<string | null>(null);
    const [failureMessage, setFailureMessage] = useState<string | null>(null);

    const enrollOne = (userId: string) => {
        setPendingUserId(userId);
        setFailureMessage(null);
        enrollMutation.mutate(userId, {
            onError: (error) => setFailureMessage(describeEnrollFailure(error)),
            onSettled: () => setPendingUserId(null),
        });
    };

    return (
        <Modal open={open} onClose={onClose} title={ENROLL_BUTTON_LABEL} size="md">
            <p className="text-sm text-ink-2 mb-1">
                Зачисление ставит человека на{" "}
                <span className="tnum" style={{ fontFamily: "var(--font-mono)" }}>
                    {formatVersionLabel(currentPublishedVersion.versionNumber)}
                </span>{" "}
                — последнюю опубликованную версию.
            </p>
            <p className="text-sm text-ink-3 mb-4">{ENROLL_HINT}</p>

            {failureMessage && (
                <p className="text-sm mb-3" style={{ color: "var(--bad)" }} role="alert">
                    {failureMessage}
                </p>
            )}

            {isRosterLoading && (
                <div className="flex flex-col gap-2">
                    <Skeleton height={40} rounded={12} />
                    <Skeleton height={40} rounded={12} />
                </div>
            )}

            {!isRosterLoading && enrollableMembers.length === 0 && (
                <EmptyState
                    icon="users"
                    title="Зачислять некого"
                    description="Все сотрудники организации уже зачислены."
                    compact
                />
            )}

            {!isRosterLoading && enrollableMembers.length > 0 && (
                <ul className="flex flex-col gap-1">
                    {enrollableMembers.map((member) => (
                        <li
                            key={member.userId}
                            className="flex items-center justify-between gap-3 rounded-xl px-3 py-2"
                            style={{ background: "var(--bg-2)" }}
                        >
                            <span className="text-sm text-ink">{member.displayName}</span>
                            <Button
                                size="sm"
                                variant="outline"
                                loading={pendingUserId === member.userId}
                                disabled={pendingUserId !== null}
                                onClick={() => enrollOne(member.userId)}
                            >
                                Зачислить
                            </Button>
                        </li>
                    ))}
                </ul>
            )}

            {!isRosterKnown && <p className="text-xs text-ink-3 mt-4">{ROSTER_UNAVAILABLE_NOTE}</p>}
        </Modal>
    );
}
