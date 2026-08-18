import { apiClient } from "@/shared/api/api-client";
import type { ProgramRosterMember } from "../types/program-roster";
import type {
    ProgramDiff,
    ProgramEnrollment,
    ProgramVersion,
    ProgramVersionSummary,
    PublishProgramVersionResult,
} from "../types/program";

const PROGRAM_ROUTES = {
    versions: "/admin/program/versions",
    version: (programVersionId: string) => `/admin/program/versions/${programVersionId}`,
    diff: (programVersionId: string, baselineProgramVersionId: string) =>
        `/admin/program/versions/${programVersionId}/diff/${baselineProgramVersionId}`,
    draft: "/admin/program/versions/draft",
    publish: "/admin/program/versions/publish",
    enrollments: "/admin/program/enrollments",
    activeRoster: "/memberships?status=active",
} as const;

/**
 * The seven `AdminProgramController` routes O18 is allowed to call. There is deliberately no method
 * here that moves somebody else's pin: no such route exists, and adding a client-side loop over
 * per-user calls would recreate the «перевести всех» button the design refuses
 * (docs/TENANCY/ADMIN_UI_DESIGN.md §7).
 */
export const programService = {
    getVersions: () => apiClient.get<ProgramVersionSummary[]>(PROGRAM_ROUTES.versions),

    getVersion: (programVersionId: string) =>
        apiClient.get<ProgramVersion>(PROGRAM_ROUTES.version(programVersionId)),

    /**
     * What changes moving from `baselineProgramVersionId` to `programVersionId`. The route spells the
     * target first and the baseline second, which is the opposite of how the sentence reads — the
     * controller passes them to `GetDiffAsync(from: baseline, to: target)`.
     */
    getDiff: (programVersionId: string, baselineProgramVersionId: string) =>
        apiClient.get<ProgramDiff>(PROGRAM_ROUTES.diff(programVersionId, baselineProgramVersionId)),

    /** Opens (or re-derives) the organization's single draft from the live skill tree. */
    ensureDraft: () => apiClient.post<ProgramVersion>(PROGRAM_ROUTES.draft, {}),

    publish: () => apiClient.post<PublishProgramVersionResult>(PROGRAM_ROUTES.publish, {}),

    getEnrollments: () => apiClient.get<ProgramEnrollment[]>(PROGRAM_ROUTES.enrollments),

    /** Idempotent, and it never moves an existing pin — the response says where the person actually is. */
    enroll: (userId: string) =>
        apiClient.post<ProgramEnrollment>(PROGRAM_ROUTES.enrollments, { userId }),

    /**
     * The people currently working at the organization, from identity-service. The programme
     * endpoints know user ids and nothing else, and «кто не зачислен» is only answerable against a
     * real roster — the skill map the design fell back to would have counted a newcomer with no
     * attempts as somebody who does not exist.
     */
    getActiveRoster: () => apiClient.get<ProgramRosterMember[]>(PROGRAM_ROUTES.activeRoster),
};
