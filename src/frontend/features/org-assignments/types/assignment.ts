import type {
    AssignmentCompletionRule,
    AssignmentContentKind,
    AssignmentProgressStatus,
} from "@/features/assignments/utils/completion-rule";

/** Mirrors learning-service's `AssignmentStatuses`. */
export type AssignmentStatus = "draft" | "active" | "closed";

/** Mirrors learning-service's `AssignmentSourceTypes`. */
export type AssignmentSourceType = "training" | "manual" | "gap_detected";

/** Mirrors learning-service's `AssignmentAudienceKinds`. */
export type AssignmentAudienceKind = "whole_team" | "users" | "group";

/** Mirrors learning-service's `AssignmentReminderScopes`. */
export type AssignmentReminderScope = "unfinished" | "not_started";

export interface AssignmentPersona {
    name: string | null;
    position: string | null;
    personality: string | null;
    difficulty: string | null;
}

export interface AssignmentContentItem {
    kind: AssignmentContentKind;
    reference: string;
    orderIndex: number;
    /** Carried only by a `dialog_scenario` item; the server drops it from every other kind. */
    persona: AssignmentPersona | null;
}

export interface AssignmentAudience {
    kind: AssignmentAudienceKind;
    userIds?: string[] | null;
    groupId?: string | null;
}

/**
 * The repeat schedule as it travels — a raw document, because 40.24 owns the vocabulary and this
 * client must survive a kind it has never heard of rather than guess at one.
 */
export interface AssignmentRepeatSchedule {
    kind: string;
    offsetDays?: number[];
}

/** One row of `GET /admin/assignments`. */
export interface AssignmentSummary {
    id: string;
    title: string;
    sourceType: string;
    status: string;
    audienceKind: string;
    opensAt: string | null;
    deadline: string | null;
    hasRepeatSchedule: boolean;
    repeatOfAssignmentId: string | null;
    repeatWaveIndex: number | null;
    contentItemCount: number;
    assignedCount: number;
    startedCount: number;
    completedCount: number;
    failedThresholdCount: number;
    createdBy: string | null;
    createdAt: string;
    updatedAt: string;
}

/** `GET /admin/assignments/{id}` — everything the edit form needs, jsonb columns included. */
export interface Assignment {
    id: string;
    title: string;
    goal: string | null;
    sourceType: string;
    sourceRef: string | null;
    content: AssignmentContentItem[];
    audience: AssignmentAudience;
    opensAt: string | null;
    deadline: string | null;
    completionRule: AssignmentCompletionRule;
    repeatSchedule: AssignmentRepeatSchedule | null;
    repeatOfAssignmentId: string | null;
    repeatWaveIndex: number | null;
    status: string;
    createdBy: string | null;
    createdAt: string;
    updatedAt: string;
    activatedAt: string | null;
    closedAt: string | null;
}

/**
 * The five-stage funnel of `AssignmentFunnelDto`. The roster counts are null — never zero — when
 * identity-service could not be asked who still works here.
 */
export interface AssignmentFunnel {
    assignedCount: number;
    notStartedCount: number;
    startedCount: number;
    completedCount: number;
    failedThresholdCount: number;
    leftOrganizationCount: number | null;
    assignedActiveCount: number | null;
}

export interface AssignmentDashboardRow {
    userId: string;
    /** Null while `UserReplicas` has never heard of this person — shown as such, never invented. */
    displayName: string | null;
    status: AssignmentProgressStatus | string;
    bestScore: number | null;
    attemptCount: number;
    firstOpenedAt: string | null;
    completedAt: string | null;
    isActiveMember: boolean | null;
}

export interface AssignmentWave {
    assignmentId: string;
    waveIndex: number;
    status: string;
    activatedAt: string | null;
    deadline: string | null;
    funnel: AssignmentFunnel;
}

export interface AssignmentDashboard {
    assignment: AssignmentSummary;
    funnel: AssignmentFunnel;
    rows: AssignmentDashboardRow[];
    series: AssignmentWave[];
    rosterKnown: boolean;
}

/** `GET /admin/assignments/{id}/progress` — the name-free fallback that cannot need identity-service. */
export interface AssignmentProgressRecord {
    userId: string;
    status: AssignmentProgressStatus | string;
    bestScore: number | null;
    attemptCount: number;
    firstOpenedAt: string | null;
    completedAt: string | null;
}

export interface AssignmentReminderResult {
    /** An intention to notify, not a delivery receipt. */
    notifiedCount: number;
    scope: string;
}

export interface CreateAssignmentRequest {
    title: string;
    goal: string | null;
    sourceType: string;
    sourceRef: string | null;
    content: AssignmentContentItem[];
    audience: AssignmentAudience;
    opensAt: string | null;
    deadline: string | null;
    completionRule: AssignmentCompletionRule;
    repeatSchedule: AssignmentRepeatSchedule | null;
}

export type UpdateAssignmentRequest = CreateAssignmentRequest;
