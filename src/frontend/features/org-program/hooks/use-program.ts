"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import { programService } from "../services/program-service";
import {
    DRAFT_FAILED_MESSAGE,
    ENROLL_FAILED_MESSAGE,
    ENROLL_NO_PUBLISHED_VERSION_MESSAGE,
    NO_ORGANIZATION_MESSAGE,
    PUBLISH_FAILED_MESSAGE,
    PUBLISH_NO_DRAFT_MESSAGE,
} from "../constants/program-dictionary";
import type { ProgramRosterMember } from "../types/program-roster";
import type {
    ProgramDiff,
    ProgramEnrollment,
    ProgramVersion,
    ProgramVersionSummary,
} from "../types/program";

const PROGRAM_VERSIONS_QUERY_KEY = ["org", "program", "versions"];
const PROGRAM_ENROLLMENTS_QUERY_KEY = ["org", "program", "enrollments"];
const ROSTER_STALE_TIME_MILLISECONDS = 60_000;

/**
 * A refused write, in a sentence a РОП can act on. 403 is not a transient failure here: the
 * controller refuses every write when no organization is in context, which is what a platform
 * administrator outside impersonation looks like (`AdminProgramController.RefuseIfNoOrganization`).
 */
export function describeProgramWriteFailure(error: unknown, fallbackMessage: string): string {
    if (error instanceof ApiError) {
        if (error.status === 403) return NO_ORGANIZATION_MESSAGE;
        if (typeof error.payload.message === "string" && error.payload.message.length > 0) {
            return error.payload.message;
        }
    }
    return fallbackMessage;
}

export function useProgramVersions() {
    return useQuery<ProgramVersionSummary[]>({
        queryKey: PROGRAM_VERSIONS_QUERY_KEY,
        queryFn: () => programService.getVersions(),
    });
}

/**
 * The organization's active roster. O18 needs it for two answers the programme endpoints cannot
 * give: the names on the enrollment rows, and how many people hold no pin at all.
 */
export function useProgramRoster() {
    return useQuery<ProgramRosterMember[]>({
        queryKey: ["org", "program", "roster"],
        queryFn: () => programService.getActiveRoster(),
        staleTime: ROSTER_STALE_TIME_MILLISECONDS,
    });
}

export function useProgramEnrollments() {
    return useQuery<ProgramEnrollment[]>({
        queryKey: PROGRAM_ENROLLMENTS_QUERY_KEY,
        queryFn: () => programService.getEnrollments(),
    });
}

/** One version with its ordered items. Only fetched while a preview is open. */
export function useProgramVersion(programVersionId: string | null) {
    return useQuery<ProgramVersion>({
        queryKey: ["org", "program", "version", programVersionId],
        queryFn: () => programService.getVersion(programVersionId as string),
        enabled: programVersionId !== null,
    });
}

/**
 * The diff between two versions. Both ids are required, so the hook stays disabled until a caller
 * has an actual baseline — the first published version has none, and there is nothing to ask for.
 */
export function useProgramDiff(
    programVersionId: string | null,
    baselineProgramVersionId: string | null
) {
    const isEnabled = programVersionId !== null && baselineProgramVersionId !== null;

    return useQuery<ProgramDiff>({
        queryKey: ["org", "program", "diff", programVersionId, baselineProgramVersionId],
        queryFn: () =>
            programService.getDiff(programVersionId as string, baselineProgramVersionId as string),
        enabled: isEnabled,
    });
}

/** Re-derives the draft from the live skill tree. Idempotent: there is at most one draft. */
export function useEnsureProgramDraft() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: () => programService.ensureDraft(),
        onSuccess: (draft) => {
            clientLogger.info("Program draft rebuilt from the skill tree", {
                programVersionId: draft.id,
                itemCount: draft.items.length,
            });
            queryClient.invalidateQueries({ queryKey: PROGRAM_VERSIONS_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.error("Failed to rebuild the programme draft", {
                error: describeProgramWriteFailure(error, DRAFT_FAILED_MESSAGE),
            });
        },
    });
}

/**
 * Freezes the draft. A 409 means there was no draft to freeze; `createdNewVersion: false` in a 200
 * means the draft matched the last published version and was discarded, which the screen reports
 * without moving the version number.
 */
export function usePublishProgramVersion() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: () => programService.publish(),
        onSuccess: (result) => {
            clientLogger.info("Program version published", {
                programVersionId: result.version.id,
                versionNumber: result.version.versionNumber,
                createdNewVersion: result.createdNewVersion,
            });
            queryClient.invalidateQueries({ queryKey: PROGRAM_VERSIONS_QUERY_KEY });
            queryClient.invalidateQueries({ queryKey: PROGRAM_ENROLLMENTS_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.error("Failed to publish the programme draft", {
                error: describeProgramWriteFailure(error, PUBLISH_FAILED_MESSAGE),
            });
        },
    });
}

export function describePublishFailure(error: unknown): string {
    if (error instanceof ApiError && error.status === 409) return PUBLISH_NO_DRAFT_MESSAGE;
    return describeProgramWriteFailure(error, PUBLISH_FAILED_MESSAGE);
}

export function describeEnrollFailure(error: unknown): string {
    if (error instanceof ApiError && error.status === 409) {
        return ENROLL_NO_PUBLISHED_VERSION_MESSAGE;
    }
    return describeProgramWriteFailure(error, ENROLL_FAILED_MESSAGE);
}

/**
 * Puts one person on the newest published version. One call per person on purpose: the API has no
 * bulk route, and the reason is the same reason there is no «перевести всех» — a list of ids handed
 * to the server in one request stops being a set of individual decisions.
 */
export function useEnrollInProgram() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (userId: string) => programService.enroll(userId),
        onSuccess: (enrollment) => {
            clientLogger.info("Learner enrolled on a programme version", {
                programVersionId: enrollment.programVersionId,
                versionNumber: enrollment.programVersionNumber,
            });
            queryClient.invalidateQueries({ queryKey: PROGRAM_ENROLLMENTS_QUERY_KEY });
            queryClient.invalidateQueries({ queryKey: PROGRAM_VERSIONS_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.error("Failed to enroll a learner", {
                error: describeEnrollFailure(error),
            });
        },
    });
}
